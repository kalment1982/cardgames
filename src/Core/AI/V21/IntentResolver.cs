using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 负责根据阶段、局面和候选集合，解析主意图/副意图以及成本约束。
    /// </summary>
    public sealed class IntentResolver
    {
        private readonly GameConfig _config;

        public IntentResolver(GameConfig config)
        {
            _config = config;
        }

        public ResolvedIntent Resolve(RuleAIContext context, IReadOnlyList<List<Card>>? candidates = null)
        {
            return context.Phase switch
            {
                PhaseKind.Bid => ResolveBidIntent(context),
                PhaseKind.BuryBottom => ResolveBuryIntent(context),
                PhaseKind.Lead => ResolveLeadIntent(context, candidates),
                PhaseKind.Follow => ResolveFollowIntent(context, candidates),
                _ => new ResolvedIntent()
            };
        }

        private ResolvedIntent ResolveBidIntent(RuleAIContext context)
        {
            bool strongTrump = context.HandProfile.TrumpCount >= System.Math.Max(4, context.HandCount / 2);
            var primary = strongTrump ? DecisionIntentKind.TakeLead : DecisionIntentKind.SaveControl;
            var secondary = strongTrump ? DecisionIntentKind.SaveControl : DecisionIntentKind.MinimizeLoss;
            return BuildIntent(context, primary, secondary, strongTrump ? "BidStrength" : "BidWait");
        }

        private ResolvedIntent ResolveBuryIntent(RuleAIContext context)
        {
            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High)
            {
                return BuildIntent(context, DecisionIntentKind.ProtectBottom, DecisionIntentKind.PreserveStructure, "BottomGuard");
            }

            if (context.HandProfile.PotentialVoidTargets.Count > 0)
            {
                return BuildIntent(context, DecisionIntentKind.ShapeHand, DecisionIntentKind.PreserveStructure, "CreateVoid");
            }

            return BuildIntent(context, DecisionIntentKind.PreserveStructure, DecisionIntentKind.SaveControl, "BalancedBury");
        }

        private ResolvedIntent ResolveLeadIntent(RuleAIContext context, IReadOnlyList<List<Card>>? candidates)
        {
            bool endgame = context.DecisionFrame.EndgameLevel != EndgameLevel.None;
            bool contestBottom = context.DecisionFrame.BottomContestPressure >= RiskLevel.Medium;
            bool protectBottom = context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High;
            bool hasCompellingThrow = HasCompellingThrowCandidate(candidates);
            bool trumpHeavy = context.HandProfile.TrumpCount >= System.Math.Max(5, context.HandCount / 2);
            bool longSuit = context.HandProfile.StrongestSuit.HasValue &&
                context.HandProfile.SuitLengths.TryGetValue(context.HandProfile.StrongestSuit.Value, out var strongestLen) &&
                strongestLen >= 5;
            bool earlySideSuitPressure = HasEarlyDealerSideSuitPressure(context);
            bool generalSideSuitPressure = HasSideSuitPressure(context);

            if (endgame && protectBottom)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.ProtectBottom, "ProtectBottomEndgame");
            }

            if (endgame && contestBottom)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.TakeScore, "ContestBottom");
            }

            if (protectBottom)
            {
                return BuildIntent(context, DecisionIntentKind.ProtectBottom, DecisionIntentKind.SaveControl, "LeadProtectBottom");
            }

            if (hasCompellingThrow)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareThrow, DecisionIntentKind.PreserveStructure, "PrepareThrow");
            }

            if (earlySideSuitPressure)
            {
                return BuildIntent(context, DecisionIntentKind.AttackLongSuit, DecisionIntentKind.TakeLead, "EarlySideSuit");
            }

            if (!endgame && !protectBottom && !contestBottom && generalSideSuitPressure)
            {
                return BuildIntent(context, DecisionIntentKind.AttackLongSuit, DecisionIntentKind.TakeLead, "SideSuitPressure");
            }

            if (trumpHeavy)
            {
                return BuildIntent(context, DecisionIntentKind.ForceTrump, DecisionIntentKind.SaveControl, "ForceTrump");
            }

            if (longSuit)
            {
                return BuildIntent(context, DecisionIntentKind.AttackLongSuit, DecisionIntentKind.TakeLead, "AttackLongSuit");
            }

            if (context.HandProfile.PotentialVoidTargets.Count > 0)
            {
                return BuildIntent(context, DecisionIntentKind.ProbeWeakSuit, DecisionIntentKind.ShapeHand, "ProbeWeakSuit");
            }

            return BuildIntent(context, DecisionIntentKind.TakeLead, DecisionIntentKind.PreserveStructure, "TakeLead");
        }

        private bool HasCompellingThrowCandidate(IReadOnlyList<List<Card>>? candidates)
        {
            if (candidates == null)
                return false;

            var validator = new ThrowValidator(_config);
            foreach (var cards in candidates)
            {
                if (cards.Count < 3 || cards.Count > 5)
                    continue;

                var pattern = new CardPattern(cards, _config);
                if (pattern.Type != PatternType.Mixed)
                    continue;

                var components = validator.DecomposeThrow(cards);
                bool hasPairPressure = components.Any(component => component.Count >= 2);
                bool hasControlSingle = components.Any(component =>
                    component.Count == 1 &&
                    IsHighControlThrowSingle(component[0]));

                if (hasPairPressure && hasControlSingle)
                    return true;
            }

            return false;
        }

        private bool IsHighControlThrowSingle(Card card)
        {
            if (card.Rank == Rank.BigJoker || card.Rank == Rank.SmallJoker)
                return true;

            if (!_config.IsTrump(card) && card.Rank == Rank.Ace)
                return true;

            return _config.IsTrump(card) && RuleAIUtility.GetCardValue(card, _config) >= 700;
        }

        private bool HasEarlyDealerSideSuitPressure(RuleAIContext context)
        {
            if (context.Role != AIRole.Dealer)
                return false;

            if (context.DecisionFrame.TrickIndex > 2)
                return false;

            return HasSideSuitPressure(context);
        }

        private bool HasSideSuitPressure(RuleAIContext context)
        {
            var comparer = new CardComparer(_config);
            var suitGroups = context.MyHand
                .Where(card => !_config.IsTrump(card))
                .GroupBy(card => card.Suit)
                .ToList();

            foreach (var group in suitGroups)
            {
                var cards = group.ToList();
                if (cards.Count < 2)
                    continue;

                var strongestTractor = RuleAIUtility.FindStrongestTractor(_config, cards, 4, comparer);
                if (strongestTractor != null)
                    return true;

                var strongestPair = RuleAIUtility.FindStrongestPair(cards, comparer);
                if (strongestPair != null)
                {
                    int value = RuleAIUtility.GetCardValue(strongestPair[0], _config);
                    if (value >= 112 || strongestPair.Sum(card => card.Score) >= 10)
                        return true;
                }

                var top = cards.OrderByDescending(card => RuleAIUtility.GetCardValue(card, _config)).First();
                bool hasSupport = cards.Count >= 2;
                if (hasSupport && RuleAIUtility.GetCardValue(top, _config) >= 112)
                    return true;
            }

            return false;
        }

        private ResolvedIntent ResolveFollowIntent(RuleAIContext context, IReadOnlyList<List<Card>>? candidates)
        {
            var candidateList = candidates ?? new List<List<Card>>();
            var currentWinning = context.CurrentWinningCards.Count > 0 ? context.CurrentWinningCards : context.LeadCards;
            var threatAnalyzer = new FollowThreatAnalyzer(_config);
            var winningCandidates = currentWinning.Count > 0
                ? candidateList.Where(candidate =>
                    candidate.Count == currentWinning.Count &&
                    RuleAIUtility.CanBeatCards(_config, currentWinning, candidate))
                    .ToList()
                : new List<List<Card>>();
            bool hasWinningCandidate = winningCandidates.Count > 0;
            bool contestBottom = context.DecisionFrame.BottomContestPressure >= RiskLevel.Medium;

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None &&
                (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                 context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High))
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.ProtectBottom, "HighScoreOrEndgame");
            }

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None && contestBottom && hasWinningCandidate)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.TakeScore, "ContestBottom");
            }

            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High)
            {
                return BuildIntent(context, DecisionIntentKind.ProtectBottom, DecisionIntentKind.SaveControl, "ProtectBottom");
            }

            if (context.PartnerWinning)
            {
                int opponentsBehind = threatAnalyzer.CountOpponentsBehind(context);
                bool scoringTrickWithOpponentBehind = context.TrickScore >= 5 && opponentsBehind > 0;
                bool lastOpponentBehind = opponentsBehind == 1 && context.DecisionFrame.PlayPosition >= 3;
                bool hasStableWinningCandidate = winningCandidates.Any(candidate =>
                    threatAnalyzer.Analyze(context, candidate, currentWinning).SecurityLevel >= WinSecurityLevel.Stable);
                double cheapestWinCost = hasWinningCandidate
                    ? winningCandidates.Min(candidate =>
                        candidate.Count(_config.IsTrump) * 1.5 + RuleAIUtility.CountHighControlCards(_config, candidate) * 2.0)
                    : double.MaxValue;
                bool affordableWinningTakeover = hasWinningCandidate &&
                    cheapestWinCost <= 1.0 + context.DifficultyProfile.CheapOvertakeTolerance;
                bool highValueMateProtection = context.TrickScore >= 10 ||
                    (context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical && context.TrickScore >= 5);
                bool protectMateTrumpControl = ShouldProtectMateTrumpControl(
                    context,
                    currentWinning,
                    winningCandidates,
                    threatAnalyzer);

                if (affordableWinningTakeover &&
                    scoringTrickWithOpponentBehind &&
                    highValueMateProtection &&
                    (hasStableWinningCandidate || lastOpponentBehind))
                {
                    return BuildIntent(
                        context,
                        DecisionIntentKind.TakeScore,
                        DecisionIntentKind.PreserveStructure,
                        hasStableWinningCandidate ? "ProtectMateScoringTrick" : "ReinforceMateFragileLead");
                }

                if (protectMateTrumpControl)
                {
                    return BuildIntent(
                        context,
                        DecisionIntentKind.TakeLead,
                        DecisionIntentKind.PreserveStructure,
                        "ProtectMateTrumpControl");
                }

                var mode = context.InferenceSnapshot.MateHoldConfidence.Probability >= 0.65
                    ? "MateWinningSecure"
                    : "MateWinningFragile";
                return BuildIntent(context, DecisionIntentKind.PassToMate, DecisionIntentKind.PreserveStructure, mode);
            }

            if (hasWinningCandidate)
            {
                int opponentsBehind = threatAnalyzer.CountOpponentsBehind(context);
                bool highScoreTrick = context.TrickScore >= 10;
                bool scoringTrickWithOpponentBehind = context.TrickScore >= 5 && opponentsBehind > 0;
                bool currentWinnerIsOpponent = context.DecisionFrame.CurrentWinningPlayer < 0 ||
                    !IsTeammate(context.PlayerIndex, context.DecisionFrame.CurrentWinningPlayer);
                bool hasStableWinningCandidate = winningCandidates.Any(candidate =>
                    threatAnalyzer.Analyze(context, candidate, currentWinning).SecurityLevel >= WinSecurityLevel.Stable);
                bool critical = context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical ||
                    context.TrickScore >= context.DifficultyProfile.TakeScoreThreshold;
                double cheapestWinCost = winningCandidates
                    .Min(candidate => candidate.Count(_config.IsTrump) * 1.5 + RuleAIUtility.CountHighControlCards(_config, candidate) * 2.0);
                bool promoteToTakeScore = highScoreTrick || scoringTrickWithOpponentBehind;
                bool affordableStableWin = hasStableWinningCandidate &&
                    cheapestWinCost <= 1.0 + context.DifficultyProfile.CheapOvertakeTolerance;
                promoteToTakeScore = promoteToTakeScore ||
                    (currentWinnerIsOpponent && affordableStableWin) ||
                    context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                    context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High;

                if (!critical && !promoteToTakeScore &&
                    cheapestWinCost > 1.0 + context.DifficultyProfile.CheapOvertakeTolerance)
                    return BuildIntent(context, DecisionIntentKind.MinimizeLoss, DecisionIntentKind.PreserveStructure, "OpponentWinningTooExpensive");

                return BuildIntent(
                    context,
                    critical || promoteToTakeScore ? DecisionIntentKind.TakeScore : DecisionIntentKind.TakeLead,
                    DecisionIntentKind.PreserveStructure,
                    critical || promoteToTakeScore ? "HighScoreOrEndgame" : "OpponentWinningCheapOvertake");
            }

            return BuildIntent(context, DecisionIntentKind.MinimizeLoss, DecisionIntentKind.PreserveStructure, "OpponentWinningTooExpensive");
        }

        private bool ShouldProtectMateTrumpControl(
            RuleAIContext context,
            List<Card> currentWinning,
            List<List<Card>> winningCandidates,
            FollowThreatAnalyzer threatAnalyzer)
        {
            if (context.Role != AIRole.DealerPartner)
                return false;

            if (context.TrickScore > 0)
                return false;

            if (currentWinning.Count != 1 || !_config.IsTrump(currentWinning[0]))
                return false;

            if (RuleAIUtility.GetCardValue(currentWinning[0], _config) > 610)
                return false;

            if (threatAnalyzer.CountOpponentsBehind(context) <= 0)
                return false;

            foreach (var candidate in winningCandidates)
            {
                var assessment = threatAnalyzer.Analyze(context, candidate, currentWinning);
                if (assessment.SecurityLevel >= WinSecurityLevel.Lock)
                    return true;
            }

            return false;
        }

        private static ResolvedIntent BuildIntent(
            RuleAIContext context,
            DecisionIntentKind primary,
            DecisionIntentKind secondary,
            string mode)
        {
            var riskFlags = new List<string>();
            var vetoFlags = new List<string>();

            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High)
            {
                riskFlags.Add("high_bottom_risk");
                vetoFlags.Add("no_risky_throw");
            }

            if (context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High)
                riskFlags.Add("dealer_retention_risk");

            if (context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical)
                riskFlags.Add("critical_score_pressure");

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                riskFlags.Add($"endgame_{context.DecisionFrame.EndgameLevel}");

            if (primary == DecisionIntentKind.PassToMate)
                vetoFlags.Add("avoid_overtake_mate");

            if (secondary == DecisionIntentKind.PreserveStructure)
                vetoFlags.Add("avoid_break_structure");

            return new ResolvedIntent
            {
                PrimaryIntent = primary,
                SecondaryIntent = secondary,
                Mode = mode,
                Priority = GetPriority(primary),
                RiskFlags = riskFlags,
                VetoFlags = vetoFlags,
                MaxCostBudget = ResolveCostBudget(primary, context)
            };
        }

        private static double GetPriority(DecisionIntentKind intent)
        {
            return intent switch
            {
                DecisionIntentKind.PrepareEndgame => 9.0,
                DecisionIntentKind.ProtectBottom => 8.0,
                DecisionIntentKind.TakeScore => 7.0,
                DecisionIntentKind.TakeLead => 6.0,
                DecisionIntentKind.PassToMate => 5.0,
                DecisionIntentKind.SaveControl => 4.0,
                DecisionIntentKind.PreserveStructure => 3.5,
                DecisionIntentKind.ShapeHand => 3.0,
                DecisionIntentKind.ForceTrump => 2.8,
                DecisionIntentKind.AttackLongSuit => 2.7,
                DecisionIntentKind.ProbeWeakSuit => 2.6,
                DecisionIntentKind.PrepareThrow => 2.5,
                DecisionIntentKind.MinimizeLoss => 2.0,
                _ => 1.0
            };
        }

        private static double ResolveCostBudget(DecisionIntentKind primary, RuleAIContext context)
        {
            if (primary == DecisionIntentKind.PrepareEndgame || primary == DecisionIntentKind.ProtectBottom)
                return 0.35;

            if (primary == DecisionIntentKind.PassToMate)
                return 0.40;

            if (primary == DecisionIntentKind.TakeScore &&
                context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical)
                return 0.85;

            if (primary == DecisionIntentKind.MinimizeLoss)
                return 0.20;

            return 0.60;
        }

        private static bool IsTeammate(int myPlayerIndex, int otherPlayerIndex)
        {
            if (myPlayerIndex < 0 || otherPlayerIndex < 0)
                return false;

            return ((otherPlayerIndex - myPlayerIndex + 4) % 4) == 2;
        }
    }
}
