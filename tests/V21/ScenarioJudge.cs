using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Tests.V21
{
    /// <summary>
    /// 场景期望：描述一个牌局快照和对AI决策的判断标准。
    /// </summary>
    public sealed class GameScenario
    {
        /// <summary>场景名称，用于测试输出识别</summary>
        public string Name { get; init; } = string.Empty;

        /// <summary>场景描述（技巧说明）</summary>
        public string Description { get; init; } = string.Empty;

        /// <summary>游戏配置</summary>
        public GameConfig Config { get; init; } = new();

        /// <summary>AI角色</summary>
        public AIRole Role { get; init; } = AIRole.Opponent;

        /// <summary>AI难度</summary>
        public AIDifficulty Difficulty { get; init; } = AIDifficulty.Hard;

        /// <summary>当前玩家索引</summary>
        public int PlayerIndex { get; init; } = -1;

        /// <summary>庄家索引</summary>
        public int DealerIndex { get; init; } = -1;

        /// <summary>手牌</summary>
        public List<Card> Hand { get; init; } = new();

        /// <summary>出牌阶段</summary>
        public ScenarioPhase Phase { get; init; } = ScenarioPhase.Lead;

        /// <summary>跟牌时的首出牌（Follow阶段用）</summary>
        public List<Card> LeadCards { get; init; } = new();

        /// <summary>当前赢牌（Follow阶段用）</summary>
        public List<Card> CurrentWinningCards { get; init; } = new();

        /// <summary>队友是否正在赢（Follow阶段用）</summary>
        public bool PartnerWinning { get; init; }

        /// <summary>当前墩分（Follow阶段用）</summary>
        public int TrickScore { get; init; }

        /// <summary>当前墩序号</summary>
        public int TrickIndex { get; init; }

        /// <summary>当前回合序号</summary>
        public int TurnIndex { get; init; }

        /// <summary>当前出牌位置（第几手）</summary>
        public int PlayPosition { get; init; } = 1;

        /// <summary>当前领先玩家</summary>
        public int CurrentWinningPlayer { get; init; } = -1;

        /// <summary>闲家方当前得分</summary>
        public int DefenderScore { get; init; }

        /// <summary>底牌总分</summary>
        public int BottomPoints { get; init; }

        /// <summary>标记为已知AI缺陷（M2待修），汇总时单独统计，不计入通过率</summary>
        public bool IsKnownDefect { get; init; }

        /// <summary>剩余最少手牌数（用于终局检测，-1表示未知）</summary>
        public int CardsLeftMin { get; init; } = -1;

        /// <summary>已知底牌（仅庄家可见）</summary>
        public List<Card> VisibleBottomCards { get; init; } = new();

        /// <summary>判断标准列表（全部满足才算通过）</summary>
        public List<IScenarioExpectation> Expectations { get; init; } = new();
    }

    public enum ScenarioPhase { Lead, Follow, Bury }

    /// <summary>判断标准接口</summary>
    public interface IScenarioExpectation
    {
        string Description { get; }
        bool Check(List<Card> selected, PhaseDecision decision);
    }

    /// <summary>期望选出指定张数的牌</summary>
    public sealed class ExpectCount : IScenarioExpectation
    {
        private readonly int _count;
        public ExpectCount(int count) => _count = count;
        public string Description => $"选出 {_count} 张牌";
        public bool Check(List<Card> selected, PhaseDecision decision) => selected.Count == _count;
    }

    /// <summary>期望选出的牌包含指定花色</summary>
    public sealed class ExpectSuit : IScenarioExpectation
    {
        private readonly Suit _suit;
        private readonly bool _all;
        public ExpectSuit(Suit suit, bool all = true) { _suit = suit; _all = all; }
        public string Description => _all ? $"全部为 {_suit}" : $"包含 {_suit}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            _all ? selected.All(c => c.Suit == _suit) : selected.Any(c => c.Suit == _suit);
    }

    /// <summary>期望选出的牌不包含指定花色（避免浪费主牌等）</summary>
    public sealed class ExpectNotSuit : IScenarioExpectation
    {
        private readonly Suit _suit;
        public ExpectNotSuit(Suit suit) => _suit = suit;
        public string Description => $"不出 {_suit}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.All(c => c.Suit != _suit);
    }

    /// <summary>期望选出的牌不包含Joker</summary>
    public sealed class ExpectNoJoker : IScenarioExpectation
    {
        public string Description => "不出王牌";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.All(c => !c.IsJoker);
    }

    /// <summary>期望主意图匹配</summary>
    public sealed class ExpectIntent : IScenarioExpectation
    {
        private readonly DecisionIntentKind _intent;
        public ExpectIntent(DecisionIntentKind intent) => _intent = intent;
        public string Description => $"主意图为 {_intent}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            decision.Intent.PrimaryIntent == _intent;
    }

    /// <summary>期望选出的牌包含指定点数</summary>
    public sealed class ExpectRank : IScenarioExpectation
    {
        private readonly Rank _rank;
        private readonly bool _all;
        public ExpectRank(Rank rank, bool all = false) { _rank = rank; _all = all; }
        public string Description => _all ? $"全部为 {_rank}" : $"包含 {_rank}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            _all ? selected.All(c => c.Rank == _rank) : selected.Any(c => c.Rank == _rank);
    }

    /// <summary>期望选出的牌不包含指定点数</summary>
    public sealed class ExpectNotRank : IScenarioExpectation
    {
        private readonly Rank _rank;
        public ExpectNotRank(Rank rank) => _rank = rank;
        public string Description => $"不出 {_rank}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.All(c => c.Rank != _rank);
    }

    /// <summary>期望选出的牌为拖拉机（连对）</summary>
    public sealed class ExpectTractor : IScenarioExpectation
    {
        private readonly GameConfig _config;
        public ExpectTractor(GameConfig config) => _config = config;
        public string Description => "选出拖拉机";
        public bool Check(List<Card> selected, PhaseDecision decision)
        {
            if (selected.Count < 4 || selected.Count % 2 != 0) return false;
            var pattern = new CardPattern(selected, _config);
            return pattern.IsTractor(selected);
        }
    }

    /// <summary>期望选出的牌总分不超过阈值（保留高分牌）</summary>
    public sealed class ExpectMaxScore : IScenarioExpectation
    {
        private readonly int _maxScore;
        public ExpectMaxScore(int maxScore) => _maxScore = maxScore;
        public string Description => $"出牌总分 <= {_maxScore}";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.Sum(c => c.Score) <= _maxScore;
    }

    /// <summary>期望选出的牌全部为主牌</summary>
    public sealed class ExpectAllTrump : IScenarioExpectation
    {
        private readonly GameConfig _config;
        public ExpectAllTrump(GameConfig config) => _config = config;
        public string Description => "全部为主牌";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.Count > 0 && selected.All(_config.IsTrump);
    }

    /// <summary>期望选出的牌可以压过当前赢家</summary>
    public sealed class ExpectBeatCurrentWinner : IScenarioExpectation
    {
        private readonly GameConfig _config;
        private readonly List<Card> _currentWinningCards;

        public ExpectBeatCurrentWinner(GameConfig config, IEnumerable<Card> currentWinningCards)
        {
            _config = config;
            _currentWinningCards = currentWinningCards.ToList();
        }

        public string Description => "能压过当前赢家";

        public bool Check(List<Card> selected, PhaseDecision decision) =>
            selected.Count == _currentWinningCards.Count &&
            RuleAIUtility.CanBeatCards(_config, _currentWinningCards, selected);
    }

    /// <summary>期望选出的牌恰好匹配指定集合</summary>
    public sealed class ExpectExactCards : IScenarioExpectation
    {
        private readonly string _expectedKey;
        public ExpectExactCards(IEnumerable<Card> expectedCards) =>
            _expectedKey = RuleAIUtility.BuildCandidateKey(expectedCards);
        public string Description => "命中指定出牌集合";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            RuleAIUtility.BuildCandidateKey(selected) == _expectedKey;
    }

    /// <summary>期望选出的牌不等于某个已知错误集合</summary>
    public sealed class ExpectNotExactCards : IScenarioExpectation
    {
        private readonly string _forbiddenKey;
        public ExpectNotExactCards(IEnumerable<Card> forbiddenCards) =>
            _forbiddenKey = RuleAIUtility.BuildCandidateKey(forbiddenCards);
        public string Description => "不命中已知错误出牌集合";
        public bool Check(List<Card> selected, PhaseDecision decision) =>
            RuleAIUtility.BuildCandidateKey(selected) != _forbiddenKey;
    }

    /// <summary>
    /// 场景判断器：运行场景并返回判断结果。
    /// </summary>
    public static class ScenarioJudge
    {
        public static ScenarioResult Run(GameScenario scenario)
        {
            var config = scenario.Config;
            var memory = new TractorGame.Core.AI.CardMemory(config);
            var builder = new RuleAIContextBuilder(config, scenario.Difficulty, null, memory);

            RuleAIContext context;
            PhaseDecision decision;

            switch (scenario.Phase)
            {
                case ScenarioPhase.Lead:
                {
                    context = builder.BuildLeadContext(scenario.Hand, scenario.Role,
                        playerIndex: scenario.PlayerIndex,
                        dealerIndex: scenario.DealerIndex,
                        visibleBottomCards: scenario.VisibleBottomCards,
                        trickIndex: scenario.TrickIndex,
                        turnIndex: scenario.TurnIndex,
                        playPosition: scenario.PlayPosition,
                        cardsLeftMin: scenario.CardsLeftMin,
                        currentWinningPlayer: scenario.CurrentWinningPlayer,
                        currentTrickScore: scenario.TrickScore,
                        defenderScore: scenario.DefenderScore,
                        bottomPoints: scenario.BottomPoints);
                    var policy = BuildLeadPolicy(config, memory);
                    decision = policy.Decide(context);
                    break;
                }
                case ScenarioPhase.Follow:
                {
                    context = builder.BuildFollowContext(
                        scenario.Hand,
                        scenario.LeadCards,
                        scenario.CurrentWinningCards.Count > 0 ? scenario.CurrentWinningCards : scenario.LeadCards,
                        scenario.Role,
                        scenario.PartnerWinning,
                        scenario.TrickScore,
                        cardsLeftMin: scenario.CardsLeftMin,
                        playerIndex: scenario.PlayerIndex,
                        dealerIndex: scenario.DealerIndex,
                        visibleBottomCards: scenario.VisibleBottomCards,
                        trickIndex: scenario.TrickIndex,
                        turnIndex: scenario.TurnIndex,
                        playPosition: scenario.PlayPosition,
                        currentWinningPlayer: scenario.CurrentWinningPlayer,
                        defenderScore: scenario.DefenderScore,
                        bottomPoints: scenario.BottomPoints);
                    var policy = BuildFollowPolicy(config, memory);
                    decision = policy.Decide(context);
                    break;
                }
                default:
                    throw new NotSupportedException($"Phase {scenario.Phase} not supported in ScenarioJudge");
            }

            var selected = decision.SelectedCards;
            var failures = scenario.Expectations
                .Where(e => !e.Check(selected, decision))
                .Select(e => e.Description)
                .ToList();

            return new ScenarioResult
            {
                ScenarioName = scenario.Name,
                Passed = failures.Count == 0,
                SelectedCards = selected,
                Intent = decision.Intent.PrimaryIntent,
                IntentMode = decision.Intent.Mode,
                FailedExpectations = failures,
                Explanation = decision.Explanation.SelectedReason
            };
        }

        private static LeadPolicy2 BuildLeadPolicy(GameConfig config, TractorGame.Core.AI.CardMemory memory) =>
            new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

        private static FollowPolicy2 BuildFollowPolicy(GameConfig config, TractorGame.Core.AI.CardMemory memory) =>
            new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());
    }

    public sealed class ScenarioResult
    {
        public string ScenarioName { get; init; } = string.Empty;
        public bool Passed { get; init; }
        public List<Card> SelectedCards { get; init; } = new();
        public DecisionIntentKind Intent { get; init; }
        public string IntentMode { get; init; } = string.Empty;
        public List<string> FailedExpectations { get; init; } = new();
        public string Explanation { get; init; } = string.Empty;

        public override string ToString()
        {
            var cards = string.Join(",", SelectedCards.Select(c => $"{c.Suit}{c.Rank}"));
            if (Passed)
                return $"[PASS] {ScenarioName} | {Intent}/{IntentMode} | {cards}";
            return $"[FAIL] {ScenarioName} | {Intent}/{IntentMode} | {cards} | 未满足: {string.Join("; ", FailedExpectations)}";
        }
    }
}
