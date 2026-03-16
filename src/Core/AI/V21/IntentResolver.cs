using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

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
            bool protectBottom = context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High;
            bool hasThrow = (candidates ?? new List<List<Card>>()).Any(cards =>
                cards.Count >= 3 && new CardPattern(cards, _config).Type == PatternType.Mixed);
            bool trumpHeavy = context.HandProfile.TrumpCount >= System.Math.Max(5, context.HandCount / 2);
            bool longSuit = context.HandProfile.StrongestSuit.HasValue &&
                context.HandProfile.SuitLengths.TryGetValue(context.HandProfile.StrongestSuit.Value, out var strongestLen) &&
                strongestLen >= 5;

            if (endgame && protectBottom)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.ProtectBottom, "ProtectBottomEndgame");
            }

            if (protectBottom)
            {
                return BuildIntent(context, DecisionIntentKind.ProtectBottom, DecisionIntentKind.SaveControl, "LeadProtectBottom");
            }

            if (hasThrow && context.StyleProfile.ThrowRiskTolerance >= 0.2)
            {
                return BuildIntent(context, DecisionIntentKind.PrepareThrow, DecisionIntentKind.PreserveStructure, "PrepareThrow");
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

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None &&
                (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                 context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High))
            {
                return BuildIntent(context, DecisionIntentKind.PrepareEndgame, DecisionIntentKind.ProtectBottom, "HighScoreOrEndgame");
            }

            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High)
            {
                return BuildIntent(context, DecisionIntentKind.ProtectBottom, DecisionIntentKind.SaveControl, "ProtectBottom");
            }

            if (context.PartnerWinning)
            {
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
                bool currentWinnerIsOpponent = context.CurrentWinningPlayer < 0 ||
                    !IsTeammate(context.PlayerIndex, context.CurrentWinningPlayer);
                bool hasStableWinningCandidate = winningCandidates.Any(candidate =>
                    threatAnalyzer.Analyze(context, candidate, currentWinning).SecurityLevel >= WinSecurityLevel.Stable);
                bool critical = context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical ||
                    context.TrickScore >= context.DifficultyProfile.TakeScoreThreshold;
                bool promoteToTakeScore = highScoreTrick ||
                    scoringTrickWithOpponentBehind ||
                    (currentWinnerIsOpponent && hasStableWinningCandidate) ||
                    context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                    context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High;
                double cheapestWinCost = winningCandidates
                    .Min(candidate => candidate.Count(_config.IsTrump) * 1.5 + RuleAIUtility.CountHighControlCards(_config, candidate) * 2.0);

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
