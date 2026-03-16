using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// AI玩家角色
    /// </summary>
    public enum AIRole
    {
        /// <summary>坐庄玩家（拥有特权）</summary>
        Dealer,
        /// <summary>庄家队友</summary>
        DealerPartner,
        /// <summary>闲家</summary>
        Opponent
    }

    /// <summary>
    /// AI难度等级
    /// </summary>
    public enum AIDifficulty
    {
        /// <summary>简单（随机性30-40%）</summary>
        Easy = 1,
        /// <summary>中等（随机性15-25%）</summary>
        Medium = 3,
        /// <summary>困难（随机性5-10%）</summary>
        Hard = 6,
        /// <summary>专家（随机性0-5%）</summary>
        Expert = 9
    }

    /// <summary>
    /// AI玩家
    /// </summary>
    public class AIPlayer
    {
        private const string DecisionBundleVersion = "1.0";
        private static readonly JsonSerializerOptions StructuredPayloadJsonOptions = CreateStructuredPayloadJsonOptions();
        private readonly GameConfig _config;
        private readonly Random _random;
        private readonly Random _telemetryRandom;
        private readonly AIDifficulty _difficulty;
        private readonly CardMemory _memory;
        private readonly AIStrategyParameters _strategy;
        private readonly IGameLogger _decisionLogger;
        private readonly RuleAIOptions _ruleAIOptions;
        private readonly RuleAIContextBuilder _contextBuilder;
        private readonly LeadPolicy2 _leadPolicy2;
        private readonly FollowPolicy2 _followPolicy2;
        private readonly BuryPolicy2 _buryPolicy2;

        public AIPlayer(
            GameConfig config,
            AIDifficulty difficulty = AIDifficulty.Medium,
            int seed = 0,
            AIStrategyParameters? strategyParameters = null,
            IGameLogger? decisionLogger = null,
            RuleAIOptions? ruleAIOptions = null)
        {
            _config = config;
            _difficulty = difficulty;
            _random = seed > 0 ? new Random(seed) : new Random();
            _telemetryRandom = seed > 0
                ? new Random(unchecked(seed ^ 0x5A17B3D))
                : new Random(Guid.NewGuid().GetHashCode());
            _memory = new CardMemory(config);
            _strategy = (strategyParameters ?? AIStrategyParameters.CreatePreset(difficulty)).Normalize();
            _decisionLogger = decisionLogger ?? GameLoggerFactory.CreateDefault();
            _ruleAIOptions = ruleAIOptions ?? RuleAIOptions.FromEnvironment();
            _contextBuilder = new RuleAIContextBuilder(config, difficulty, _strategy, _memory, seed);

            var intentResolver = new IntentResolver(config);
            var actionScorer = new ActionScorer(config);
            var explainer = new DecisionExplainer();
            _leadPolicy2 = new LeadPolicy2(
                new LeadCandidateGenerator(config, _memory),
                intentResolver,
                actionScorer,
                explainer);
            _followPolicy2 = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                intentResolver,
                actionScorer,
                explainer);
            _buryPolicy2 = new BuryPolicy2(
                new BuryCandidateGenerator(config),
                intentResolver,
                actionScorer,
                explainer);
        }

        /// <summary>
        /// 记录一墩牌（用于记牌系统）
        /// </summary>
        public void RecordTrick(List<TrickPlay> plays)
        {
            // 简单难度不记牌
            if (_difficulty == AIDifficulty.Easy)
                return;

            _memory.RecordTrick(plays);
        }

        /// <summary>
        /// 重置记牌（新局开始）
        /// </summary>
        public void ResetMemory()
        {
            _memory.Reset();
        }

        /// <summary>
        /// 获取随机决策概率（根据难度）
        /// </summary>
        private double GetRandomnessRate()
        {
            return _difficulty switch
            {
                AIDifficulty.Easy => _strategy.EasyRandomnessRate,
                AIDifficulty.Medium => _strategy.MediumRandomnessRate,
                AIDifficulty.Hard => _strategy.HardRandomnessRate,
                AIDifficulty.Expert => _strategy.ExpertRandomnessRate,
                _ => _strategy.MediumRandomnessRate
            };
        }

        /// <summary>
        /// 判断是否使用随机决策
        /// </summary>
        private bool ShouldUseRandomDecision()
        {
            return _random.NextDouble() < GetRandomnessRate();
        }

        /// <summary>
        /// 首家出牌：根据角色和难度选择最优策略
        /// </summary>
        /// <param name="hand">当前手牌</param>
        /// <param name="role">AI角色</param>
        /// <param name="myPosition">我的位置（用于记牌评估）</param>
        /// <param name="opponentPositions">对手位置列表（用于甩牌评估）</param>
        /// <param name="knownBottomCards">当前玩家可见的底牌（通常仅庄家可见）</param>
        public List<Card> Lead(List<Card> hand, AIRole role = AIRole.Opponent,
            int myPosition = -1, List<int>? opponentPositions = null, List<Card>? knownBottomCards = null,
            AIDecisionLogContext? logContext = null)
        {
            var safeHand = hand ?? new List<Card>();
            var totalStopwatch = Stopwatch.StartNew();

            var contextStopwatch = Stopwatch.StartNew();
            var context = _contextBuilder.BuildLeadContext(
                safeHand,
                role,
                playerIndex: myPosition,
                dealerIndex: logContext?.DealerIndex ?? -1,
                legalActions: null,
                visibleBottomCards: knownBottomCards,
                trickIndex: logContext?.TrickIndex ?? 0,
                turnIndex: logContext?.TurnIndex ?? 0,
                playPosition: logContext?.PlayPosition ?? 1,
                cardsLeftMin: safeHand.Count,
                currentWinningPlayer: logContext?.CurrentWinningPlayer ?? -1,
                defenderScore: logContext?.DefenderScore ?? 0,
                bottomPoints: logContext?.BottomPoints ?? knownBottomCards?.Sum(card => card.Score) ?? 0);
            contextStopwatch.Stop();

            var legacyStopwatch = Stopwatch.StartNew();
            var legacyOutcome = LeadOldPath(safeHand, role, myPosition, opponentPositions, knownBottomCards);
            legacyStopwatch.Stop();

            var shadowStopwatch = Stopwatch.StartNew();
            var shouldCompare = _ruleAIOptions.EnableShadowCompare && ShouldRunShadowCompare();
            DecisionOutcome? newOutcome = null;
            if (_ruleAIOptions.UseRuleAIV21 || shouldCompare)
            {
                newOutcome = LeadNewPath(context);
            }

            var decisionTraceId = ResolveDecisionTraceId(context, logContext);
            var compareSnapshot = BuildCompareSnapshot(legacyOutcome, newOutcome, shouldCompare);

            if (compareSnapshot.ShadowCompared)
                LogAiCompare(context, compareSnapshot, logContext, decisionTraceId);

            shadowStopwatch.Stop();

            var selectedOutcome = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome
                : legacyOutcome;
            var selectedPath = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome.Path
                : legacyOutcome.Path;

            totalStopwatch.Stop();

            LogAiDecision(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId);

            LogAiPerf(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                shouldCompare,
                logContext,
                decisionTraceId);

            LogAiBundle(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId,
                compareSnapshot);

            return new List<Card>(selectedOutcome.SelectedCards);
        }

        /// <summary>
        /// 跟牌：根据角色和当前局势选择最优策略
        /// </summary>
        /// <param name="hand">当前手牌</param>
        /// <param name="leadCards">首家出牌</param>
        /// <param name="currentWinningCards">当前赢牌的牌组（用于判断能否赢）</param>
        /// <param name="role">AI角色</param>
        /// <param name="partnerWinning">对家是否当前赢牌</param>
        public List<Card> Follow(List<Card> hand, List<Card> leadCards,
            List<Card>? currentWinningCards = null,
            AIRole role = AIRole.Opponent,
            bool partnerWinning = false,
            int trickScore = 0,
            AIDecisionLogContext? logContext = null,
            List<Card>? visibleBottomCards = null)
        {
            var safeHand = hand ?? new List<Card>();
            var safeLeadCards = leadCards ?? new List<Card>();
            var totalStopwatch = Stopwatch.StartNew();

            var contextStopwatch = Stopwatch.StartNew();
            var context = _contextBuilder.BuildFollowContext(
                safeHand,
                safeLeadCards,
                currentWinningCards,
                role,
                partnerWinning,
                trickScore,
                cardsLeftMin: safeHand.Count,
                playerIndex: logContext?.PlayerIndex ?? -1,
                dealerIndex: logContext?.DealerIndex ?? -1,
                visibleBottomCards: visibleBottomCards,
                trickIndex: logContext?.TrickIndex ?? 0,
                turnIndex: logContext?.TurnIndex ?? 0,
                playPosition: logContext?.PlayPosition ?? (safeLeadCards.Count == 0 ? 0 : safeLeadCards.Count + 1),
                currentWinningPlayer: logContext?.CurrentWinningPlayer ?? -1,
                defenderScore: logContext?.DefenderScore ?? 0,
                bottomPoints: logContext?.BottomPoints ?? 0);
            contextStopwatch.Stop();

            var legacyStopwatch = Stopwatch.StartNew();
            var legacyOutcome = FollowOldPath(safeHand, safeLeadCards, currentWinningCards, role, partnerWinning);
            legacyStopwatch.Stop();

            var shadowStopwatch = Stopwatch.StartNew();
            var shouldCompare = _ruleAIOptions.EnableShadowCompare && ShouldRunShadowCompare();
            DecisionOutcome? newOutcome = null;
            if (_ruleAIOptions.UseRuleAIV21 || shouldCompare)
            {
                newOutcome = FollowNewPath(context, safeHand, safeLeadCards, currentWinningCards, role, partnerWinning);
            }

            var decisionTraceId = ResolveDecisionTraceId(context, logContext);
            var compareSnapshot = BuildCompareSnapshot(legacyOutcome, newOutcome, shouldCompare);

            if (compareSnapshot.ShadowCompared)
                LogAiCompare(context, compareSnapshot, logContext, decisionTraceId);

            shadowStopwatch.Stop();

            var selectedOutcome = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome
                : legacyOutcome;
            var selectedPath = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome.Path
                : legacyOutcome.Path;

            selectedOutcome = EnsureLegalFollowOutcome(
                selectedOutcome,
                safeHand,
                safeLeadCards,
                currentWinningCards,
                role,
                partnerWinning);
            selectedPath = selectedOutcome.Path;

            totalStopwatch.Stop();

            LogAiDecision(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId);

            LogAiPerf(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                shouldCompare,
                logContext,
                decisionTraceId);

            LogAiBundle(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId,
                compareSnapshot);

            return new List<Card>(selectedOutcome.SelectedCards);
        }

        private DecisionOutcome LeadOldPath(List<Card> hand, AIRole role,
            int myPosition, List<int>? opponentPositions, List<Card>? knownBottomCards)
        {
            if (hand == null || hand.Count == 0)
                return CreateOutcome(
                    PhaseKind.Lead,
                    new List<Card>(),
                    new List<List<Card>>(),
                    "TakeLead",
                    "empty_hand",
                    "legacy");

            // 简单难度：随机选择
            if (_difficulty == AIDifficulty.Easy && ShouldUseRandomDecision())
            {
                var comparer = new CardComparer(_config);
                var validator = new PlayValidator(_config);
                var validCards = hand.Where(c => validator.IsValidPlay(hand, new List<Card> { c })).ToList();
                if (validCards.Count > 0)
                {
                    var selected = new List<Card> { validCards[_random.Next(validCards.Count)] };
                    return CreateOutcome(
                        PhaseKind.Lead,
                        selected,
                        validCards.Select(card => new List<Card> { card }).ToList(),
                        "TakeLead",
                        "easy_random_single",
                        "legacy",
                        "randomized");
                }
            }

            var cardComparer = new CardComparer(_config);
            var playValidator = new PlayValidator(_config);

            var candidates = BuildLeadCandidates(hand, cardComparer, role, myPosition, opponentPositions, knownBottomCards)
                .Where(c => playValidator.IsValidPlay(hand, c))
                .ToList();

            if (candidates.Count == 0)
            {
                var fallback = new List<Card> { hand.OrderByDescending(c => c, cardComparer).First() };
                return CreateOutcome(
                    PhaseKind.Lead,
                    fallback,
                    new List<List<Card>> { fallback },
                    "TakeLead",
                    "lead_high_card_fallback",
                    "legacy",
                    "fallback");
            }

            var selectedCandidate = SelectBestLeadCandidate(candidates, cardComparer, role);
            return CreateOutcome(
                PhaseKind.Lead,
                selectedCandidate,
                candidates,
                DetermineLeadIntent(selectedCandidate),
                DetermineLeadReason(selectedCandidate),
                "legacy");
        }

        private DecisionOutcome FollowOldPath(List<Card> hand, List<Card> leadCards,
            List<Card>? currentWinningCards, AIRole role, bool partnerWinning)
        {
            if (hand == null || hand.Count == 0 || leadCards == null || leadCards.Count == 0)
            {
                return CreateOutcome(
                    PhaseKind.Follow,
                    new List<Card>(),
                    new List<List<Card>>(),
                    DetermineFollowIntent(partnerWinning, false),
                    "empty_follow_input",
                    "legacy");
            }

            int need = leadCards.Count;
            if (hand.Count <= need)
            {
                return CreateOutcome(
                    PhaseKind.Follow,
                    new List<Card>(hand),
                    new List<List<Card>> { new List<Card>(hand) },
                    DetermineFollowIntent(partnerWinning, false),
                    "follow_all_cards",
                    "legacy",
                    "short_hand");
            }

            // 如果没有提供当前赢牌，默认为首家出牌
            if (currentWinningCards == null || currentWinningCards.Count == 0)
                currentWinningCards = leadCards;

            // 简单难度：基本跟牌
            if (_difficulty == AIDifficulty.Easy && ShouldUseRandomDecision())
            {
                var comparer = new CardComparer(_config);
                var validator = new FollowValidator(_config);
                var leadCategory = _config.GetCardCategory(leadCards[0]);
                var leadSuit = leadCards[0].Suit;
                var sameCategoryCards = hand.Where(c => MatchesLeadCategory(c, leadSuit, leadCategory)).ToList();

                if (sameCategoryCards.Count >= need)
                {
                    var shuffled = sameCategoryCards.OrderBy(x => _random.Next()).Take(need).ToList();
                    if (validator.IsValidFollow(hand, leadCards, shuffled))
                    {
                        return CreateOutcome(
                            PhaseKind.Follow,
                            shuffled,
                            new List<List<Card>> { shuffled },
                            DetermineFollowIntent(partnerWinning, CanBeatCards(currentWinningCards, shuffled)),
                            "easy_random_follow",
                            "legacy",
                            "randomized");
                    }
                }
            }

            var cardComparer = new CardComparer(_config);
            var followValidator = new FollowValidator(_config);

            var leadCategory2 = _config.GetCardCategory(leadCards[0]);
            var leadSuit2 = leadCards[0].Suit;
            var sameCategoryCards2 = hand.Where(c => MatchesLeadCategory(c, leadSuit2, leadCategory2)).ToList();

            var candidates = new List<List<Card>>();

            // 有足够同类牌：根据角色和局势选择策略
            if (sameCategoryCards2.Count >= need)
            {
                candidates.AddRange(BuildSameCategoryFollowCandidates(
                    sameCategoryCards2, leadCards, need, cardComparer, role, partnerWinning));
            }
            else
            {
                // 同类牌不足：先出尽同类牌，剩余根据角色选择
                var mustFollow = sameCategoryCards2.OrderByDescending(c => c, cardComparer).ToList();
                int missing = need - mustFollow.Count;

                var remaining = RemoveCards(hand, mustFollow);
                var filler = new List<Card>();

                // 缺门时的策略
                if (partnerWinning)
                {
                    // 对家赢牌：优先送分牌
                    var pointCards = remaining.Where(c => GetCardPoints(c) > 0)
                        .OrderByDescending(c => GetCardPoints(c))
                        .ToList();
                    filler.AddRange(pointCards.Take(missing));
                }
                else if (leadCategory2 == CardCategory.Suit)
                {
                    // 对手赢牌且是副牌：仅在"确实可赢"时才尝试主牌毙牌，避免无效浪费大牌/主牌
                    var trumpCards = remaining.Where(_config.IsTrump)
                        .OrderByDescending(c => c, cardComparer)
                        .ToList();
                    var cutPriority = ResolveRoleWeighted01(_strategy.FollowTrumpCutPriority, role, neutral: 0.6);
                    var maxCuts = (int)System.Math.Ceiling(missing * cutPriority);
                    int cutCount = System.Math.Min(System.Math.Min(missing, maxCuts), trumpCards.Count);
                    if (cutCount > 0)
                    {
                        var trumpAttempt = trumpCards.Take(cutCount).ToList();
                        var remainingAfterTrump = RemoveCards(remaining, trumpAttempt);
                        var trialFiller = trumpAttempt
                            .Concat(remainingAfterTrump.OrderBy(c => c, cardComparer).Take(missing - trumpAttempt.Count))
                            .ToList();
                        var trialCandidate = mustFollow.Concat(trialFiller).ToList();

                        if (CanBeatCards(currentWinningCards, trialCandidate))
                            filler.AddRange(trumpAttempt);
                    }
                }

                // 填充剩余
                if (filler.Count < missing)
                {
                    var used = RemoveCards(remaining, filler);
                    filler.AddRange(used.OrderBy(c => c, cardComparer).Take(missing - filler.Count));
                }

                candidates.Add(mustFollow.Concat(filler).ToList());
            }

            // 兜底候选：按花色优先级选择
            var fallbackCandidate = BuildFallbackFollowCandidate(hand, leadCards, need, cardComparer, followValidator);
            candidates.Add(fallbackCandidate);

            var validCandidates = DeduplicateCandidates(candidates)
                .Where(c => c.Count == need && followValidator.IsValidFollow(hand, leadCards, c))
                .ToList();

            // [P1修复] 如果所有候选都无效，使用兜底候选并确保合法
            if (validCandidates.Count == 0)
            {
                var emergencyCandidate = BuildFallbackFollowCandidate(hand, leadCards, need, cardComparer, followValidator);
                if (followValidator.IsValidFollow(hand, leadCards, emergencyCandidate))
                {
                    return CreateOutcome(
                        PhaseKind.Follow,
                        emergencyCandidate,
                        new List<List<Card>> { emergencyCandidate },
                        DetermineFollowIntent(partnerWinning, CanBeatCards(currentWinningCards, emergencyCandidate)),
                        "follow_validator_fallback",
                        "legacy",
                        "fallback");
                }

                var exhaustiveFallback = FindExhaustiveLegalFollowCandidate(hand, leadCards, need, cardComparer, followValidator);
                if (exhaustiveFallback != null)
                {
                    return CreateOutcome(
                        PhaseKind.Follow,
                        exhaustiveFallback,
                        new List<List<Card>> { exhaustiveFallback },
                        DetermineFollowIntent(partnerWinning, CanBeatCards(currentWinningCards, exhaustiveFallback)),
                        "follow_exhaustive_fallback",
                        "legacy",
                        "fallback",
                        "exhaustive");
                }

                // 最后的兜底：直接取前N张（理论上不应该到这里）
                var finalFallback = hand.Take(need).ToList();
                return CreateOutcome(
                    PhaseKind.Follow,
                    finalFallback,
                    new List<List<Card>> { finalFallback },
                    DetermineFollowIntent(partnerWinning, false),
                    "follow_final_fallback",
                    "legacy",
                    "fallback",
                    "unsafe_fallback");
            }

            var selected = SelectBestFollowCandidate(validCandidates, currentWinningCards, cardComparer, role, partnerWinning);
            return CreateOutcome(
                PhaseKind.Follow,
                selected,
                validCandidates,
                DetermineFollowIntent(partnerWinning, CanBeatCards(currentWinningCards, selected)),
                DetermineFollowReason(partnerWinning, CanBeatCards(currentWinningCards, selected)),
                "legacy");
        }

        private DecisionOutcome LeadNewPath(RuleAIContext context)
        {
            if (context.MyHand.Count == 0)
            {
                return CreateScoredOutcome(
                    PhaseKind.Lead,
                    new List<Card>(),
                    new List<List<Card>>(),
                    "MinimizeLoss",
                    "rule_ai_v21_empty_lead",
                    "rule_ai_v21_lead_policy2",
                    new List<double>(),
                    candidateReasonCodes: null,
                    candidateFeatures: null,
                    explanation: null,
                    "rule_ai_v21",
                    "lead");
            }

            var decision = _leadPolicy2.Decide(context);
            if (decision.SelectedCards.Count == 0)
            {
                var fallback = LeadOldPath(context.MyHand, context.Role, context.PlayerIndex, new List<int>(), context.VisibleBottomCards);
                return BuildPassthroughOutcome(fallback, "rule_ai_v21_lead_fallback_to_legacy");
            }

            return CreatePolicyOutcome("rule_ai_v21_lead_policy2", decision, "rule_ai_v21", "lead", "scored");
        }

        private DecisionOutcome FollowNewPath(
            RuleAIContext context,
            List<Card> hand,
            List<Card> leadCards,
            List<Card>? currentWinningCards,
            AIRole role,
            bool partnerWinning)
        {
            if (hand == null || hand.Count == 0 || leadCards == null || leadCards.Count == 0)
            {
                return CreateScoredOutcome(
                    PhaseKind.Follow,
                    new List<Card>(),
                    new List<List<Card>>(),
                    "MinimizeLoss",
                    "rule_ai_v21_empty_follow",
                    "rule_ai_v21_follow_policy2",
                    new List<double>(),
                    candidateReasonCodes: null,
                    candidateFeatures: null,
                    explanation: null,
                    "rule_ai_v21",
                    "follow");
            }

            var decision = _followPolicy2.Decide(context);
            if (decision.SelectedCards.Count == 0)
            {
                var winningCards = currentWinningCards == null || currentWinningCards.Count == 0 ? leadCards : currentWinningCards;
                var fallback = FollowOldPath(hand, leadCards, winningCards, role, partnerWinning);
                return BuildPassthroughOutcome(fallback, "rule_ai_v21_follow_fallback_to_legacy");
            }

            return CreatePolicyOutcome("rule_ai_v21_follow_policy2", decision, "rule_ai_v21", "follow", "scored");
        }

        private List<List<Card>> BuildRuleAIV21FollowCandidates(
            List<Card> hand,
            List<Card> leadCards,
            List<Card> currentWinningCards,
            AIRole role,
            bool partnerWinning,
            CardComparer comparer,
            FollowValidator validator)
        {
            int need = leadCards.Count;
            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand.Where(card => MatchesLeadCategory(card, leadSuit, leadCategory)).ToList();
            var candidates = new List<List<Card>>();

            if (sameCategoryCards.Count >= need)
            {
                candidates.AddRange(BuildSameCategoryFollowCandidates(
                    sameCategoryCards,
                    leadCards,
                    need,
                    comparer,
                    role,
                    partnerWinning));

                var structureOrdered = sameCategoryCards
                    .OrderBy(card => EstimateSingleCardDiscardCost(sameCategoryCards, card, comparer))
                    .ThenBy(card => card, comparer)
                    .Take(need)
                    .ToList();
                if (structureOrdered.Count == need)
                    candidates.Add(structureOrdered);
            }
            else
            {
                candidates.AddRange(BuildRuleAIV21ShortageFollowCandidates(
                    hand,
                    sameCategoryCards,
                    currentWinningCards,
                    comparer));
            }

            candidates.Add(BuildFallbackFollowCandidate(hand, leadCards, need, comparer, validator));

            return DeduplicateCandidates(candidates)
                .Where(candidate => candidate.Count == need && validator.IsValidFollow(hand, leadCards, candidate))
                .ToList();
        }

        private List<List<Card>> BuildRuleAIV21ShortageFollowCandidates(
            List<Card> hand,
            List<Card> sameCategoryCards,
            List<Card> currentWinningCards,
            CardComparer comparer)
        {
            var candidates = new List<List<Card>>();
            int need = currentWinningCards.Count;
            var mustFollow = sameCategoryCards.OrderByDescending(card => card, comparer).ToList();
            int missing = need - mustFollow.Count;
            var remaining = RemoveCards(hand, mustFollow);

            if (missing <= 0)
                return candidates;

            candidates.Add(AppendFiller(mustFollow, remaining.OrderBy(card => card, comparer).ToList(), missing));

            var pointFirst = remaining
                .OrderByDescending(GetCardPoints)
                .ThenBy(card => card, comparer)
                .ToList();
            candidates.Add(AppendFiller(mustFollow, pointFirst, missing));

            var preserveStructure = remaining
                .OrderBy(card => EstimateSingleCardDiscardCost(remaining, card, comparer))
                .ThenBy(card => card, comparer)
                .ToList();
            candidates.Add(AppendFiller(mustFollow, preserveStructure, missing));

            var nonTrumpFirst = remaining
                .OrderBy(card => _config.IsTrump(card) ? 1 : 0)
                .ThenBy(card => EstimateSingleCardDiscardCost(remaining, card, comparer))
                .ThenBy(card => card, comparer)
                .ToList();
            candidates.Add(AppendFiller(mustFollow, nonTrumpFirst, missing));

            var trumpCards = remaining.Where(_config.IsTrump).OrderBy(card => card, comparer).ToList();
            for (int trumpCount = 1; trumpCount <= System.Math.Min(missing, trumpCards.Count); trumpCount++)
            {
                var trumpAttempt = trumpCards.Take(trumpCount).ToList();
                var remainder = RemoveCards(remaining, trumpAttempt)
                    .OrderBy(card => card, comparer)
                    .ToList();
                var candidate = AppendFiller(mustFollow, trumpAttempt.Concat(remainder).ToList(), missing);
                if (candidate.Count == need && CanBeatCards(currentWinningCards, candidate))
                    candidates.Add(candidate);
            }

            return candidates;
        }

        private string ResolveRuleAIV21FollowIntent(
            RuleAIContext context,
            List<List<Card>> candidates,
            List<Card> currentWinningCards)
        {
            if (context.PartnerWinning)
                return "PassToMate";

            var winningCandidates = candidates
                .Where(candidate => CanBeatCards(currentWinningCards, candidate))
                .ToList();
            if (winningCandidates.Count == 0)
                return "MinimizeLoss";

            var cheapestWinningCandidate = winningCandidates
                .OrderBy(candidate => candidate.Count(_config.IsTrump))
                .ThenBy(candidate => CalculateWinMargin(candidate, currentWinningCards, new CardComparer(_config)))
                .First();

            if (context.TrickScore >= 10)
                return "TakeScore";

            if (cheapestWinningCandidate.Count(_config.IsTrump) == 0)
                return "TakeScore";

            return "MinimizeLoss";
        }

        private CandidateEvaluation ScoreRuleAIV21FollowCandidate(
            RuleAIContext context,
            List<Card> candidate,
            List<Card> currentWinningCards,
            string intent,
            CardComparer comparer)
        {
            bool canBeat = CanBeatCards(currentWinningCards, candidate);
            int points = candidate.Sum(GetCardPoints);
            int trumpCount = candidate.Count(_config.IsTrump);
            int structureLoss = EstimateStructureLoss(context.MyHand, candidate, comparer);
            int averageValue = candidate.Count == 0 ? 0 : (int)candidate.Average(GetCardValue);
            int winMargin = canBeat ? CalculateWinMargin(candidate, currentWinningCards, comparer) : 0;
            double normalizedWinMargin = NormalizeWinMargin(winMargin);

            double score;
            string reason;

            if (intent == "PassToMate")
            {
                score = 20
                    + points * 6.0
                    - (canBeat ? 120.0 : 0.0)
                    - trumpCount * 10.0
                    - structureLoss * 3.5
                    - averageValue / 25.0;
                reason = canBeat
                    ? "pass_to_mate_avoid_overtake"
                    : points > 0
                        ? "pass_to_mate_send_points"
                        : "pass_to_mate_keep_power";
            }
            else if (intent == "TakeScore")
            {
                score = (canBeat ? 60.0 : -25.0)
                    + context.TrickScore * (canBeat ? 4.0 : -1.5)
                    - trumpCount * 9.0
                    - structureLoss * 4.0
                    - averageValue / 30.0
                    - normalizedWinMargin
                    - (!canBeat ? points * 1.2 : 0.0);
                reason = canBeat
                    ? "take_score_low_cost_win"
                    : "take_score_cannot_secure";
            }
            else
            {
                score = (canBeat
                        ? (context.TrickScore >= 15 ? 18.0 : -18.0)
                        : 12.0)
                    - points * 2.0
                    - trumpCount * 8.0
                    - structureLoss * 4.5
                    - averageValue / 20.0
                    - (canBeat ? normalizedWinMargin * 1.2 : 0.0);
                reason = canBeat
                    ? "minimize_loss_decline_expensive_win"
                    : "minimize_loss_preserve_structure";
            }

            if (context.IsDealerSide)
                score -= trumpCount * 1.5;

            return new CandidateEvaluation
            {
                Cards = CloneCards(candidate),
                Score = score,
                Reason = reason
            };
        }

        private int EstimateStructureLoss(List<Card> hand, List<Card> candidate, CardComparer comparer)
        {
            var before = EstimateStructureValue(hand, comparer);
            var after = EstimateStructureValue(RemoveCards(hand, candidate), comparer);
            return System.Math.Max(0, before - after);
        }

        private static double NormalizeWinMargin(int winMargin)
        {
            if (winMargin <= 0)
                return 0;

            return System.Math.Min(40.0, winMargin / 20.0);
        }

        private int EstimateStructureValue(List<Card> cards, CardComparer comparer)
        {
            if (cards == null || cards.Count == 0)
                return 0;

            int pairValue = CountPairs(cards) * 8;
            int tractorValue = CountTractorPairUnits(cards, comparer) * 12;
            return pairValue + tractorValue;
        }

        private int CountTractorPairUnits(List<Card> cards, CardComparer comparer)
        {
            int total = 0;
            foreach (var group in BuildSystemGroups(cards))
            {
                var pairReps = group
                    .GroupBy(card => card)
                    .Where(g => g.Count() >= 2)
                    .Select(g => g.Key)
                    .OrderByDescending(card => card, comparer)
                    .ToList();

                if (pairReps.Count < 2)
                    continue;

                int index = 0;
                while (index < pairReps.Count - 1)
                {
                    int run = 1;
                    while (index + run < pairReps.Count &&
                        IsConsecutivePairForTractor(pairReps[index + run - 1], pairReps[index + run]))
                    {
                        run++;
                    }

                    if (run >= 2)
                        total += run;

                    index += run;
                }
            }

            return total;
        }

        private IEnumerable<List<Card>> BuildSystemGroups(List<Card> cards)
        {
            var groups = new List<List<Card>>();

            var trumpCards = cards.Where(_config.IsTrump).ToList();
            if (trumpCards.Count > 0)
                groups.Add(trumpCards);

            groups.AddRange(cards
                .Where(card => !_config.IsTrump(card))
                .GroupBy(card => card.Suit)
                .Select(group => group.ToList()));

            return groups;
        }

        private bool IsConsecutivePairForTractor(Card higher, Card lower)
        {
            var cards = new List<Card> { higher, higher, lower, lower };
            return new CardPattern(cards, _config).IsTractor(cards);
        }

        private int EstimateSingleCardDiscardCost(List<Card> source, Card card, CardComparer comparer)
        {
            var remaining = RemoveCards(source, new List<Card> { card });
            int structureLoss = EstimateStructureValue(source, comparer) - EstimateStructureValue(remaining, comparer);
            int pointCost = GetCardPoints(card) * 4;
            int trumpCost = _config.IsTrump(card) ? 12 : 0;
            int valueCost = GetCardValue(card) / 40;
            return structureLoss * 10 + pointCost + trumpCost + valueCost;
        }

        private List<Card> AppendFiller(List<Card> mustFollow, List<Card> orderedFiller, int missing)
        {
            var result = new List<Card>(mustFollow);
            result.AddRange(orderedFiller.Take(missing));
            return result;
        }

        private DecisionOutcome BuildPassthroughOutcome(DecisionOutcome source, string selectedReason)
        {
            return CreateScoredOutcome(
                source.Phase,
                CloneCards(source.SelectedCards),
                CloneCandidateSets(source.Candidates),
                source.PrimaryIntent,
                selectedReason,
                "rule_ai_v21_m1_passthrough",
                source.CandidateScores,
                source.CandidateReasonCodes,
                source.CandidateFeatures,
                source.Explanation,
                source.Tags.Concat(new[] { "rule_ai_v21_m1", "passthrough" }).Distinct().ToArray());
        }

        private DecisionOutcome CreateOutcome(
            PhaseKind phase,
            List<Card> selectedCards,
            List<List<Card>> candidates,
            string primaryIntent,
            string selectedReason,
            params string[] tags)
        {
            return new DecisionOutcome
            {
                Phase = phase,
                SelectedCards = CloneCards(selectedCards),
                Candidates = CloneCandidateSets(candidates),
                PrimaryIntent = primaryIntent,
                SelectedReason = selectedReason,
                Path = "legacy",
                CandidateScores = new List<double>(),
                CandidateReasonCodes = new List<string?>(),
                CandidateFeatures = new List<Dictionary<string, double>>(),
                Tags = tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct().ToList()
            };
        }

        private DecisionOutcome CreateScoredOutcome(
            PhaseKind phase,
            List<Card> selectedCards,
            List<List<Card>> candidates,
            string primaryIntent,
            string selectedReason,
            string path,
            IEnumerable<double>? candidateScores,
            IEnumerable<string?>? candidateReasonCodes = null,
            IEnumerable<Dictionary<string, double>>? candidateFeatures = null,
            DecisionExplanation? explanation = null,
            params string[] tags)
        {
            return new DecisionOutcome
            {
                Phase = phase,
                SelectedCards = CloneCards(selectedCards),
                Candidates = CloneCandidateSets(candidates),
                PrimaryIntent = primaryIntent,
                SelectedReason = selectedReason,
                Path = path,
                CandidateScores = candidateScores?.ToList() ?? new List<double>(),
                CandidateReasonCodes = candidateReasonCodes?.ToList() ?? new List<string?>(),
                CandidateFeatures = candidateFeatures?.Select(features => new Dictionary<string, double>(features)).ToList()
                    ?? new List<Dictionary<string, double>>(),
                Tags = tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct().ToList(),
                Explanation = explanation
            };
        }

        private DecisionOutcome CreatePolicyOutcome(
            string path,
            PhaseDecision decision,
            params string[] tags)
        {
            return new DecisionOutcome
            {
                Phase = decision.Phase,
                SelectedCards = CloneCards(decision.SelectedCards),
                Candidates = decision.ScoredActions.Select(action => CloneCards(action.Cards)).ToList(),
                PrimaryIntent = decision.Intent.PrimaryIntent.ToString(),
                SelectedReason = decision.SelectedReason,
                Path = path,
                CandidateScores = decision.ScoredActions.Select(action => action.Score).ToList(),
                CandidateReasonCodes = decision.ScoredActions.Select(action => string.IsNullOrWhiteSpace(action.ReasonCode) ? null : action.ReasonCode).ToList(),
                CandidateFeatures = decision.ScoredActions.Select(action => new Dictionary<string, double>(action.Features)).ToList(),
                Tags = tags.Where(tag => !string.IsNullOrWhiteSpace(tag)).Distinct().ToList(),
                Explanation = decision.Explanation
            };
        }

        private DecisionExplanation BuildDecisionExplanation(RuleAIContext context, DecisionOutcome outcome)
        {
            if (outcome.Explanation != null)
                return outcome.Explanation;

            var orderedCandidates = OrderCandidatesForLogging(context, outcome);
            var useStoredScores = outcome.CandidateScores.Count == orderedCandidates.Count && orderedCandidates.Count > 0;
            var topCandidates = orderedCandidates
                .Take(3)
                .Select(BuildReadableCandidateKey)
                .ToList();
            var topScores = useStoredScores
                ? outcome.CandidateScores.Take(3).ToList()
                : orderedCandidates
                    .Take(3)
                    .Select((_, index) => (double)(orderedCandidates.Count - index))
                    .ToList();

            var effectiveCandidates = orderedCandidates.Count > 0
                ? orderedCandidates
                : new List<List<Card>> { CloneCards(outcome.SelectedCards) };

            return new DecisionExplanation
            {
                Phase = outcome.Phase,
                PrimaryIntent = outcome.PrimaryIntent,
                SelectedReason = outcome.SelectedReason,
                CandidateCount = effectiveCandidates.Count,
                TopCandidates = topCandidates,
                TopScores = topScores,
                SelectedAction = outcome.SelectedCards.Select(card => card.ToString()).ToList(),
                Tags = new List<string>(outcome.Tags)
            };
        }

        private List<List<Card>> OrderCandidatesForLogging(RuleAIContext context, DecisionOutcome outcome)
        {
            var candidates = outcome.Candidates.Count > 0
                ? CloneCandidateSets(outcome.Candidates)
                : new List<List<Card>> { CloneCards(outcome.SelectedCards) };

            if (candidates.Count <= 1)
                return candidates;

            if (outcome.CandidateScores.Count == candidates.Count && outcome.CandidateScores.Count > 0)
                return candidates;

            var comparer = new CardComparer(_config);
            if (outcome.Phase == PhaseKind.Lead)
            {
                candidates.Sort((left, right) => CompareLeadCandidates(right, left, comparer, context.Role));
            }
            else if (outcome.Phase == PhaseKind.Follow)
            {
                candidates.Sort((left, right) => CompareFollowCandidates(
                    right,
                    left,
                    context.CurrentWinningCards.Count > 0 ? context.CurrentWinningCards : context.LeadCards,
                    comparer,
                    context.Role,
                    context.PartnerWinning));
            }

            return candidates;
        }

        private void LogAiDecision(
            RuleAIContext context,
            DecisionOutcome outcome,
            string path,
            double totalMs,
            double contextMs,
            double legacyMs,
            double shadowMs,
            AIDecisionLogContext? logContext,
            string decisionTraceId)
        {
            var explanation = BuildDecisionExplanation(context, outcome);
            var entry = CreateAiLogEntry("ai.decision", LogCategories.Decision, context, logContext, decisionTraceId);

            entry.Payload = new Dictionary<string, object?>
            {
                ["decision_trace_id"] = decisionTraceId,
                ["phase"] = context.Phase.ToString(),
                ["path"] = path,
                ["phase_policy"] = explanation.PhasePolicy,
                ["difficulty"] = _difficulty.ToString(),
                ["player_index"] = ResolvePlayerIndex(context, logContext),
                ["role"] = context.Role.ToString(),
                ["partner_winning"] = context.PartnerWinning,
                ["primary_intent"] = explanation.PrimaryIntent,
                ["secondary_intent"] = explanation.SecondaryIntent,
                ["selected_reason"] = explanation.SelectedReason,
                ["candidate_count"] = explanation.CandidateCount,
                ["selected_cards"] = BuildCardPayload(outcome.SelectedCards),
                ["top_candidates"] = explanation.TopCandidates,
                ["top_scores"] = explanation.TopScores,
                ["hard_rule_rejects"] = explanation.HardRuleRejects,
                ["risk_flags"] = explanation.RiskFlags,
                ["selected_action_features"] = explanation.SelectedActionFeatures,
                ["tags"] = explanation.Tags,
                ["hand_count"] = context.HandCount,
                ["trump_count"] = context.TrumpCount,
                ["point_card_count"] = context.PointCardCount,
                ["trick_score"] = context.TrickScore,
                ["cards_left_min"] = context.CardsLeftMin
            };
            entry.Metrics = new Dictionary<string, double>
            {
                ["total_ms"] = totalMs,
                ["context_ms"] = contextMs,
                ["legacy_ms"] = legacyMs,
                ["shadow_ms"] = shadowMs,
                ["selected_score"] = ResolveSelectedScore(outcome)
            };

            _decisionLogger.Log(entry);
        }

        private void LogAiCompare(
            RuleAIContext context,
            DecisionCompareSnapshot compareSnapshot,
            AIDecisionLogContext? logContext,
            string decisionTraceId)
        {
            var entry = CreateAiLogEntry("ai.compare", LogCategories.Diag, context, logContext, decisionTraceId);

            entry.Payload = new Dictionary<string, object?>
            {
                ["decision_trace_id"] = decisionTraceId,
                ["phase"] = context.Phase.ToString(),
                ["player_index"] = ResolvePlayerIndex(context, logContext),
                ["role"] = context.Role.ToString(),
                ["partner_winning"] = context.PartnerWinning,
                ["divergence"] = compareSnapshot.Divergence,
                ["old_path"] = compareSnapshot.OldPath,
                ["new_path"] = compareSnapshot.NewPath,
                ["old_action"] = BuildCardPayload(compareSnapshot.OldAction),
                ["new_action"] = BuildCardPayload(compareSnapshot.NewAction),
                ["old_reason"] = compareSnapshot.OldReason,
                ["new_reason"] = compareSnapshot.NewReason,
                ["old_intent"] = compareSnapshot.OldIntent,
                ["new_intent"] = compareSnapshot.NewIntent
            };
            entry.Metrics = new Dictionary<string, double>
            {
                ["old_candidate_count"] = compareSnapshot.OldCandidateCount,
                ["new_candidate_count"] = compareSnapshot.NewCandidateCount
            };

            _decisionLogger.Log(entry);
        }

        private void LogAiPerf(
            RuleAIContext context,
            DecisionOutcome outcome,
            string path,
            double totalMs,
            double contextMs,
            double legacyMs,
            double shadowMs,
            bool shadowCompared,
            AIDecisionLogContext? logContext,
            string decisionTraceId)
        {
            var entry = CreateAiLogEntry("ai.perf", LogCategories.Perf, context, logContext, decisionTraceId);

            entry.Payload = new Dictionary<string, object?>
            {
                ["decision_trace_id"] = decisionTraceId,
                ["phase"] = context.Phase.ToString(),
                ["path"] = path,
                ["player_index"] = ResolvePlayerIndex(context, logContext),
                ["candidate_count"] = outcome.Candidates.Count,
                ["selected_count"] = outcome.SelectedCards.Count,
                ["shadow_compared"] = shadowCompared
            };
            entry.Metrics = new Dictionary<string, double>
            {
                ["total_ms"] = totalMs,
                ["context_ms"] = contextMs,
                ["legacy_ms"] = legacyMs,
                ["shadow_ms"] = shadowMs
            };

            _decisionLogger.Log(entry);
        }

        private void LogAiBundle(
            RuleAIContext context,
            DecisionOutcome outcome,
            string path,
            double totalMs,
            double contextMs,
            double legacyMs,
            double shadowMs,
            AIDecisionLogContext? logContext,
            string decisionTraceId,
            DecisionCompareSnapshot compareSnapshot)
        {
            if (!_ruleAIOptions.DecisionTraceEnabled)
                return;

            var explanation = BuildDecisionExplanation(context, outcome);
            var selectedMetadata = ResolveSelectedCandidateMetadata(context, outcome, explanation);
            var candidateDetails = BuildCandidateDetails(context, outcome);
            var entry = CreateAiLogEntry("ai.bundle", LogCategories.Diag, context, logContext, decisionTraceId);

            entry.Payload = new Dictionary<string, object?>
            {
                ["decision_trace_id"] = decisionTraceId,
                ["bundle_version"] = DecisionBundleVersion,
                ["phase"] = context.Phase.ToString(),
                ["path"] = path,
                ["player_index"] = ResolvePlayerIndex(context, logContext),
                ["bundle"] = new Dictionary<string, object?>
                {
                    ["meta"] = BuildBundleMeta(context, path, logContext, decisionTraceId),
                    ["context_snapshot"] = BuildContextSnapshot(context, logContext),
                    ["intent_snapshot"] = BuildIntentSnapshot(explanation),
                    ["candidate_details"] = candidateDetails,
                    ["selected_action"] = new Dictionary<string, object?>
                    {
                        ["cards"] = BuildCardPayload(outcome.SelectedCards),
                        ["score"] = selectedMetadata.Score,
                        ["reason_code"] = selectedMetadata.ReasonCode ?? explanation.SelectedReason,
                        ["features"] = selectedMetadata.Features
                    },
                    ["compare_snapshot"] = BuildCompareSnapshotPayload(compareSnapshot),
                    ["perf_snapshot"] = BuildPerfSnapshotPayload(outcome, totalMs, contextMs, legacyMs, shadowMs),
                    ["truth_snapshot"] = BuildTruthSnapshot(logContext),
                    ["algorithm_trace"] = BuildAlgorithmTrace(context, explanation, path, compareSnapshot, candidateDetails.Count)
                }
            };
            entry.Metrics = new Dictionary<string, double>
            {
                ["candidate_count"] = candidateDetails.Count,
                ["selected_score"] = selectedMetadata.Score ?? 0
            };

            _decisionLogger.Log(entry);
        }

        private LogEntry CreateAiLogEntry(
            string eventName,
            string category,
            RuleAIContext context,
            AIDecisionLogContext? logContext,
            string decisionTraceId)
        {
            return new LogEntry
            {
                SchemaVersion = "1.3",
                Event = eventName,
                Category = category,
                Level = LogLevels.Info,
                SessionId = logContext?.SessionId,
                GameId = logContext?.GameId,
                RoundId = logContext?.RoundId,
                TrickId = ResolveTrickId(context, logContext),
                TurnId = ResolveTurnId(context, logContext),
                Phase = context.Phase.ToString(),
                Actor = ResolveLogActor(logContext),
                CorrelationId = decisionTraceId
            };
        }

        private static string ResolveLogActor(AIDecisionLogContext? logContext)
        {
            if (!string.IsNullOrWhiteSpace(logContext?.Actor))
                return logContext.Actor!;

            if (logContext?.PlayerIndex != null)
                return $"player_{logContext.PlayerIndex.Value}";

            return "rule_ai";
        }

        private string ResolveDecisionTraceId(RuleAIContext context, AIDecisionLogContext? logContext)
        {
            if (!string.IsNullOrWhiteSpace(logContext?.DecisionTraceId))
                return logContext.DecisionTraceId!;

            var phaseToken = context.Phase.ToString().ToLowerInvariant();
            var playerToken = ResolvePlayerIndex(context, logContext);
            var trickToken = ResolveTrickId(context, logContext) ?? "no_trick";
            var turnToken = ResolveTurnId(context, logContext) ?? "no_turn";
            var uniqueSuffix = Guid.NewGuid().ToString("N").Substring(0, 8);
            return $"{phaseToken}_p{playerToken}_{trickToken}_{turnToken}_{uniqueSuffix}";
        }

        private DecisionCompareSnapshot BuildCompareSnapshot(
            DecisionOutcome legacyOutcome,
            DecisionOutcome? newOutcome,
            bool shadowCompared)
        {
            if (!shadowCompared || newOutcome == null)
            {
                return new DecisionCompareSnapshot
                {
                    ShadowCompared = false,
                    OldCandidateCount = legacyOutcome.Candidates.Count,
                    NewCandidateCount = newOutcome?.Candidates.Count ?? 0
                };
            }

            return new DecisionCompareSnapshot
            {
                ShadowCompared = true,
                Divergence = !string.Equals(
                    BuildCandidateKey(legacyOutcome.SelectedCards),
                    BuildCandidateKey(newOutcome.SelectedCards),
                    StringComparison.Ordinal),
                OldPath = legacyOutcome.Path,
                NewPath = newOutcome.Path,
                OldAction = CloneCards(legacyOutcome.SelectedCards),
                NewAction = CloneCards(newOutcome.SelectedCards),
                OldReason = legacyOutcome.SelectedReason,
                NewReason = newOutcome.SelectedReason,
                OldIntent = legacyOutcome.PrimaryIntent,
                NewIntent = newOutcome.PrimaryIntent,
                OldCandidateCount = legacyOutcome.Candidates.Count,
                NewCandidateCount = newOutcome.Candidates.Count
            };
        }

        private Dictionary<string, object?> BuildBundleMeta(
            RuleAIContext context,
            string path,
            AIDecisionLogContext? logContext,
            string decisionTraceId)
        {
            return new Dictionary<string, object?>
            {
                ["decision_trace_id"] = decisionTraceId,
                ["phase"] = context.Phase.ToString(),
                ["path"] = path,
                ["difficulty"] = _difficulty.ToString(),
                ["player_index"] = ResolvePlayerIndex(context, logContext),
                ["role"] = context.Role.ToString(),
                ["session_id"] = logContext?.SessionId,
                ["game_id"] = logContext?.GameId,
                ["round_id"] = logContext?.RoundId,
                ["trick_id"] = ResolveTrickId(context, logContext),
                ["turn_id"] = ResolveTurnId(context, logContext)
            };
        }

        private Dictionary<string, object?> BuildContextSnapshot(
            RuleAIContext context,
            AIDecisionLogContext? logContext)
        {
            return new Dictionary<string, object?>
            {
                ["trick_index"] = logContext?.TrickIndex ?? context.DecisionFrame.TrickIndex,
                ["turn_index"] = logContext?.TurnIndex ?? context.DecisionFrame.TurnIndex,
                ["play_position"] = logContext?.PlayPosition ?? context.DecisionFrame.PlayPosition,
                ["dealer_index"] = logContext?.DealerIndex ?? context.DealerIndex,
                ["current_winning_player"] = logContext?.CurrentWinningPlayer ?? context.DecisionFrame.CurrentWinningPlayer,
                ["partner_winning"] = context.PartnerWinning,
                ["trick_score"] = context.TrickScore,
                ["cards_left_min"] = context.CardsLeftMin,
                ["my_hand"] = BuildCardPayload(context.MyHand),
                ["lead_cards"] = BuildCardPayload(context.LeadCards),
                ["current_winning_cards"] = BuildCardPayload(context.CurrentWinningCards),
                ["visible_bottom_cards"] = BuildCardPayload(context.VisibleBottomCards),
                ["game_config"] = new Dictionary<string, object?>
                {
                    ["trump_suit"] = _config.TrumpSuit?.ToString(),
                    ["level_rank"] = _config.LevelRank.ToString(),
                    ["throw_fail_penalty"] = _config.ThrowFailPenalty,
                    ["enable_counter_bottom"] = _config.EnableCounterBottom,
                    ["strict_follow_structure"] = context.RuleProfile.StrictFollowStructure,
                    ["strict_cut_structure"] = context.RuleProfile.StrictCutStructure
                },
                ["hand_profile"] = ToStructuredElement(context.HandProfile),
                ["memory_snapshot"] = ToStructuredElement(context.MemorySnapshot),
                ["inference_snapshot"] = ToStructuredElement(context.InferenceSnapshot),
                ["decision_frame"] = ToStructuredElement(context.DecisionFrame)
            };
        }

        private static Dictionary<string, object?> BuildIntentSnapshot(DecisionExplanation explanation)
        {
            return new Dictionary<string, object?>
            {
                ["primary_intent"] = explanation.PrimaryIntent,
                ["secondary_intent"] = explanation.SecondaryIntent,
                ["selected_reason"] = explanation.SelectedReason,
                ["phase_policy"] = explanation.PhasePolicy,
                ["hard_rule_rejects"] = explanation.HardRuleRejects,
                ["risk_flags"] = explanation.RiskFlags,
                ["tags"] = explanation.Tags
            };
        }

        private List<Dictionary<string, object?>> BuildCandidateDetails(RuleAIContext context, DecisionOutcome outcome)
        {
            var orderedCandidates = OrderCandidatesForLogging(context, outcome);
            if (orderedCandidates.Count == 0 && outcome.SelectedCards.Count > 0)
                orderedCandidates.Add(CloneCards(outcome.SelectedCards));

            var selectedKey = BuildCandidateKey(outcome.SelectedCards);
            if (!string.IsNullOrWhiteSpace(selectedKey) &&
                orderedCandidates.All(candidate => !string.Equals(BuildCandidateKey(candidate), selectedKey, StringComparison.Ordinal)))
            {
                orderedCandidates.Insert(0, CloneCards(outcome.SelectedCards));
            }

            orderedCandidates = LimitCandidatesForTrace(orderedCandidates, selectedKey);
            var metadataByKey = BuildCandidateMetadataLookup(outcome);
            var details = new List<Dictionary<string, object?>>(orderedCandidates.Count);

            for (int index = 0; index < orderedCandidates.Count; index++)
            {
                var candidate = orderedCandidates[index];
                var candidateKey = BuildCandidateKey(candidate);
                metadataByKey.TryGetValue(candidateKey, out var metadata);

                details.Add(new Dictionary<string, object?>
                {
                    ["candidate_index"] = index,
                    ["cards"] = BuildCardPayload(candidate),
                    ["score"] = metadata?.Score,
                    ["reason_code"] = metadata?.ReasonCode,
                    ["features"] = metadata?.Features ?? new Dictionary<string, double>(),
                    ["is_selected"] = string.Equals(candidateKey, selectedKey, StringComparison.Ordinal)
                });
            }

            return details;
        }

        private Dictionary<string, CandidateMetadata> BuildCandidateMetadataLookup(DecisionOutcome outcome)
        {
            var lookup = new Dictionary<string, CandidateMetadata>(StringComparer.Ordinal);
            var candidates = outcome.Candidates.Count > 0
                ? outcome.Candidates
                : (outcome.SelectedCards.Count > 0
                    ? new List<List<Card>> { CloneCards(outcome.SelectedCards) }
                    : new List<List<Card>>());

            for (int index = 0; index < candidates.Count; index++)
            {
                var key = BuildCandidateKey(candidates[index]);
                if (string.IsNullOrWhiteSpace(key) || lookup.ContainsKey(key))
                    continue;

                lookup[key] = new CandidateMetadata
                {
                    Score = index < outcome.CandidateScores.Count ? outcome.CandidateScores[index] : null,
                    ReasonCode = index < outcome.CandidateReasonCodes.Count ? outcome.CandidateReasonCodes[index] : null,
                    Features = index < outcome.CandidateFeatures.Count
                        ? new Dictionary<string, double>(outcome.CandidateFeatures[index])
                        : new Dictionary<string, double>()
                };
            }

            return lookup;
        }

        private CandidateMetadata ResolveSelectedCandidateMetadata(
            RuleAIContext context,
            DecisionOutcome outcome,
            DecisionExplanation explanation)
        {
            var selectedKey = BuildCandidateKey(outcome.SelectedCards);
            var lookup = BuildCandidateMetadataLookup(outcome);
            if (!string.IsNullOrWhiteSpace(selectedKey) && lookup.TryGetValue(selectedKey, out var metadata))
            {
                return new CandidateMetadata
                {
                    Score = metadata.Score,
                    ReasonCode = string.IsNullOrWhiteSpace(metadata.ReasonCode) ? outcome.SelectedReason : metadata.ReasonCode,
                    Features = metadata.Features.Count > 0
                        ? new Dictionary<string, double>(metadata.Features)
                        : new Dictionary<string, double>(explanation.SelectedActionFeatures)
                };
            }

            return new CandidateMetadata
            {
                Score = outcome.CandidateScores.Count > 0 ? outcome.CandidateScores[0] : null,
                ReasonCode = outcome.SelectedReason,
                Features = new Dictionary<string, double>(explanation.SelectedActionFeatures)
            };
        }

        private List<List<Card>> LimitCandidatesForTrace(List<List<Card>> candidates, string selectedKey)
        {
            if (_ruleAIOptions.DecisionTraceMaxCandidates <= 0 || candidates.Count <= _ruleAIOptions.DecisionTraceMaxCandidates)
                return candidates.Select(CloneCards).ToList();

            var limited = candidates
                .Take(_ruleAIOptions.DecisionTraceMaxCandidates)
                .Select(CloneCards)
                .ToList();

            if (!string.IsNullOrWhiteSpace(selectedKey) &&
                limited.All(candidate => !string.Equals(BuildCandidateKey(candidate), selectedKey, StringComparison.Ordinal)))
            {
                var selected = candidates.FirstOrDefault(candidate => string.Equals(BuildCandidateKey(candidate), selectedKey, StringComparison.Ordinal));
                if (selected != null)
                {
                    limited[limited.Count - 1] = CloneCards(selected);
                    limited = DeduplicateCandidates(limited);
                }
            }

            return limited;
        }

        private static Dictionary<string, object?> BuildCompareSnapshotPayload(DecisionCompareSnapshot compareSnapshot)
        {
            return new Dictionary<string, object?>
            {
                ["shadow_compared"] = compareSnapshot.ShadowCompared,
                ["divergence"] = compareSnapshot.Divergence,
                ["old_path"] = compareSnapshot.OldPath,
                ["new_path"] = compareSnapshot.NewPath,
                ["old_action"] = BuildCardPayload(compareSnapshot.OldAction),
                ["new_action"] = BuildCardPayload(compareSnapshot.NewAction),
                ["old_reason"] = compareSnapshot.OldReason,
                ["new_reason"] = compareSnapshot.NewReason,
                ["old_intent"] = compareSnapshot.OldIntent,
                ["new_intent"] = compareSnapshot.NewIntent
            };
        }

        private Dictionary<string, object?> BuildPerfSnapshotPayload(
            DecisionOutcome outcome,
            double totalMs,
            double contextMs,
            double legacyMs,
            double shadowMs)
        {
            return new Dictionary<string, object?>
            {
                ["total_ms"] = totalMs,
                ["context_ms"] = contextMs,
                ["legacy_ms"] = legacyMs,
                ["shadow_ms"] = shadowMs,
                ["candidate_count"] = outcome.Candidates.Count,
                ["selected_score"] = ResolveSelectedScore(outcome)
            };
        }

        private Dictionary<string, object?> BuildTruthSnapshot(AIDecisionLogContext? logContext)
        {
            if (!_ruleAIOptions.DecisionTraceIncludeTruthSnapshot || logContext?.TruthSnapshot == null)
            {
                return new Dictionary<string, object?>
                {
                    ["enabled"] = false
                };
            }

            var truthSnapshot = new Dictionary<string, object?>
            {
                ["enabled"] = true
            };

            foreach (var entry in logContext.TruthSnapshot)
                truthSnapshot[entry.Key] = entry.Value;

            return truthSnapshot;
        }

        private Dictionary<string, object?> BuildAlgorithmTrace(
            RuleAIContext context,
            DecisionExplanation explanation,
            string path,
            DecisionCompareSnapshot compareSnapshot,
            int candidateCount)
        {
            return new Dictionary<string, object?>
            {
                ["policy_module"] = string.IsNullOrWhiteSpace(explanation.PhasePolicy) ? path : explanation.PhasePolicy,
                ["path"] = path,
                ["phase"] = context.Phase.ToString(),
                ["use_rule_ai_v21"] = _ruleAIOptions.UseRuleAIV21,
                ["shadow_compare_enabled"] = _ruleAIOptions.EnableShadowCompare,
                ["shadow_compared"] = compareSnapshot.ShadowCompared,
                ["shadow_sample_rate"] = _ruleAIOptions.ShadowSampleRate,
                ["decision_trace_enabled"] = _ruleAIOptions.DecisionTraceEnabled,
                ["decision_trace_include_truth_snapshot"] = _ruleAIOptions.DecisionTraceIncludeTruthSnapshot,
                ["decision_trace_max_candidates"] = _ruleAIOptions.DecisionTraceMaxCandidates,
                ["candidate_count"] = candidateCount
            };
        }

        private double ResolveSelectedScore(DecisionOutcome outcome)
        {
            var selectedKey = BuildCandidateKey(outcome.SelectedCards);
            if (!string.IsNullOrWhiteSpace(selectedKey))
            {
                var lookup = BuildCandidateMetadataLookup(outcome);
                if (lookup.TryGetValue(selectedKey, out var metadata) && metadata.Score.HasValue)
                    return metadata.Score.Value;
            }

            return outcome.CandidateScores.Count > 0 ? outcome.CandidateScores[0] : 0;
        }

        private static int ResolvePlayerIndex(RuleAIContext context, AIDecisionLogContext? logContext)
        {
            if (logContext?.PlayerIndex != null)
                return logContext.PlayerIndex.Value;

            return context.PlayerIndex;
        }

        private static string? ResolveTrickId(RuleAIContext context, AIDecisionLogContext? logContext)
        {
            if (!string.IsNullOrWhiteSpace(logContext?.TrickId))
                return logContext.TrickId;

            return context.DecisionFrame.TrickIndex > 0
                ? $"trick_{context.DecisionFrame.TrickIndex:D4}"
                : null;
        }

        private static string? ResolveTurnId(RuleAIContext context, AIDecisionLogContext? logContext)
        {
            if (!string.IsNullOrWhiteSpace(logContext?.TurnId))
                return logContext.TurnId;

            return context.DecisionFrame.TurnIndex > 0
                ? $"turn_{context.DecisionFrame.TurnIndex:D4}"
                : null;
        }

        private static JsonSerializerOptions CreateStructuredPayloadJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = SnakeCaseJsonNamingPolicy.Instance
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        private static JsonElement ToStructuredElement<T>(T value)
        {
            return JsonSerializer.SerializeToElement(value, StructuredPayloadJsonOptions);
        }

        private bool ShouldRunShadowCompare()
        {
            if (!_ruleAIOptions.EnableShadowCompare)
                return false;

            if (_ruleAIOptions.ShadowSampleRate >= 1)
                return true;

            if (_ruleAIOptions.ShadowSampleRate <= 0)
                return false;

            return _telemetryRandom.NextDouble() < _ruleAIOptions.ShadowSampleRate;
        }

        private string DetermineLeadIntent(List<Card> selectedCards)
        {
            if (selectedCards == null || selectedCards.Count == 0)
                return "TakeLead";

            return selectedCards.Count > 1 ? "PrepareThrow" : "TakeLead";
        }

        private string DetermineLeadReason(List<Card> selectedCards)
        {
            if (selectedCards == null || selectedCards.Count == 0)
                return "lead_noop";

            var pattern = new CardPattern(selectedCards, _config);
            return pattern.Type switch
            {
                PatternType.Tractor => "lead_best_tractor",
                PatternType.Pair => "lead_best_pair",
                PatternType.Mixed => "lead_best_throw",
                _ => "lead_best_single"
            };
        }

        private string DetermineFollowIntent(bool partnerWinning, bool canBeatCurrentWinner)
        {
            if (partnerWinning)
                return "PassToMate";

            return canBeatCurrentWinner ? "TakeScore" : "MinimizeLoss";
        }

        private string DetermineFollowReason(bool partnerWinning, bool canBeatCurrentWinner)
        {
            if (partnerWinning)
                return "follow_support_partner";

            return canBeatCurrentWinner ? "follow_contest_trick" : "follow_minimize_loss";
        }

        private static List<Card> CloneCards(List<Card>? cards)
        {
            return cards == null ? new List<Card>() : new List<Card>(cards);
        }

        private static List<List<Card>> CloneCandidateSets(List<List<Card>>? candidates)
        {
            if (candidates == null)
                return new List<List<Card>>();

            return candidates.Select(CloneCards).ToList();
        }

        private static List<Dictionary<string, object?>> BuildCardPayload(List<Card> cards)
        {
            return cards.Select(card => new Dictionary<string, object?>
            {
                ["suit"] = card.Suit.ToString(),
                ["rank"] = card.Rank.ToString(),
                ["score"] = card.Score,
                ["text"] = card.ToString()
            }).ToList();
        }

        private static string BuildReadableCandidateKey(List<Card> cards)
        {
            return cards == null || cards.Count == 0
                ? string.Empty
                : string.Join(" ", cards.Select(card => card.ToString()));
        }

        /// <summary>
        /// 构建兜底跟牌候选（确保合法）
        /// </summary>
        private List<Card> BuildFallbackFollowCandidate(List<Card> hand, List<Card> leadCards,
            int need, CardComparer comparer, FollowValidator validator)
        {
            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand.Where(c => MatchesLeadCategory(c, leadSuit, leadCategory)).ToList();

            // 优先同花色
            if (sameCategoryCards.Count >= need)
            {
                var structuredCandidate = FindLegalSameCategoryFollowCandidate(
                    hand,
                    leadCards,
                    sameCategoryCards,
                    need,
                    comparer,
                    validator);
                if (structuredCandidate != null)
                    return structuredCandidate;

                return sameCategoryCards.OrderBy(c => c, comparer).Take(need).ToList();
            }

            // 同花色不足，先出同花色，再补其他
            var result = new List<Card>(sameCategoryCards);
            var remaining = RemoveCards(hand, sameCategoryCards);
            result.AddRange(remaining.OrderBy(c => c, comparer).Take(need - result.Count));

            return result;
        }

        private List<Card>? FindExhaustiveLegalFollowCandidate(
            List<Card> hand,
            List<Card> leadCards,
            int need,
            CardComparer comparer,
            FollowValidator validator)
        {
            if (hand.Count < need || need <= 0)
                return null;

            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand.Where(c => MatchesLeadCategory(c, leadSuit, leadCategory)).ToList();
            if (sameCategoryCards.Count >= need)
            {
                var sameCategoryCandidate = FindLegalSameCategoryFollowCandidate(
                    hand,
                    leadCards,
                    sameCategoryCards,
                    need,
                    comparer,
                    validator);
                if (sameCategoryCandidate != null)
                    return sameCategoryCandidate;
            }

            var groupedSearch = FindLegalGroupedFollowCandidate(hand, leadCards, hand, need, comparer, validator);
            if (groupedSearch != null)
                return groupedSearch;

            var ordered = hand
                .OrderBy(card => EstimateSingleCardDiscardCost(hand, card, comparer))
                .ThenBy(card => card, comparer)
                .ToList();

            foreach (var combo in Combinations(ordered, need))
            {
                if (validator.IsValidFollow(hand, leadCards, combo))
                    return combo;
            }

            return null;
        }

        private DecisionOutcome EnsureLegalFollowOutcome(
            DecisionOutcome outcome,
            List<Card> hand,
            List<Card> leadCards,
            List<Card>? currentWinningCards,
            AIRole role,
            bool partnerWinning)
        {
            if (outcome.Phase != PhaseKind.Follow || hand.Count == 0 || leadCards.Count == 0)
                return outcome;

            var validator = new FollowValidator(_config);
            if (outcome.SelectedCards.Count == leadCards.Count &&
                validator.IsValidFollow(hand, leadCards, outcome.SelectedCards))
            {
                return outcome;
            }

            var comparer = new CardComparer(_config);
            var legalized = FindExhaustiveLegalFollowCandidate(hand, leadCards, leadCards.Count, comparer, validator);
            if (legalized == null)
                return outcome;

            var winningCards = currentWinningCards == null || currentWinningCards.Count == 0
                ? leadCards
                : currentWinningCards;

            return CreateOutcome(
                PhaseKind.Follow,
                legalized,
                new List<List<Card>> { legalized },
                DetermineFollowIntent(partnerWinning, CanBeatCards(winningCards, legalized)),
                "follow_legalized_after_invalid_selection",
                $"{outcome.Path}_legalized",
                "fallback",
                "legalized");
        }

        private List<Card>? FindLegalSameCategoryFollowCandidate(
            List<Card> hand,
            List<Card> leadCards,
            List<Card> sameCategoryCards,
            int need,
            CardComparer comparer,
            FollowValidator validator)
        {
            return FindLegalGroupedFollowCandidate(hand, leadCards, sameCategoryCards, need, comparer, validator);
        }

        private List<Card>? FindLegalGroupedFollowCandidate(
            List<Card> hand,
            List<Card> leadCards,
            List<Card> searchPool,
            int need,
            CardComparer comparer,
            FollowValidator validator)
        {
            if (searchPool.Count < need || need <= 0)
                return null;

            var groups = searchPool
                .GroupBy(card => card)
                .Select(group => new FollowCardGroup(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => EstimateSingleCardDiscardCost(searchPool, group.Card, comparer))
                .ThenBy(group => group.Card, comparer)
                .ToList();

            var remainingCounts = new int[groups.Count];
            for (int i = groups.Count - 1; i >= 0; i--)
            {
                remainingCounts[i] = groups[i].Count + (i + 1 < groups.Count ? remainingCounts[i + 1] : 0);
            }

            var candidate = new List<Card>(need);
            return SearchLegalGroupedFollowCandidate(
                hand,
                leadCards,
                validator,
                groups,
                remainingCounts,
                index: 0,
                remainingToPick: need,
                candidate);
        }

        private List<Card>? SearchLegalGroupedFollowCandidate(
            List<Card> hand,
            List<Card> leadCards,
            FollowValidator validator,
            List<FollowCardGroup> groups,
            int[] remainingCounts,
            int index,
            int remainingToPick,
            List<Card> candidate)
        {
            if (remainingToPick == 0)
            {
                return validator.IsValidFollow(hand, leadCards, candidate)
                    ? new List<Card>(candidate)
                    : null;
            }

            if (index >= groups.Count || remainingCounts[index] < remainingToPick)
                return null;

            var group = groups[index];
            int maxTake = System.Math.Min(group.Count, remainingToPick);
            for (int take = maxTake; take >= 0; take--)
            {
                for (int i = 0; i < take; i++)
                    candidate.Add(group.Card);

                var result = SearchLegalGroupedFollowCandidate(
                    hand,
                    leadCards,
                    validator,
                    groups,
                    remainingCounts,
                    index + 1,
                    remainingToPick - take,
                    candidate);
                if (result != null)
                    return result;

                for (int i = 0; i < take; i++)
                    candidate.RemoveAt(candidate.Count - 1);
            }

            return null;
        }

        /// <summary>
        /// 获取牌的分值
        /// </summary>
        private int GetCardPoints(Card card)
        {
            if (card.Rank == Rank.Five) return 5;
            if (card.Rank == Rank.Ten || card.Rank == Rank.King) return 10;
            return 0;
        }

        /// <summary>
        /// 计算赢牌余量（越小说明用最小的牌赢）
        /// </summary>
        private int CalculateWinMargin(List<Card> candidate, List<Card> currentWinning, CardComparer comparer)
        {
            int margin = 0;
            for (int i = 0; i < candidate.Count && i < currentWinning.Count; i++)
            {
                int cmp = comparer.Compare(candidate[i], currentWinning[i]);
                if (cmp > 0)
                    margin += (GetCardValue(candidate[i]) - GetCardValue(currentWinning[i]));
            }
            return margin;
        }

        /// <summary>
        /// 扣底（坐庄玩家专属）
        /// 输入：手牌（推荐33张：25张手牌 + 8张底牌，但也支持其他数量）
        /// 输出：8张要扣底的牌
        /// </summary>
        public List<Card> BuryBottom(
            List<Card> hand,
            AIRole role = AIRole.Dealer,
            List<Card>? visibleBottomCards = null,
            AIDecisionLogContext? logContext = null,
            int defenderScore = 0,
            int cardsLeftMin = -1)
        {
            var safeHand = hand ?? new List<Card>();
            var totalStopwatch = Stopwatch.StartNew();

            var contextStopwatch = Stopwatch.StartNew();
            var context = _contextBuilder.BuildBuryContext(
                safeHand,
                role,
                playerIndex: logContext?.PlayerIndex ?? -1,
                dealerIndex: logContext?.DealerIndex ?? -1,
                visibleBottomCards: visibleBottomCards,
                defenderScore: defenderScore,
                cardsLeftMin: cardsLeftMin);
            contextStopwatch.Stop();

            var legacyStopwatch = Stopwatch.StartNew();
            var legacyOutcome = BuryBottomOldPath(safeHand);
            legacyStopwatch.Stop();

            var shadowStopwatch = Stopwatch.StartNew();
            var shouldCompare = _ruleAIOptions.EnableShadowCompare && ShouldRunShadowCompare();
            DecisionOutcome? newOutcome = null;
            if (_ruleAIOptions.UseRuleAIV21 || shouldCompare)
            {
                var decision = _buryPolicy2.Decide(context);
                newOutcome = decision.SelectedCards.Count == 8
                    ? CreatePolicyOutcome("rule_ai_v21_bury_policy2", decision, "rule_ai_v21", "bury")
                    : BuildPassthroughOutcome(legacyOutcome, "rule_ai_v21_bury_fallback_to_legacy");
            }

            var decisionTraceId = ResolveDecisionTraceId(context, logContext);
            var compareSnapshot = BuildCompareSnapshot(legacyOutcome, newOutcome, shouldCompare);

            if (compareSnapshot.ShadowCompared)
                LogAiCompare(context, compareSnapshot, logContext, decisionTraceId);

            shadowStopwatch.Stop();

            var selectedOutcome = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome
                : legacyOutcome;
            var selectedPath = _ruleAIOptions.UseRuleAIV21 && newOutcome != null
                ? newOutcome.Path
                : legacyOutcome.Path;

            totalStopwatch.Stop();

            LogAiDecision(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId);

            LogAiPerf(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                shouldCompare,
                logContext,
                decisionTraceId);

            LogAiBundle(
                context,
                selectedOutcome,
                selectedPath,
                totalStopwatch.Elapsed.TotalMilliseconds,
                contextStopwatch.Elapsed.TotalMilliseconds,
                legacyStopwatch.Elapsed.TotalMilliseconds,
                shadowStopwatch.Elapsed.TotalMilliseconds,
                logContext,
                decisionTraceId,
                compareSnapshot);

            return new List<Card>(selectedOutcome.SelectedCards);
        }

        private DecisionOutcome BuryBottomOldPath(List<Card> hand)
        {
            if (hand == null || hand.Count < 8)
            {
                return CreateOutcome(
                    PhaseKind.BuryBottom,
                    new List<Card>(),
                    new List<List<Card>>(),
                    "ProtectBottom",
                    "bury_empty_hand",
                    "legacy");
            }

            var comparer = new CardComparer(_config);
            if (hand.Count == 33)
            {
                var cardScores = hand.Select(c => new
                {
                    Card = c,
                    Score = EvaluateCardForBurying(c, hand, comparer)
                }).OrderBy(x => x.Score).ToList();

                var selected = cardScores.Take(8).Select(x => x.Card).ToList();
                ProtectPointCardsInBurySelection(selected, cardScores.Select(x => x.Card).ToList());
                return CreateOutcome(
                    PhaseKind.BuryBottom,
                    selected,
                    new List<List<Card>> { selected },
                    "ProtectBottom",
                    "bury_ranked_selection",
                    "legacy");
            }

            var fallback = hand.OrderBy(c => c, comparer).Take(8).ToList();
            return CreateOutcome(
                PhaseKind.BuryBottom,
                fallback,
                new List<List<Card>> { fallback },
                "ProtectBottom",
                "bury_small_cards_fallback",
                "legacy");
        }

        /// <summary>
        /// 评估牌的埋底价值（分数越低越适合埋）
        /// </summary>
        private int EvaluateCardForBurying(Card card, List<Card> hand, CardComparer comparer)
        {
            int score = 0;
            var protectionWeight = _strategy.PointCardProtectionWeight;
            var denyWeight = _strategy.OpponentDenyPointBias;

            // 1. 分牌惩罚（最不想埋）
            int points = GetCardPoints(card);
            if (points > 0)
                score += (int)System.Math.Round(1000 * protectionWeight + points * 100 * denyWeight);

            // 2. 主牌惩罚
            if (_config.IsTrump(card))
                score += 500;

            // 3. 对子惩罚（对子比单张更不想埋）
            int sameCount = hand.Count(c => c.Suit == card.Suit && c.Rank == card.Rank);
            if (sameCount >= 2)
                score += 200;

            // 4. 花色长度考虑（短门优先埋）
            var suitCards = hand.Where(c => !_config.IsTrump(c) && c.Suit == card.Suit).ToList();
            if (suitCards.Count <= 3 && suitCards.Count > 0)
                score -= 100; // 短门优先

            // 5. 牌力考虑（小牌优先埋）
            score += GetCardValue(card) / 10;

            return score;
        }

        /// <summary>
        /// 在满足8张扣底前提下尽量避免埋分牌，优先替换为非分牌。
        /// </summary>
        private void ProtectPointCardsInBurySelection(List<Card> selected, List<Card> orderedCards)
        {
            if (selected.Count != 8)
                return;

            var selectedPointCards = selected.Where(c => GetCardPoints(c) > 0).ToList();
            if (selectedPointCards.Count == 0)
                return;

            var replacements = orderedCards
                .Where(c => GetCardPoints(c) == 0 && !selected.Contains(c))
                .ToList();

            foreach (var pointCard in selectedPointCards)
            {
                if (replacements.Count == 0)
                    break;

                var replacement = replacements[0];
                replacements.RemoveAt(0);
                selected.Remove(pointCard);
                selected.Add(replacement);
            }
        }

        private List<List<Card>> BuildLeadCandidates(List<Card> hand, CardComparer comparer, AIRole role,
            int myPosition, List<int>? opponentPositions, List<Card>? knownBottomCards)
        {
            var groups = new List<List<Card>>();

            var trumpCards = hand.Where(_config.IsTrump).ToList();
            if (trumpCards.Count > 0)
                groups.Add(trumpCards);

            foreach (var suitGroup in hand.Where(c => !_config.IsTrump(c)).GroupBy(c => c.Suit))
                groups.Add(suitGroup.ToList());

            var candidates = new List<List<Card>>();

            foreach (var group in groups)
            {
                var sorted = group.OrderByDescending(c => c, comparer).ToList();

                bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

                // 评估甩牌（多张）
                if (sorted.Count >= 3)
                {
                    // 使用记牌系统评估甩牌成功率
                    bool canThrow = CanSafelyThrow(sorted, hand, myPosition, opponentPositions, knownBottomCards);
                    var dealerThrowAggressiveness = Math.Min(1.0, _strategy.LeadThrowAggressiveness + 0.15);

                    if (canThrow)
                    {
                        // 甩牌成功率高，可以尝试
                        candidates.Add(sorted);
                    }
                    else if (isDealerSide && sorted.Count >= 4)
                    {
                        // 保守策略：仅在4张子甩也安全时，才允许小概率尝试。
                        var cautiousThrow = sorted.Take(4).ToList();
                        bool canCautiousThrow = CanSafelyThrow(cautiousThrow, hand, myPosition, opponentPositions, knownBottomCards);
                        if (canCautiousThrow && _random.NextDouble() < dealerThrowAggressiveness * 0.25)
                        {
                            candidates.Add(cautiousThrow);
                        }
                    }
                }

                var pair = FindStrongestPair(sorted, comparer);
                if (pair != null)
                    candidates.Add(pair);

                // 尝试不同长度拖拉机，优先更长拖拉机
                for (int len = sorted.Count - (sorted.Count % 2); len >= 4; len -= 2)
                {
                    var tractor = FindStrongestTractor(sorted, len, comparer);
                    if (tractor != null)
                    {
                        candidates.Add(tractor);
                        break;
                    }
                }

                // 单张兜底
                candidates.Add(new List<Card> { sorted[0] });
            }

            return DeduplicateCandidates(candidates);
        }

        /// <summary>
        /// 判断是否可以安全甩牌
        /// </summary>
        private bool CanSafelyThrow(List<Card> throwCards, List<Card> hand,
            int myPosition, List<int>? opponentPositions, List<Card>? knownBottomCards)
        {
            // 简单难度：不评估，随机决定
            if (_difficulty == AIDifficulty.Easy)
                return _random.NextDouble() > 0.5;

            // 没有对手信息，保守处理
            if (opponentPositions == null || opponentPositions.Count == 0)
                return throwCards.Count <= 2; // 只允许甩2张以下

            var others = opponentPositions
                .Where(pos => pos != myPosition)
                .Distinct()
                .ToList();
            if (others.Count == 0)
                return throwCards.Count <= 2;

            // 先做保守硬判定，再看概率阈值
            var safety = _memory.EvaluateThrowSafety(
                throwCards, hand, myPosition, others, knownBottomCards);
            if (!safety.IsDeterministicallySafe)
                return false;

            // 根据难度设置阈值
            double threshold = _difficulty switch
            {
                AIDifficulty.Medium => 0.6,  // 60%成功率
                AIDifficulty.Hard => 0.7,    // 70%成功率
                AIDifficulty.Expert => 0.8,  // 80%成功率
                _ => 0.5
            };

            return safety.SuccessProbability >= threshold;
        }

        private List<List<Card>> BuildSameCategoryFollowCandidates(
            List<Card> sameCategoryCards,
            List<Card> leadCards,
            int need,
            CardComparer comparer,
            AIRole role,
            bool partnerWinning)
        {
            var candidates = new List<List<Card>>();
            var sorted = sameCategoryCards.OrderByDescending(c => c, comparer).ToList();
            var leadPattern = new CardPattern(leadCards, _config);

            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            if (leadPattern.Type == PatternType.Tractor && need >= 4 && need % 2 == 0)
            {
                var tractor = FindStrongestTractor(sorted, need, comparer);
                if (tractor != null)
                    candidates.Add(tractor);
            }

            if (leadPattern.Type == PatternType.Pair && need == 2)
            {
                var pair = FindStrongestPair(sorted, comparer);
                if (pair != null)
                    candidates.Add(pair);
            }

            // 根据角色和局势选择策略
            if (partnerWinning)
            {
                // 对家赢牌：出小牌保留实力，或送分牌
                candidates.Add(sorted.OrderBy(c => c, comparer).Take(need).ToList());

                // 如果有分牌，优先送分牌
                var pointCards = sorted.Where(c => GetCardPoints(c) > 0)
                    .OrderByDescending(c => GetCardPoints(c))
                    .ToList();
                if (pointCards.Count >= need)
                    candidates.Add(pointCards.Take(need).ToList());
            }
            else
            {
                // 对手赢牌：尝试用大牌争胜
                candidates.Add(sorted.Take(need).ToList());

                // 如果无法赢，出小牌保留
                candidates.Add(sorted.OrderBy(c => c, comparer).Take(need).ToList());
            }

            return DeduplicateCandidates(candidates);
        }

        private List<Card> SelectBestLeadCandidate(List<List<Card>> candidates, CardComparer comparer, AIRole role)
        {
            // 随机决策
            if (ShouldUseRandomDecision() && candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];

            var best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareLeadCandidates(candidates[i], best, comparer, role) > 0)
                    best = candidates[i];
            }
            return best;
        }

        private int CompareLeadCandidates(List<Card> a, List<Card> b, CardComparer comparer, AIRole role)
        {
            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            // 庄家：优先控制（拖拉机/对子），闲家：优先多张（甩牌）
            if (!isDealerSide)
            {
                // 闲家优先多张
                if (a.Count != b.Count)
                    return a.Count.CompareTo(b.Count);
            }

            int pa = GetPatternPriority(new CardPattern(a, _config).Type);
            int pb = GetPatternPriority(new CardPattern(b, _config).Type);
            if (pa != pb)
                return pa.CompareTo(pb);

            if (isDealerSide)
            {
                // 庄家优先多张（在牌型相同的情况下）
                if (a.Count != b.Count)
                    return a.Count.CompareTo(b.Count);
            }

            int pairA = CountPairs(a);
            int pairB = CountPairs(b);
            if (pairA != pairB)
                return pairA.CompareTo(pairB);

            int cmp = CompareCardSets(a, b, comparer);
            if (cmp != 0)
                return cmp;

            return _random.Next(2) == 0 ? -1 : 1;
        }

        private List<Card> SelectBestFollowCandidate(List<List<Card>> candidates, List<Card> currentWinningCards,
            CardComparer comparer, AIRole role, bool partnerWinning)
        {
            // 跟牌阶段仅在简单难度允许随机，避免中高难度出现"明知不能赢却垫大牌"的噪声决策
            if (_difficulty == AIDifficulty.Easy && ShouldUseRandomDecision() && candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];

            var best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareFollowCandidates(candidates[i], best, currentWinningCards, comparer, role, partnerWinning) > 0)
                    best = candidates[i];
            }
            return best;
        }

        private int CompareFollowCandidates(List<Card> a, List<Card> b, List<Card> currentWinningCards,
            CardComparer comparer, AIRole role, bool partnerWinning)
        {
            // [P1修复] 与当前赢牌比较，而不是首引牌
            bool beatA = CanBeatCards(currentWinningCards, a);
            bool beatB = CanBeatCards(currentWinningCards, b);

            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            // 对家赢牌时的策略
            if (partnerWinning)
            {
                // 1. 优先送分牌（使用细粒度参数）
                int pointsA = a.Sum(c => GetCardPoints(c));
                int pointsB = b.Sum(c => GetCardPoints(c));
                if (pointsA != pointsB)
                {
                    var givePriority = _strategy.PartnerWinning_GivePointsPriority;
                    if (givePriority >= 0.5)
                        return pointsA.CompareTo(pointsB); // 送分优先
                }

                // 2. 其次垫小牌（保留大牌）
                var discardSmallPriority = _strategy.PartnerWinning_DiscardSmallPriority;
                if (discardSmallPriority >= 0.5)
                {
                    int minValueA = a.Min(c => GetCardValue(c));
                    int minValueB = b.Min(c => GetCardValue(c));
                    if (minValueA != minValueB)
                        return minValueB.CompareTo(minValueA); // 小牌优先
                }

                // 3. 避免垫主牌
                var avoidTrumpPriority = _strategy.PartnerWinning_AvoidTrumpPriority;
                if (avoidTrumpPriority >= 0.5)
                {
                    int trumpCountA = a.Count(_config.IsTrump);
                    int trumpCountB = b.Count(_config.IsTrump);
                    if (trumpCountA != trumpCountB)
                        return trumpCountB.CompareTo(trumpCountA); // 少垫主牌
                }

                // 4. 保留对子
                var keepPairsPriority = _strategy.PartnerWinning_KeepPairsPriority;
                if (keepPairsPriority >= 0.5)
                {
                    int pairCountA = CountPairs(a);
                    int pairCountB = CountPairs(b);
                    if (pairCountA != pairCountB)
                        return pairCountB.CompareTo(pairCountA); // 少垫对子
                }
            }
            else
            {
                // 对手赢牌时：优先争胜
                if (beatA != beatB)
                {
                    var beatBias = ResolveRoleWeighted01(_strategy.FollowBeatAttemptBias, role, neutral: 0.5);
                    if (beatBias >= 0.5)
                    {
                        // 如果都能赢，选择"用最小牌赢"（保留大牌控制下轮）
                        if (beatA && beatB)
                        {
                            var useMinimalPriority = _strategy.WinAttempt_UseMinimalCardsPriority;
                            if (useMinimalPriority >= 0.5)
                            {
                                // 比较"赢牌余量"（越小越好）
                                int marginA = CalculateWinMargin(a, currentWinningCards, comparer);
                                int marginB = CalculateWinMargin(b, currentWinningCards, comparer);
                                if (marginA != marginB)
                                    return marginA.CompareTo(marginB); // 余量小的优先
                            }
                        }
                        return beatA ? 1 : -1;
                    }

                    return beatA ? -1 : 1;
                }

                // 无法赢时：优先垫小牌和非分牌
                if (!beatA && !beatB)
                {
                    // 1. 优先垫非分牌
                    var avoidPointsPriority = _strategy.CannotWin_AvoidPointsPriority;
                    if (avoidPointsPriority >= 0.5)
                    {
                        int pointsA = a.Sum(c => GetCardPoints(c));
                        int pointsB = b.Sum(c => GetCardPoints(c));
                        if (pointsA != pointsB)
                            return pointsB.CompareTo(pointsA); // 少送分优先
                    }

                    // 2. 其次垫小牌
                    var discardSmallPriority = _strategy.CannotWin_DiscardSmallPriority;
                    if (discardSmallPriority >= 0.5)
                    {
                        int avgValueA = (int)a.Average(c => GetCardValue(c));
                        int avgValueB = (int)b.Average(c => GetCardValue(c));
                        if (avgValueA != avgValueB)
                            return avgValueB.CompareTo(avgValueA); // 小牌优先
                    }

                    // 3. 避免垫主牌
                    var avoidTrumpPriority = _strategy.CannotWin_AvoidTrumpPriority;
                    if (avoidTrumpPriority >= 0.5)
                    {
                        int trumpCountA = a.Count(_config.IsTrump);
                        int trumpCountB = b.Count(_config.IsTrump);
                        if (trumpCountA != trumpCountB)
                            return trumpCountB.CompareTo(trumpCountA); // 少垫主牌
                    }

                    // 4. 庄家额外保守
                    if (isDealerSide)
                    {
                        int cmpSmall = CompareCardSets(b, a, comparer);
                        if (cmpSmall != 0)
                            return cmpSmall;
                    }
                }
            }

            // 收官阶段优先稳健：在牌力接近时尽量保留高牌和主牌控制力。
            var endgameStability = ResolveRoleWeighted01(_strategy.EndgameStabilityBias, role, neutral: 0.6);
            if (endgameStability > 0.5)
            {
                var volatilityA = EstimateVolatility(a);
                var volatilityB = EstimateVolatility(b);
                if (volatilityA != volatilityB)
                    return volatilityB.CompareTo(volatilityA);
            }

            var endgameFinish = ResolveRoleWeighted01(_strategy.EndgameFinishBias, role, neutral: 0.6);
            if (endgameFinish > 0.6 && beatA && beatB)
            {
                var pointsA = a.Sum(GetCardPoints);
                var pointsB = b.Sum(GetCardPoints);
                if (pointsA != pointsB)
                    return pointsA.CompareTo(pointsB);
            }

            int pa = GetPatternPriority(new CardPattern(a, _config).Type);
            int pb = GetPatternPriority(new CardPattern(b, _config).Type);
            if (pa != pb)
                return pa.CompareTo(pb);

            int pairA = CountPairs(a);
            int pairB = CountPairs(b);
            if (pairA != pairB)
                return pairA.CompareTo(pairB);

            int cmp = CompareCardSets(a, b, comparer);
            if (cmp != 0)
                return cmp;

            return _random.Next(2) == 0 ? -1 : 1;
        }

        /// <summary>
        /// 判断followCards能否赢过currentWinningCards
        /// </summary>
        private bool CanBeatCards(List<Card> currentWinningCards, List<Card> followCards)
        {
            if (currentWinningCards.Count != followCards.Count)
                return false;

            var judge = new TrickJudge(_config);
            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, currentWinningCards),
                new TrickPlay(1, followCards)
            };

            return judge.DetermineWinner(plays) == 1;
        }

        private bool CanBeatLead(List<Card> leadCards, List<Card> followCards)
        {
            // 保留此方法以兼容旧代码，内部调用新方法
            return CanBeatCards(leadCards, followCards);
        }

        private List<Card>? FindStrongestPair(List<Card> cards, CardComparer comparer)
        {
            var groups = cards.GroupBy(c => (c.Suit, c.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => g.Take(2).ToList())
                .ToList();

            if (groups.Count == 0)
                return null;

            var best = groups[0];
            for (int i = 1; i < groups.Count; i++)
            {
                if (comparer.Compare(groups[i][0], best[0]) > 0)
                    best = groups[i];
            }
            return best;
        }

        /// <summary>
        /// 查找最强拖拉机（优化版，避免组合爆炸）
        /// [P2修复] 使用贪心算法代替组合枚举，降低复杂度
        /// </summary>
        private List<Card>? FindStrongestTractor(List<Card> cards, int neededCount, CardComparer comparer)
        {
            if (neededCount < 4 || neededCount % 2 != 0)
                return null;

            int pairCount = neededCount / 2;

            // 找出所有对子
            var pairUnits = cards.GroupBy(c => (c.Suit, c.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => g.Take(2).ToList())
                .OrderByDescending(p => p[0], comparer)
                .ToList();

            if (pairUnits.Count < pairCount)
                return null;

            // [P2优化] 使用贪心算法：从最大的对子开始，尝试构建连续拖拉机
            // 这样避免了组合枚举，复杂度从O(C(n,k))降到O(n)
            for (int startIdx = 0; startIdx <= pairUnits.Count - pairCount; startIdx++)
            {
                var candidate = new List<Card>();
                for (int i = 0; i < pairCount; i++)
                {
                    candidate.AddRange(pairUnits[startIdx + i]);
                }

                var pattern = new CardPattern(candidate, _config);
                if (pattern.IsTractor(candidate))
                    return candidate;
            }

            // 如果没有找到连续拖拉机，且对子数量不多（<=10），尝试组合搜索
            // 这是为了处理断档拖的情况
            if (pairUnits.Count <= 10)
            {
                foreach (var combo in Combinations(pairUnits, pairCount))
                {
                    var candidate = combo.SelectMany(x => x).ToList();
                    var pattern = new CardPattern(candidate, _config);
                    if (pattern.IsTractor(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private IEnumerable<List<T>> Combinations<T>(List<T> items, int choose)
        {
            var buffer = new List<T>();
            foreach (var combo in CombinationsCore(items, choose, 0, buffer))
                yield return combo;
        }

        private IEnumerable<List<T>> CombinationsCore<T>(List<T> items, int choose, int start, List<T> buffer)
        {
            if (buffer.Count == choose)
            {
                yield return new List<T>(buffer);
                yield break;
            }

            int needed = choose - buffer.Count;
            for (int i = start; i <= items.Count - needed; i++)
            {
                buffer.Add(items[i]);
                foreach (var combo in CombinationsCore(items, choose, i + 1, buffer))
                    yield return combo;
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        private int CompareCardSets(List<Card> a, List<Card> b, CardComparer comparer)
        {
            var sa = a.OrderByDescending(c => GetCardValue(c)).ToList();
            var sb = b.OrderByDescending(c => GetCardValue(c)).ToList();

            int n = System.Math.Min(sa.Count, sb.Count);
            for (int i = 0; i < n; i++)
            {
                int cmp = comparer.Compare(sa[i], sb[i]);
                if (cmp != 0)
                    return cmp;

                int va = GetCardValue(sa[i]);
                int vb = GetCardValue(sb[i]);
                if (va != vb)
                    return va.CompareTo(vb);
            }

            return sa.Count.CompareTo(sb.Count);
        }

        private int GetCardValue(Card card)
        {
            if (card.Rank == Rank.BigJoker) return 1000;
            if (card.Rank == Rank.SmallJoker) return 900;

            bool isLevel = card.Rank == _config.LevelRank;
            bool isTrumpSuit = _config.TrumpSuit.HasValue && card.Suit == _config.TrumpSuit.Value;

            if (isLevel && isTrumpSuit) return 800;
            if (isLevel) return 700;
            if (_config.IsTrump(card)) return 600 + (int)card.Rank;
            return 100 + (int)card.Rank;
        }

        private int CountPairs(List<Card> cards)
        {
            return cards.GroupBy(c => (c.Suit, c.Rank)).Sum(g => g.Count() / 2);
        }

        private int GetPatternPriority(PatternType type)
        {
            return type switch
            {
                PatternType.Tractor => 3,
                PatternType.Pair => 2,
                PatternType.Mixed => 1,
                PatternType.Single => 0,
                _ => 0
            };
        }

        private bool MatchesLeadCategory(Card card, Suit leadSuit, CardCategory leadCategory)
        {
            if (leadCategory == CardCategory.Trump)
                return _config.IsTrump(card);

            return !_config.IsTrump(card) && card.Suit == leadSuit;
        }

        private List<Card> RemoveCards(List<Card> source, List<Card> toRemove)
        {
            var copy = new List<Card>(source);
            foreach (var card in toRemove)
            {
                var found = copy.FirstOrDefault(c => c.Equals(card));
                if (found != null)
                    copy.Remove(found);
            }
            return copy;
        }

        private List<List<Card>> DeduplicateCandidates(List<List<Card>> candidates)
        {
            var deduped = new List<List<Card>>();
            var seen = new HashSet<string>();

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.Count == 0)
                    continue;

                string key = BuildCandidateKey(candidate);
                if (seen.Add(key))
                    deduped.Add(candidate);
            }

            return deduped;
        }

        private string BuildCandidateKey(List<Card> cards)
        {
            return string.Join(",", cards
                .OrderBy(c => (int)c.Suit)
                .ThenBy(c => (int)c.Rank)
                .Select(c => $"{(int)c.Suit}-{(int)c.Rank}"));
        }

        private static double ResolveRoleWeighted01(double value, AIRole role, double neutral)
        {
            var clamped = System.Math.Clamp(value, 0, 1);
            if (role == AIRole.Opponent)
                return clamped;

            // 非防守侧采用保守插值，避免参数过拟合影响庄家链路。
            return neutral + (clamped - neutral) * 0.4;
        }

        private int EstimateVolatility(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return 0;

            var trumpWeight = cards.Count(_config.IsTrump) * 12;
            var highCardWeight = cards.Sum(GetCardValue) / 60;
            var pointRisk = cards.Sum(GetCardPoints) * 8;
            return trumpWeight + highCardWeight + pointRisk;
        }

        private sealed class DecisionOutcome
        {
            public PhaseKind Phase { get; init; } = PhaseKind.Unknown;

            public List<Card> SelectedCards { get; init; } = new();

            public List<List<Card>> Candidates { get; init; } = new();

            public string PrimaryIntent { get; init; } = "Unknown";

            public string SelectedReason { get; init; } = "unspecified";

            public string Path { get; init; } = "legacy";

            public List<double> CandidateScores { get; init; } = new();

            public List<string?> CandidateReasonCodes { get; init; } = new();

            public List<Dictionary<string, double>> CandidateFeatures { get; init; } = new();

            public List<string> Tags { get; init; } = new();

            public DecisionExplanation? Explanation { get; init; }
        }

        private sealed class DecisionCompareSnapshot
        {
            public bool ShadowCompared { get; init; }

            public bool Divergence { get; init; }

            public string? OldPath { get; init; }

            public string? NewPath { get; init; }

            public List<Card> OldAction { get; init; } = new();

            public List<Card> NewAction { get; init; } = new();

            public string? OldReason { get; init; }

            public string? NewReason { get; init; }

            public string? OldIntent { get; init; }

            public string? NewIntent { get; init; }

            public int OldCandidateCount { get; init; }

            public int NewCandidateCount { get; init; }
        }

        private sealed class CandidateMetadata
        {
            public double? Score { get; init; }

            public string? ReasonCode { get; init; }

            public Dictionary<string, double> Features { get; init; } = new();
        }

        private sealed class CandidateEvaluation
        {
            public List<Card> Cards { get; init; } = new();

            public double Score { get; init; }

            public string Reason { get; init; } = string.Empty;
        }

        private sealed class FollowCardGroup
        {
            public FollowCardGroup(Card card, int count)
            {
                Card = card;
                Count = count;
            }

            public Card Card { get; }

            public int Count { get; }
        }

        private sealed class SnakeCaseJsonNamingPolicy : JsonNamingPolicy
        {
            public static readonly SnakeCaseJsonNamingPolicy Instance = new();

            public override string ConvertName(string name)
            {
                if (string.IsNullOrEmpty(name))
                    return name;

                var buffer = new System.Text.StringBuilder(name.Length + 8);
                for (int index = 0; index < name.Length; index++)
                {
                    var ch = name[index];
                    if (char.IsUpper(ch))
                    {
                        if (index > 0)
                            buffer.Append('_');

                        buffer.Append(char.ToLowerInvariant(ch));
                    }
                    else
                    {
                        buffer.Append(ch);
                    }
                }

                return buffer.ToString();
            }
        }
    }
}
