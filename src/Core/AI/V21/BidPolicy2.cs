using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 亮主/反主策略 2.0，保留原有强信号与“提前亮主碰运气”能力，并接入统一解释结构。
    /// </summary>
    public sealed class BidPolicy2
    {
        public const string ReasonLuck = "LUCK";
        public const string ReasonC1 = "C1";
        public const string ReasonC2 = "C2";
        public const string ReasonC3 = "C3";
        public const int EarlyStageMaxRoundIndex = 8;
        public const int MidStageMaxRoundIndex = 18;

        private readonly Random _rng;

        public double RoundLuckProbability { get; }

        public BidPolicy2(int seed = 0, double minLuck = 0.1, double maxLuck = 0.3)
        {
            if (minLuck > maxLuck)
            {
                var temp = minLuck;
                minLuck = maxLuck;
                maxLuck = temp;
            }

            minLuck = Clamp(minLuck, 0, 1);
            maxLuck = Clamp(maxLuck, 0, 1);
            _rng = seed != 0 ? new Random(seed) : new Random();
            RoundLuckProbability = minLuck + _rng.NextDouble() * (maxLuck - minLuck);
        }

        public BidPolicy2Decision Decide(RuleAIContext context)
        {
            if (context == null || context.MyHand.Count == 0)
                return EmptyDecision();

            var candidates = BuildCandidates(context);
            if (candidates.Count == 0)
                return EmptyDecision();

            var best = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.LevelCards.Count)
                .ThenBy(candidate => candidate.Suit)
                .First();

            bool usedLuck = false;
            List<Card> attempt = new();
            if (best.MaxBidPriority > context.CurrentBidPriority)
            {
                if (ShouldBidDeterministically(best, context))
                {
                    attempt = BuildAttempt(best, context.CurrentBidPriority);
                }
                else if (IsEarlyStage(context.BidRoundIndex) && ShouldTakeEarlyLuck(best, context))
                {
                    usedLuck = true;
                    attempt = BuildAttempt(best, context.CurrentBidPriority);
                }
            }

            var reasons = new List<string>();
            if (usedLuck)
                reasons.Add(ReasonLuck);
            if (best.C1)
                reasons.Add(ReasonC1);
            if (best.C2)
                reasons.Add(ReasonC2);
            if (best.C3)
                reasons.Add(ReasonC3);

            string? primaryReason = usedLuck ? ReasonLuck : reasons.FirstOrDefault();
            var explanation = new DecisionExplanation
            {
                Phase = PhaseKind.Bid,
                PhasePolicy = "BidPolicy2",
                PrimaryIntent = attempt.Count > 0 ? DecisionIntentKind.TakeLead.ToString() : DecisionIntentKind.SaveControl.ToString(),
                SecondaryIntent = DecisionIntentKind.SaveControl.ToString(),
                SelectedReason = primaryReason ?? "no_bid",
                CandidateCount = candidates.Count,
                TopCandidates = candidates.Take(3).Select(candidate => candidate.Suit.ToString()).ToList(),
                TopScores = candidates.Take(3).Select(candidate => Math.Round(candidate.Score, 4)).ToList(),
                SelectedAction = attempt.Select(card => card.ToString()).ToList(),
                Tags = new List<string> { ResolveStage(best.RoundIndex).ToString(), usedLuck ? "luck" : "deterministic" }
            };

            return new BidPolicy2Decision
            {
                AttemptCards = attempt,
                PrimaryReason = primaryReason,
                Reasons = reasons,
                UsedLuck = usedLuck,
                RoundLuckProbability = RoundLuckProbability,
                CandidateScore = best.Score,
                Stage = ResolveStage(best.RoundIndex).ToString(),
                CandidateSuit = best.Suit.ToString(),
                CandidatePriority = best.MaxBidPriority,
                TrumpCount = best.TrumpCount,
                PairUnits = best.PairUnits,
                HasTractor = best.HasTractor,
                Explanation = explanation
            };
        }

        private List<SuitCandidate> BuildCandidates(RuleAIContext context)
        {
            var suitCandidates = context.MyHand
                .Where(card => card.Rank == context.RuleProfile.LevelRank && card.Suit != Suit.Joker)
                .GroupBy(card => card.Suit)
                .Select(group => EvaluateSuit(context, group.Key, group.ToList()))
                .ToList();

            var jokerCandidates = context.MyHand
                .Where(card => card.IsJoker)
                .GroupBy(card => card.Rank)
                .Where(group => group.Count() >= 2)
                .Select(group => EvaluateNoTrump(context, group.Key, group.Take(2).ToList()))
                .ToList();

            return suitCandidates
                .Concat(jokerCandidates)
                .ToList();
        }

        private SuitCandidate EvaluateSuit(RuleAIContext context, Suit suit, List<Card> levelCards)
        {
            var config = new GameConfig
            {
                LevelRank = context.RuleProfile.LevelRank,
                TrumpSuit = suit
            };

            var visible = context.MyHand;
            var trumpCards = visible.Where(config.IsTrump).ToList();
            int visibleCount = visible.Count;
            int trumpCount = trumpCards.Count;
            int pairUnits = trumpCards.GroupBy(card => card).Sum(group => group.Count() / 2);
            bool hasTractor = HasTractor(config, trumpCards);
            int jokerCount = visible.Count(card => card.IsJoker);

            bool c1 = pairUnits >= 2 || hasTractor;
            bool c2 = trumpCount > visibleCount / 2.0;
            bool c3 = trumpCount > visibleCount / 3.0;

            bool dealerSide = context.PlayerIndex >= 0 && context.DealerIndex >= 0
                ? context.PlayerIndex % 2 == context.DealerIndex % 2
                : context.Role != AIRole.Opponent;
            bool hasBid = context.CurrentBidPlayer >= 0;
            bool sameSideBid = hasBid && context.PlayerIndex >= 0 && context.CurrentBidPlayer % 2 == context.PlayerIndex % 2;
            bool oppositeSideBid = hasBid && !sameSideBid;

            double ratio = trumpCount / (double)Math.Max(1, visibleCount);
            double score = 0;
            score += c1 ? 1.00 : 0.00;
            score += hasTractor ? 0.45 : 0.00;
            score += c2 ? 1.20 : 0.00;
            score += c3 ? 0.80 : 0.00;
            score += Math.Min(0.80, pairUnits * 0.20);
            score += ratio * 0.70;
            score += levelCards.Count * 0.30;
            score += jokerCount * 0.15;
            score += dealerSide ? 0.12 : 0.05;
            score += oppositeSideBid ? 0.25 : 0.00;
            score -= sameSideBid ? 0.15 : 0.00;
            score += ResolveStage(context.BidRoundIndex) switch
            {
                BidStage.Early => -0.20,
                BidStage.Mid => 0.00,
                _ => 0.20
            };

            return new SuitCandidate(
                suit,
                levelCards.OrderByDescending(card => card.Rank).ToList(),
                Math.Min(1, levelCards.Count - 1),
                c1,
                c2,
                c3,
                trumpCount,
                pairUnits,
                hasTractor,
                context.BidRoundIndex,
                score);
        }

        private SuitCandidate EvaluateNoTrump(RuleAIContext context, Rank jokerRank, List<Card> jokerCards)
        {
            var config = new GameConfig
            {
                LevelRank = context.RuleProfile.LevelRank,
                TrumpSuit = null
            };

            var visible = context.MyHand;
            var trumpCards = visible.Where(config.IsTrump).ToList();
            int visibleCount = visible.Count;
            int trumpCount = trumpCards.Count;
            int pairUnits = trumpCards.GroupBy(card => card).Sum(group => group.Count() / 2);
            bool hasTractor = HasTractor(config, trumpCards);

            bool c1 = true;
            bool c2 = trumpCount > visibleCount / 3.0;
            bool c3 = jokerRank == Rank.BigJoker || trumpCount >= 4;

            bool dealerSide = context.PlayerIndex >= 0 && context.DealerIndex >= 0
                ? context.PlayerIndex % 2 == context.DealerIndex % 2
                : context.Role != AIRole.Opponent;
            bool hasBid = context.CurrentBidPlayer >= 0;
            bool sameSideBid = hasBid && context.PlayerIndex >= 0 && context.CurrentBidPlayer % 2 == context.PlayerIndex % 2;
            bool oppositeSideBid = hasBid && !sameSideBid;

            double ratio = trumpCount / (double)Math.Max(1, visibleCount);
            double score = 0;
            score += 1.35;
            score += jokerRank == Rank.BigJoker ? 0.55 : 0.25;
            score += c2 ? 0.90 : 0.00;
            score += c3 ? 0.45 : 0.00;
            score += hasTractor ? 0.25 : 0.00;
            score += Math.Min(0.60, pairUnits * 0.15);
            score += ratio * 0.60;
            score += dealerSide ? 0.10 : 0.04;
            score += oppositeSideBid ? 0.35 : 0.00;
            score -= sameSideBid ? 0.12 : 0.00;
            score += ResolveStage(context.BidRoundIndex) switch
            {
                BidStage.Early => -0.15,
                BidStage.Mid => 0.00,
                _ => 0.15
            };

            int maxBidPriority = jokerRank == Rank.BigJoker ? 3 : 2;
            return new SuitCandidate(
                Suit.Joker,
                jokerCards.OrderByDescending(card => card.Rank).ToList(),
                maxBidPriority,
                c1,
                c2,
                c3,
                trumpCount,
                pairUnits,
                hasTractor,
                context.BidRoundIndex,
                score);
        }

        private bool ShouldBidDeterministically(SuitCandidate candidate, RuleAIContext context)
        {
            bool hasCoreSignal = candidate.C1 || candidate.C2 || candidate.C3;
            if (!hasCoreSignal)
                return false;

            bool hasBid = context.CurrentBidPlayer >= 0;
            bool sameSideBid = hasBid && context.PlayerIndex >= 0 && context.CurrentBidPlayer % 2 == context.PlayerIndex % 2;
            bool oppositeSideBid = hasBid && !sameSideBid;

            double threshold = ResolveStage(context.BidRoundIndex) switch
            {
                BidStage.Early => 1.45,
                BidStage.Mid => 1.20,
                _ => 0.95
            };

            if (candidate.MaxBidPriority >= 1)
                threshold -= 0.20;
            if (oppositeSideBid)
                threshold -= 0.10;
            if (sameSideBid)
                threshold += 0.10;

            return candidate.Score >= threshold;
        }

        private bool ShouldTakeEarlyLuck(SuitCandidate candidate, RuleAIContext context)
        {
            int remainingOps = Math.Max(1, EarlyStageMaxRoundIndex - Math.Max(0, context.BidRoundIndex) + 1);
            double pStep = 1.0 - Math.Pow(1.0 - RoundLuckProbability, 1.0 / remainingOps);
            double quality = Clamp((candidate.Score + 0.4) / 2.2, 0.35, 1.0);
            double styleBoost = Clamp(context.StyleProfile.EarlyBidLuck / 0.3, 0.5, 1.2);
            double finalP = Clamp(pStep * quality * styleBoost, 0, 1);
            return _rng.NextDouble() < finalP;
        }

        private static List<Card> BuildAttempt(SuitCandidate candidate, int currentBidPriority)
        {
            int targetCount;
            if (candidate.Suit == Suit.Joker)
            {
                targetCount = 2;
            }
            else
            {
                targetCount = candidate.MaxBidPriority >= 1 && currentBidPriority < 1 ? 2 : 1;
            }

            if (candidate.LevelCards.Count < targetCount)
                targetCount = candidate.LevelCards.Count;
            return targetCount <= 0 ? new List<Card>() : candidate.LevelCards.Take(targetCount).ToList();
        }

        private static bool HasTractor(GameConfig config, List<Card> trumpCards)
        {
            var pairReps = trumpCards
                .GroupBy(card => card)
                .Where(group => group.Count() >= 2)
                .Select(group => group.Key)
                .ToList();

            for (int i = 0; i < pairReps.Count; i++)
            {
                for (int j = i + 1; j < pairReps.Count; j++)
                {
                    var candidate = new List<Card> { pairReps[i], pairReps[i], pairReps[j], pairReps[j] };
                    if (new CardPattern(candidate, config).IsTractor(candidate))
                        return true;
                }
            }

            return false;
        }

        private static BidStage ResolveStage(int roundIndex)
        {
            if (roundIndex <= EarlyStageMaxRoundIndex)
                return BidStage.Early;
            if (roundIndex <= MidStageMaxRoundIndex)
                return BidStage.Mid;
            return BidStage.Late;
        }

        private static bool IsEarlyStage(int roundIndex) => ResolveStage(roundIndex) == BidStage.Early;

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private static BidPolicy2Decision EmptyDecision()
        {
            return new BidPolicy2Decision
            {
                AttemptCards = new List<Card>(),
                Reasons = new List<string>(),
                Explanation = new DecisionExplanation
                {
                    Phase = PhaseKind.Bid,
                    PhasePolicy = "BidPolicy2",
                    PrimaryIntent = DecisionIntentKind.SaveControl.ToString(),
                    SecondaryIntent = DecisionIntentKind.MinimizeLoss.ToString(),
                    SelectedReason = "no_bid"
                }
            };
        }

        public sealed class BidPolicy2Decision
        {
            public List<Card> AttemptCards { get; init; } = new();
            public string? PrimaryReason { get; init; }
            public List<string> Reasons { get; init; } = new();
            public bool UsedLuck { get; init; }
            public double RoundLuckProbability { get; init; }
            public double CandidateScore { get; init; }
            public string Stage { get; init; } = "unknown";
            public string? CandidateSuit { get; init; }
            public int CandidatePriority { get; init; }
            public int TrumpCount { get; init; }
            public int PairUnits { get; init; }
            public bool HasTractor { get; init; }
            public DecisionExplanation Explanation { get; init; } = new();
        }

        private enum BidStage
        {
            Early,
            Mid,
            Late
        }

        private sealed record SuitCandidate(
            Suit Suit,
            List<Card> LevelCards,
            int MaxBidPriority,
            bool C1,
            bool C2,
            bool C3,
            int TrumpCount,
            int PairUnits,
            bool HasTractor,
            int RoundIndex,
            double Score);
    }
}
