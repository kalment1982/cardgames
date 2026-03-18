using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 在有限合法候选集合上进行收益/成本/风险打分。
    /// </summary>
    public sealed class ActionScorer
    {
        private readonly GameConfig _config;
        private readonly FollowThreatAnalyzer _followThreatAnalyzer;

        public ActionScorer(GameConfig config)
        {
            _config = config;
            _followThreatAnalyzer = new FollowThreatAnalyzer(config);
        }

        public List<ScoredAction> Score(RuleAIContext context, ResolvedIntent intent, IEnumerable<List<Card>> candidates)
        {
            var comparer = new CardComparer(_config);
            var scored = candidates
                .Select(candidate => ScoreSingle(context, intent, candidate, comparer))
                .ToList();

            if (ShouldUseSecurityFloorOrdering(context, intent))
                return OrderWithSecurityFloor(context, intent, scored);

            if (ShouldUsePassToMateOrdering(context, intent))
                return OrderForPassToMate(scored);

            return scored
                .OrderByDescending(action => action.Score)
                .ThenBy(action => RuleAIUtility.BuildCandidateKey(action.Cards), StringComparer.Ordinal)
                .ToList();
        }

        private ScoredAction ScoreSingle(
            RuleAIContext context,
            ResolvedIntent intent,
            List<Card> candidate,
            CardComparer comparer)
        {
            var features = context.Phase switch
            {
                PhaseKind.Lead => BuildLeadFeatures(context, candidate, comparer),
                PhaseKind.Follow => BuildFollowFeatures(context, candidate, comparer),
                PhaseKind.BuryBottom => BuildBuryFeatures(context, candidate, comparer),
                _ => BuildDefaultFeatures(context, candidate, comparer)
            };

            double baseScore =
                features.GetValueOrDefault("TrickWinValue") * Weight("TrickWinValue", context) +
                features.GetValueOrDefault("TrickScoreSwing") * Weight("TrickScoreSwing", context) +
                features.GetValueOrDefault("WinSecurityValue") * Weight("WinSecurityValue", context) +
                features.GetValueOrDefault("PointProtectionValue") * Weight("PointProtectionValue", context) +
                features.GetValueOrDefault("BottomSafetyValue") * Weight("BottomSafetyValue", context) +
                features.GetValueOrDefault("LeadControlValue") * Weight("LeadControlValue", context) +
                features.GetValueOrDefault("HandShapeValue") * Weight("HandShapeValue", context) +
                features.GetValueOrDefault("MateSyncValue") * Weight("MateSyncValue", context) -
                features.GetValueOrDefault("TrumpConsumptionCost") * Weight("TrumpConsumptionCost", context) -
                features.GetValueOrDefault("HighControlLossCost") * Weight("HighControlLossCost", context) -
                features.GetValueOrDefault("StructureBreakCost") * Weight("StructureBreakCost", context) -
                features.GetValueOrDefault("InfoLeakCost") * Weight("InfoLeakCost", context) -
                features.GetValueOrDefault("FuturePointRisk") * Weight("FuturePointRisk", context) -
                features.GetValueOrDefault("BehindOpponentThreat") * Weight("BehindOpponentThreat", context) -
                features.GetValueOrDefault("RecutRiskCost") * Weight("RecutRiskCost", context) +
                features.GetValueOrDefault("EndgameReserveValue") * Weight("EndgameReserveValue", context);

            double score = baseScore;
            score += ApplyScenarioAdjust(context, features);
            score += ApplyIntentAdjust(intent, features, candidate);
            score += ApplyEndgameCloseoutAdjust(context, candidate);
            score -= ApplyRiskPenalty(context, intent, candidate, features);
            score += ResolveTieBreakNoise(context, candidate);

            return new ScoredAction
            {
                Cards = new List<Card>(candidate),
                Score = Math.Round(score, 4),
                ReasonCode = BuildReasonCode(context, intent, candidate, features),
                Features = features
            };
        }

        private Dictionary<string, double> BuildLeadFeatures(RuleAIContext context, List<Card> candidate, CardComparer comparer)
        {
            var remaining = RuleAIUtility.RemoveCards(context.MyHand, candidate);
            var pattern = new CardPattern(candidate, _config);
            var systemKey = ResolveSystemKey(candidate);
            double rawStructureLoss = RuleAIUtility.EstimateStructureLoss(_config, context.MyHand, candidate, comparer);
            double effectiveStructureLoss = pattern.Type switch
            {
                PatternType.Tractor => Math.Max(0, rawStructureLoss - candidate.Count * 2.0),
                PatternType.Pair => Math.Max(0, rawStructureLoss - 6.0),
                PatternType.Mixed when candidate.Count >= 3 => Math.Max(0, rawStructureLoss - candidate.Count),
                _ => rawStructureLoss
            };

            return new Dictionary<string, double>
            {
                ["TrickWinValue"] = pattern.Type == PatternType.Tractor ? candidate.Count * 0.9 :
                    pattern.Type == PatternType.Pair ? 1.8 :
                    candidate.Count == 1 ? RuleAIUtility.GetCardValue(candidate[0], _config) / 180.0 :
                    candidate.Count * 0.4,
                ["TrickScoreSwing"] = candidate.Sum(card => card.Score) / 10.0,
                ["ContestBottomValue"] = ResolveContestBottomValue(context, candidate),
                ["BottomSafetyValue"] = context.IsDealerSide ? RemainingControlValue(remaining) / 8.0 : 0,
                ["LeadControlValue"] = pattern.Type == PatternType.Tractor ? 6.0 :
                    pattern.Type == PatternType.Pair ? 2.8 :
                    candidate.All(_config.IsTrump) ? 1.6 : 1.0,
                ["HandShapeValue"] = EvaluateCreateVoidValue(context.MyHand, remaining),
                ["MateSyncValue"] = 0,
                ["TrumpConsumptionCost"] = candidate.Count(_config.IsTrump),
                ["HighControlLossCost"] = RuleAIUtility.CountHighControlCards(_config, candidate),
                ["StructureBreakCost"] = effectiveStructureLoss,
                ["InfoLeakCost"] = candidate.Count(_config.IsTrump) * 0.6 + RuleAIUtility.CountHighControlCards(_config, candidate) * 0.5,
                ["RecutRiskCost"] = ResolveLeadCutRisk(context, systemKey),
                ["EndgameReserveValue"] = RemainingControlValue(remaining)
            };
        }

        private Dictionary<string, double> BuildFollowFeatures(RuleAIContext context, List<Card> candidate, CardComparer comparer)
        {
            var currentWinning = context.CurrentWinningCards.Count > 0 ? context.CurrentWinningCards : context.LeadCards;
            var remaining = RuleAIUtility.RemoveCards(context.MyHand, candidate);
            bool canBeat = currentWinning.Count == candidate.Count &&
                currentWinning.Count > 0 &&
                RuleAIUtility.CanBeatCards(_config, currentWinning, candidate);
            var threatAssessment = canBeat
                ? _followThreatAnalyzer.Analyze(context, candidate, currentWinning)
                : new FollowThreatAssessment();
            double sentPoints = candidate.Sum(card => card.Score) / 10.0;
            double currentTrickScore = context.TrickScore / 10.0;

            return new Dictionary<string, double>
            {
                ["TrickWinValue"] = canBeat ? 2.5 : 0,
                ["TrickScoreSwing"] = canBeat ? currentTrickScore + sentPoints : 0,
                ["WinSecurityValue"] = canBeat ? threatAssessment.WinSecurityValue : 0,
                ["WinMarginValue"] = canBeat ? threatAssessment.WinMargin : 0,
                ["PointProtectionValue"] = canBeat ? threatAssessment.PointProtectionValue : 0,
                ["ContestBottomValue"] = canBeat ? ResolveContestBottomValue(context, candidate) : 0,
                ["BottomSafetyValue"] = context.IsDealerSide ? RemainingControlValue(remaining) / 8.0 : 0,
                ["LeadControlValue"] = canBeat ? 1.5 : 0,
                ["HandShapeValue"] = EvaluateCreateVoidValue(context.MyHand, remaining),
                ["MateSyncValue"] = context.PartnerWinning && !canBeat ? 1.0 + sentPoints : 0,
                ["TrumpConsumptionCost"] = candidate.Count(_config.IsTrump),
                ["HighControlLossCost"] = RuleAIUtility.CountHighControlCards(_config, candidate) +
                    candidate.Sum(card => RuleAIUtility.GetCardValue(card, _config)) / 400.0,
                ["StructureBreakCost"] = RuleAIUtility.EstimateStructureLoss(_config, context.MyHand, candidate, comparer),
                ["InfoLeakCost"] = candidate.Count(_config.IsTrump) * 0.4 +
                    sentPoints * 0.2 +
                    candidate.Sum(card => RuleAIUtility.GetCardValue(card, _config)) / 500.0,
                ["DiscardRankCost"] = candidate.Sum(card => _config.IsTrump(card) ? 0 : (int)card.Rank),
                ["DiscardPointCost"] = candidate.Sum(card => card.Score) / 5.0,
                ["FuturePointRisk"] = canBeat ? threatAssessment.FuturePointRisk : 0,
                ["BehindOpponentThreat"] = canBeat ? threatAssessment.BehindOpponentThreat : 0,
                ["RecutRiskCost"] = canBeat ? EstimateRecutRisk(context, candidate, currentWinning, comparer, threatAssessment) : 0,
                ["EndgameReserveValue"] = RemainingControlValue(remaining)
            };
        }

        private Dictionary<string, double> BuildBuryFeatures(RuleAIContext context, List<Card> candidate, CardComparer comparer)
        {
            var remaining = RuleAIUtility.RemoveCards(context.MyHand, candidate);
            return new Dictionary<string, double>
            {
                ["TrickWinValue"] = 0,
                ["TrickScoreSwing"] = 0,
                ["BottomSafetyValue"] = Math.Max(0, 4.0 - candidate.Sum(card => card.Score) / 10.0),
                ["LeadControlValue"] = 0,
                ["HandShapeValue"] = EvaluateCreateVoidValue(context.MyHand, remaining),
                ["MateSyncValue"] = 0,
                ["TrumpConsumptionCost"] = candidate.Count(_config.IsTrump),
                ["HighControlLossCost"] = RuleAIUtility.CountHighControlCards(_config, candidate),
                ["StructureBreakCost"] = RuleAIUtility.EstimateStructureLoss(_config, context.MyHand, candidate, comparer),
                ["InfoLeakCost"] = candidate.Count(card => card.Score > 0) * 0.5,
                ["RecutRiskCost"] = 0,
                ["EndgameReserveValue"] = RemainingControlValue(remaining)
            };
        }

        private Dictionary<string, double> BuildDefaultFeatures(RuleAIContext context, List<Card> candidate, CardComparer comparer)
        {
            var remaining = RuleAIUtility.RemoveCards(context.MyHand, candidate);
            return new Dictionary<string, double>
            {
                ["TrickWinValue"] = candidate.Count,
                ["TrickScoreSwing"] = candidate.Sum(card => card.Score) / 10.0,
                ["WinSecurityValue"] = 0,
                ["PointProtectionValue"] = 0,
                ["BottomSafetyValue"] = 0,
                ["LeadControlValue"] = 0,
                ["HandShapeValue"] = EvaluateCreateVoidValue(context.MyHand, remaining),
                ["MateSyncValue"] = 0,
                ["TrumpConsumptionCost"] = candidate.Count(_config.IsTrump),
                ["HighControlLossCost"] = RuleAIUtility.CountHighControlCards(_config, candidate),
                ["StructureBreakCost"] = RuleAIUtility.EstimateStructureLoss(_config, context.MyHand, candidate, comparer),
                ["InfoLeakCost"] = 0,
                ["FuturePointRisk"] = 0,
                ["BehindOpponentThreat"] = 0,
                ["RecutRiskCost"] = 0,
                ["EndgameReserveValue"] = RemainingControlValue(remaining)
            };
        }

        private double Weight(string feature, RuleAIContext context)
        {
            var difficulty = context.DifficultyProfile;
            return feature switch
            {
                "TrickWinValue" => 0.90,
                "TrickScoreSwing" => 1.25,
                "WinSecurityValue" => 1.15,
                "PointProtectionValue" => 1.35,
                "BottomSafetyValue" => 1.35,
                "LeadControlValue" => 0.80,
                "HandShapeValue" => 0.45,
                "MateSyncValue" => 0.60 * difficulty.PassToMateBias,
                "TrumpConsumptionCost" => 0.95 * difficulty.TrumpConsumptionPenalty,
                "HighControlLossCost" => 1.10 * difficulty.HighCardRetentionPenalty,
                "StructureBreakCost" => 1.05 * difficulty.PreserveStructureWeight,
                "InfoLeakCost" => 0.35,
                "FuturePointRisk" => 1.25,
                "BehindOpponentThreat" => 0.45,
                "RecutRiskCost" => 0.85,
                "EndgameReserveValue" => 1.20 * difficulty.PreserveControlWeight,
                _ => 1.0
            };
        }

        private static double ApplyScenarioAdjust(RuleAIContext context, Dictionary<string, double> features)
        {
            double score = 0;
            if (context.DecisionFrame.ScorePressure == ScorePressureLevel.Critical)
                score += features.GetValueOrDefault("TrickScoreSwing") * 0.80;

            if (context.DecisionFrame.ScorePressure == ScorePressureLevel.Tight)
                score += features.GetValueOrDefault("TrickScoreSwing") * 0.30;

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                score += features.GetValueOrDefault("EndgameReserveValue") * 0.90 +
                    features.GetValueOrDefault("BottomSafetyValue") * 0.70;

            if (context.PartnerWinning)
                score += features.GetValueOrDefault("MateSyncValue") * 0.60;

            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High)
                score += features.GetValueOrDefault("BottomSafetyValue") * 1.10;

            if (context.TrickScore >= 5 && features.GetValueOrDefault("BehindOpponentThreat") > 0)
            {
                score += features.GetValueOrDefault("WinSecurityValue") * 0.90;
                score += features.GetValueOrDefault("PointProtectionValue") * 0.70;
                score -= features.GetValueOrDefault("FuturePointRisk") * 0.90;
            }

            if (context.DecisionFrame.BottomContestPressure >= RiskLevel.Medium &&
                context.DecisionFrame.EndgameLevel != EndgameLevel.None)
            {
                score += features.GetValueOrDefault("ContestBottomValue") * 0.80;
            }

            return score;
        }

        private static double ApplyIntentAdjust(ResolvedIntent intent, Dictionary<string, double> features, List<Card> candidate)
        {
            return intent.PrimaryIntent switch
            {
                DecisionIntentKind.PassToMate =>
                    features.GetValueOrDefault("MateSyncValue") * 2.40 -
                    features.GetValueOrDefault("TrickWinValue") * 2.20 -
                    features.GetValueOrDefault("TrumpConsumptionCost") * 0.80,
                DecisionIntentKind.TakeScore =>
                    features.GetValueOrDefault("TrickWinValue") * 1.80 +
                    features.GetValueOrDefault("TrickScoreSwing") * 1.60 +
                    features.GetValueOrDefault("WinSecurityValue") * 1.80 +
                    features.GetValueOrDefault("PointProtectionValue") * 1.60 -
                    features.GetValueOrDefault("FuturePointRisk") * 1.50 -
                    features.GetValueOrDefault("StructureBreakCost") * 0.20,
                DecisionIntentKind.ProtectBottom =>
                    features.GetValueOrDefault("BottomSafetyValue") * 2.50 +
                    features.GetValueOrDefault("EndgameReserveValue") * 1.40 -
                    features.GetValueOrDefault("FuturePointRisk") * 1.20 -
                    features.GetValueOrDefault("TrumpConsumptionCost") * 0.80,
                DecisionIntentKind.PrepareEndgame =>
                    features.GetValueOrDefault("EndgameReserveValue") * 2.20 +
                    features.GetValueOrDefault("BottomSafetyValue") * 1.20 +
                    features.GetValueOrDefault("ContestBottomValue") * 1.60 +
                    features.GetValueOrDefault("WinSecurityValue") * 0.90 -
                    features.GetValueOrDefault("FuturePointRisk") * 1.00,
                DecisionIntentKind.TakeLead =>
                    features.GetValueOrDefault("LeadControlValue") * 1.60 +
                    features.GetValueOrDefault("TrickWinValue") * 0.80 +
                    features.GetValueOrDefault("WinSecurityValue") * 0.40 -
                    features.GetValueOrDefault("FuturePointRisk") * 0.35,
                DecisionIntentKind.SaveControl =>
                    features.GetValueOrDefault("EndgameReserveValue") * 1.60 -
                    features.GetValueOrDefault("HighControlLossCost") * 0.80,
                DecisionIntentKind.PreserveStructure =>
                    features.GetValueOrDefault("HandShapeValue") * 0.80 -
                    features.GetValueOrDefault("StructureBreakCost") * 1.20,
                DecisionIntentKind.ShapeHand =>
                    features.GetValueOrDefault("HandShapeValue") * 1.80,
                DecisionIntentKind.ForceTrump =>
                    features.GetValueOrDefault("LeadControlValue") * 1.80 +
                    candidate.Count * 0.20,
                DecisionIntentKind.AttackLongSuit =>
                    features.GetValueOrDefault("LeadControlValue") * 1.10 +
                    features.GetValueOrDefault("HandShapeValue") * 0.60,
                DecisionIntentKind.ProbeWeakSuit =>
                    features.GetValueOrDefault("HandShapeValue") * 1.20 -
                    features.GetValueOrDefault("InfoLeakCost") * 0.20,
                DecisionIntentKind.PrepareThrow =>
                    candidate.Count >= 4 ? 6.00 + candidate.Count * 0.50 :
                    candidate.Count == 3 ? 3.50 :
                    -4.50,
                DecisionIntentKind.MinimizeLoss =>
                    -features.GetValueOrDefault("TrumpConsumptionCost") * 1.60 -
                    features.GetValueOrDefault("HighControlLossCost") * 1.20 -
                    features.GetValueOrDefault("StructureBreakCost") * 0.60 -
                    features.GetValueOrDefault("DiscardRankCost") * 0.55 -
                    features.GetValueOrDefault("DiscardPointCost") * 1.10,
                _ => 0
            };
        }

        private static double ApplyRiskPenalty(
            RuleAIContext context,
            ResolvedIntent intent,
            List<Card> candidate,
            Dictionary<string, double> features)
        {
            double penalty = 0;
            if (intent.VetoFlags.Contains("avoid_overtake_mate") && features.GetValueOrDefault("TrickWinValue") > 0)
                penalty += 8.0;

            if (intent.VetoFlags.Contains("avoid_break_structure"))
                penalty += features.GetValueOrDefault("StructureBreakCost") * 0.80;

            if (intent.VetoFlags.Contains("no_risky_throw") && candidate.Count >= 3)
                penalty += 5.0;

            if (context.Phase == PhaseKind.BuryBottom && candidate.Sum(card => card.Score) >= 20)
                penalty += 4.0;

            return penalty;
        }

        private static double ResolveTieBreakNoise(RuleAIContext context, List<Card> candidate)
        {
            if (context.StyleProfile.TieBreakRandomness <= 0)
                return 0;

            string key = RuleAIUtility.BuildCandidateKey(candidate) + ":" + context.StyleProfile.SessionStyleSeed;
            uint hash = 2166136261u;
            foreach (char c in key) { hash ^= c; hash *= 16777619u; }
            double normalized = (hash & 0xFFFF) / 65535.0;
            return (normalized - 0.5) * context.StyleProfile.TieBreakRandomness;
        }

        private double ApplyEndgameCloseoutAdjust(RuleAIContext context, List<Card> candidate)
        {
            if (context.Phase != PhaseKind.Lead)
                return 0;

            if (context.DecisionFrame.EndgameLevel == EndgameLevel.None)
                return 0;

            if (candidate.Count != context.MyHand.Count || candidate.Count < 3)
                return 0;

            var pattern = new CardPattern(candidate, _config);
            if (pattern.Type != PatternType.Mixed)
                return 0;

            return context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ? 20.0 : 12.0;
        }

        private double EvaluateCreateVoidValue(List<Card> before, List<Card> after)
        {
            int beforeVoids = CountVoidTargets(before);
            int afterVoids = CountVoidTargets(after);
            return Math.Max(0, afterVoids - beforeVoids) * 1.5;
        }

        private int CountVoidTargets(List<Card> cards)
        {
            var nonTrumpSuits = cards
                .Where(card => !_config.IsTrump(card))
                .GroupBy(card => card.Suit)
                .ToDictionary(group => group.Key, group => group.Count());

            return new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond }
                .Count(suit => !nonTrumpSuits.ContainsKey(suit) || nonTrumpSuits[suit] <= 1);
        }

        private static double RemainingControlValue(List<Card> remaining)
        {
            return remaining.Sum(card => card.Score > 0 ? 0.15 : 0) + remaining.Count(card => card.IsJoker) * 2.0 + remaining.Count;
        }

        private double ResolveLeadCutRisk(RuleAIContext context, string systemKey)
        {
            if (!context.InferenceSnapshot.LeadCutRiskBySystem.TryGetValue(systemKey, out var risk))
                return 0;

            return risk.Level switch
            {
                RiskLevel.High => 2.0,
                RiskLevel.Medium => 1.0,
                RiskLevel.Low => 0.4,
                _ => 0
            };
        }

        private double EstimateRecutRisk(
            RuleAIContext context,
            List<Card> candidate,
            List<Card> currentWinningCards,
            CardComparer comparer,
            FollowThreatAssessment threatAssessment)
        {
            double risk = 0;
            int winMargin = RuleAIUtility.CalculateWinMargin(_config, candidate, currentWinningCards, comparer);
            if (winMargin < 60)
                risk += 1.0;

            if (context.DecisionFrame.PlayPosition <= 2)
                risk += 0.8;

            if (candidate.Count(_config.IsTrump) > 0)
                risk += 0.6;

            risk += threatAssessment.BehindOpponentThreat * 0.35;
            risk += threatAssessment.StrongerThreatCount * 0.10;
            if (threatAssessment.SecurityLevel == WinSecurityLevel.Fragile)
                risk += 0.8;

            return risk;
        }

        private bool ShouldUseSecurityFloorOrdering(RuleAIContext context, ResolvedIntent intent)
        {
            return context.Phase == PhaseKind.Follow &&
                (intent.PrimaryIntent == DecisionIntentKind.TakeScore ||
                 intent.PrimaryIntent == DecisionIntentKind.ProtectBottom ||
                 intent.PrimaryIntent == DecisionIntentKind.PrepareEndgame ||
                 intent.Mode == "ProtectMateTrumpControl");
        }

        private bool ShouldUsePassToMateOrdering(RuleAIContext context, ResolvedIntent intent)
        {
            return context.Phase == PhaseKind.Follow &&
                intent.PrimaryIntent == DecisionIntentKind.PassToMate;
        }

        private List<ScoredAction> OrderWithSecurityFloor(
            RuleAIContext context,
            ResolvedIntent intent,
            List<ScoredAction> scored)
        {
            double requiredSecurityFloor = ResolveRequiredSecurityFloor(context, intent);
            bool hasFloorCandidate = scored.Any(action =>
                action.Features.GetValueOrDefault("TrickWinValue") > 0 &&
                action.Features.GetValueOrDefault("WinSecurityValue") >= requiredSecurityFloor);
            bool preferCheapestSecureWin =
                hasFloorCandidate &&
                ((context.TrickScore <= 5 &&
                  (intent.PrimaryIntent == DecisionIntentKind.ProtectBottom ||
                   intent.PrimaryIntent == DecisionIntentKind.PrepareEndgame)) ||
                 (intent.PrimaryIntent == DecisionIntentKind.TakeScore &&
                  (context.CurrentWinningCards.Count > 1 || context.LeadCards.Count > 1)));
            bool preferHigherMarginToProtectMate =
                context.PartnerWinning &&
                context.TrickScore >= 5 &&
                _followThreatAnalyzer.CountOpponentsBehind(context) > 0 &&
                !hasFloorCandidate;

            if (preferCheapestSecureWin)
            {
                bool takeScore = intent.PrimaryIntent == DecisionIntentKind.TakeScore;
                return scored
                    .OrderByDescending(action => action.Features.GetValueOrDefault("TrickWinValue") > 0 ? 1 : 0)
                    .ThenByDescending(action => ResolveSecurityBucket(action, hasFloorCandidate, requiredSecurityFloor))
                    .ThenBy(action => takeScore ? action.Features.GetValueOrDefault("DiscardPointCost") : ResolveControlSpendCost(action))
                    .ThenBy(action => takeScore ? ResolveControlSpendCost(action) : action.Features.GetValueOrDefault("DiscardPointCost"))
                    .ThenBy(action => action.Features.GetValueOrDefault("FuturePointRisk"))
                    .ThenBy(action => action.Features.GetValueOrDefault("StructureBreakCost"))
                    .ThenByDescending(action => action.Features.GetValueOrDefault("PointProtectionValue"))
                    .ThenByDescending(action => action.Score)
                    .ThenBy(action => RuleAIUtility.BuildCandidateKey(action.Cards), StringComparer.Ordinal)
                    .ToList();
            }

            return scored
                .OrderByDescending(action => action.Features.GetValueOrDefault("TrickWinValue") > 0 ? 1 : 0)
                .ThenByDescending(action => ResolveSecurityBucket(action, hasFloorCandidate, requiredSecurityFloor))
                .ThenByDescending(action => preferHigherMarginToProtectMate
                    ? action.Features.GetValueOrDefault("WinMarginValue")
                    : 0)
                .ThenByDescending(action => action.Features.GetValueOrDefault("PointProtectionValue"))
                .ThenBy(action => action.Features.GetValueOrDefault("FuturePointRisk"))
                .ThenBy(action => ResolveControlSpendCost(action))
                .ThenBy(action => action.Features.GetValueOrDefault("StructureBreakCost"))
                .ThenByDescending(action => action.Score)
                .ThenBy(action => RuleAIUtility.BuildCandidateKey(action.Cards), StringComparer.Ordinal)
                .ToList();
        }

        private List<ScoredAction> OrderForPassToMate(List<ScoredAction> scored)
        {
            return scored
                .OrderBy(action => action.Features.GetValueOrDefault("TrickWinValue") > 0 ? 1 : 0)
                .ThenByDescending(action => action.Features.GetValueOrDefault("MateSyncValue"))
                .ThenBy(action => action.Features.GetValueOrDefault("DiscardPointCost"))
                .ThenBy(action => action.Features.GetValueOrDefault("TrumpConsumptionCost"))
                .ThenBy(action => action.Features.GetValueOrDefault("HighControlLossCost"))
                .ThenBy(action => action.Features.GetValueOrDefault("DiscardRankCost"))
                .ThenBy(action => action.Features.GetValueOrDefault("InfoLeakCost"))
                .ThenBy(action => action.Features.GetValueOrDefault("StructureBreakCost"))
                .ThenByDescending(action => action.Score)
                .ThenBy(action => RuleAIUtility.BuildCandidateKey(action.Cards), StringComparer.Ordinal)
                .ToList();
        }

        private double ResolveRequiredSecurityFloor(RuleAIContext context, ResolvedIntent intent)
        {
            if (intent.Mode == "ProtectMateTrumpControl")
                return (double)WinSecurityLevel.Lock;

            if (intent.PrimaryIntent == DecisionIntentKind.ProtectBottom ||
                intent.PrimaryIntent == DecisionIntentKind.PrepareEndgame)
            {
                return (double)WinSecurityLevel.Stable;
            }

            if (intent.PrimaryIntent == DecisionIntentKind.TakeScore && context.TrickScore >= 5)
                return (double)WinSecurityLevel.Stable;

            return (double)WinSecurityLevel.Fragile;
        }

        private static double ResolveSecurityBucket(ScoredAction action, bool hasFloorCandidate, double requiredSecurityFloor)
        {
            double winSecurity = action.Features.GetValueOrDefault("WinSecurityValue");
            if (!hasFloorCandidate)
                return winSecurity;

            return winSecurity >= requiredSecurityFloor ? 1 : 0;
        }

        private static double ResolveControlSpendCost(ScoredAction action)
        {
            return action.Features.GetValueOrDefault("TrumpConsumptionCost") * 1.4 +
                action.Features.GetValueOrDefault("HighControlLossCost") * 1.2 +
                action.Features.GetValueOrDefault("InfoLeakCost") * 0.6;
        }

        private double ResolveContestBottomValue(RuleAIContext context, List<Card> candidate)
        {
            if (context.DecisionFrame.BottomContestPressure < RiskLevel.Medium)
                return 0;

            if (context.DecisionFrame.EndgameLevel == EndgameLevel.None)
                return 0;

            if (candidate.Count == 0)
                return 0;

            int multiplier = ResolveBottomMultiplier(candidate);
            return multiplier;
        }

        private int ResolveBottomMultiplier(List<Card> cards)
        {
            var pattern = new CardPattern(cards, _config);

            if (pattern.Type == PatternType.Tractor)
            {
                int pairCount = cards.Count / 2;
                return (int)System.Math.Pow(2, pairCount);
            }

            if (pattern.Type == PatternType.Pair)
                return 4;

            return 2;
        }

        private string ResolveSystemKey(List<Card> candidate)
        {
            if (candidate.Count == 0)
                return "Unknown";

            return _config.IsTrump(candidate[0]) ? "Trump" : $"Suit:{candidate[0].Suit}";
        }

        private string BuildReasonCode(
            RuleAIContext context,
            ResolvedIntent intent,
            List<Card> candidate,
            Dictionary<string, double> features)
        {
            return intent.PrimaryIntent switch
            {
                DecisionIntentKind.PassToMate when features.GetValueOrDefault("MateSyncValue") > 1.0 => "pass_to_mate_send_points",
                DecisionIntentKind.PassToMate => "pass_to_mate_keep_power",
                DecisionIntentKind.TakeScore when features.GetValueOrDefault("TrickWinValue") > 0 => "cheap_overtake_with_acceptable_structure_loss",
                DecisionIntentKind.TakeScore => "take_score_but_cannot_secure",
                DecisionIntentKind.ProtectBottom => "protect_bottom_keep_last_control",
                DecisionIntentKind.PrepareEndgame when candidate.Count == context.MyHand.Count && candidate.Count >= 3 => "endgame_safe_throw_closeout",
                DecisionIntentKind.PrepareEndgame => "prepare_endgame_preserve_control",
                DecisionIntentKind.ShapeHand => "shape_hand_create_void",
                DecisionIntentKind.PrepareThrow when candidate.Count >= 3 => "safe_throw_candidate",
                DecisionIntentKind.MinimizeLoss => "loss_cut_keep_control",
                _ when context.Phase == PhaseKind.BuryBottom => "bury_preserve_structure",
                _ when context.Phase == PhaseKind.Lead => "lead_best_control",
                _ => "balanced_select"
            };
        }
    }
}
