using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.Bidding
{
    /// <summary>
    /// 亮主/反主决策策略（AI层），仅生成“尝试亮主牌”。
    /// 合法性与优先级仍由 TrumpBidding/Game.CanBidTrumpEx 校验。
    /// </summary>
    public sealed class BidPolicy
    {
        public const string ReasonLuck = "LUCK";
        public const string ReasonC1 = "C1";
        public const string ReasonC2 = "C2";
        public const string ReasonC3 = "C3";
        public const int EarlyStageMaxRoundIndex = 8;   // 前期
        public const int MidStageMaxRoundIndex = 18;    // 中期

        private readonly Random _rng;
        public double RoundLuckProbability { get; }

        public BidPolicy(int seed = 0, double minLuck = 0.1, double maxLuck = 0.3)
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

        public sealed class DecisionContext
        {
            public int PlayerIndex { get; init; }
            public int DealerIndex { get; init; }
            public Rank LevelRank { get; init; }
            public List<Card> VisibleCards { get; init; } = new();
            public int RoundIndex { get; init; }         // 0-based
            public int CurrentBidPriority { get; init; } // -1/0/1/2
            public int CurrentBidPlayer { get; init; }   // -1 表示无人亮主
        }

        public sealed class BidDecision
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

            public Dictionary<string, object?> ToLogDetail()
            {
                return new Dictionary<string, object?>
                {
                    ["bid_reason"] = PrimaryReason,
                    ["bid_reasons"] = Reasons,
                    ["bid_used_luck"] = UsedLuck,
                    ["round_luck_p"] = Math.Round(RoundLuckProbability, 4),
                    ["bid_score"] = Math.Round(CandidateScore, 4),
                    ["bid_stage"] = Stage,
                    ["bid_candidate_suit"] = CandidateSuit,
                    ["bid_candidate_priority"] = CandidatePriority,
                    ["bid_trump_count"] = TrumpCount,
                    ["bid_pair_units"] = PairUnits,
                    ["bid_has_tractor"] = HasTractor
                };
            }
        }

        public List<Card> SelectBidAttempt(DecisionContext context)
        {
            return Decide(context).AttemptCards;
        }

        public BidDecision Decide(DecisionContext context)
        {
            if (context == null || context.VisibleCards == null || context.VisibleCards.Count == 0)
                return EmptyDecision();

            var candidates = BuildCandidates(context);
            if (candidates.Count == 0)
                return EmptyDecision();

            var best = candidates
                .OrderByDescending(candidate => candidate.Score)
                .ThenByDescending(candidate => candidate.LevelCards.Count)
                .ThenBy(candidate => candidate.Suit)
                .First();

            if (best.MaxBidPriority <= context.CurrentBidPriority)
                return CreateDecision(best, new List<Card>(), usedLuck: false);

            if (ShouldBidDeterministically(best, context))
                return CreateDecision(best, BuildAttempt(best, context.CurrentBidPriority), usedLuck: false);

            if (IsEarlyStage(context.RoundIndex) && ShouldTakeEarlyLuck(best, context.RoundIndex))
                return CreateDecision(best, BuildAttempt(best, context.CurrentBidPriority), usedLuck: true);

            return CreateDecision(best, new List<Card>(), usedLuck: false);
        }

        private List<SuitCandidate> BuildCandidates(DecisionContext context)
        {
            var grouped = context.VisibleCards
                .Where(card => card.Rank == context.LevelRank && card.Suit != Suit.Joker)
                .GroupBy(card => card.Suit)
                .ToList();

            var candidates = new List<SuitCandidate>();
            foreach (var group in grouped)
            {
                var levelCards = group.ToList();
                if (levelCards.Count == 0)
                    continue;

                var suit = group.Key;
                var evaluation = EvaluateSuit(context, suit, levelCards);
                candidates.Add(evaluation);
            }

            return candidates;
        }

        private SuitCandidate EvaluateSuit(DecisionContext context, Suit suit, List<Card> levelCards)
        {
            var config = new GameConfig
            {
                LevelRank = context.LevelRank,
                TrumpSuit = suit
            };

            var visible = context.VisibleCards;
            var trumpCards = visible.Where(config.IsTrump).ToList();
            int visibleCount = visible.Count;
            int trumpCount = trumpCards.Count;
            int pairUnits = trumpCards.GroupBy(card => card).Sum(group => group.Count() / 2);
            bool hasTractor = HasTractor(config, trumpCards);
            int jokerCount = visible.Count(card => card.IsJoker);

            bool c1 = pairUnits >= 2 || hasTractor;              // 多对对子/拖拉机
            bool c2 = trumpCount > visibleCount / 2.0;           // 主牌过半
            bool c3 = trumpCount > visibleCount / 3.0;           // 主牌超过平均（含王与级牌）

            bool dealerSide = context.PlayerIndex % 2 == context.DealerIndex % 2;
            bool hasBid = context.CurrentBidPlayer >= 0;
            bool sameSideBid = hasBid && context.CurrentBidPlayer % 2 == context.PlayerIndex % 2;
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

            var stage = ResolveStage(context.RoundIndex);
            score += stage switch
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
                context.RoundIndex,
                score);
        }

        private bool ShouldBidDeterministically(SuitCandidate candidate, DecisionContext context)
        {
            bool hasCoreSignal = candidate.C1 || candidate.C2 || candidate.C3;
            if (!hasCoreSignal)
                return false;

            bool hasBid = context.CurrentBidPlayer >= 0;
            bool sameSideBid = hasBid && context.CurrentBidPlayer % 2 == context.PlayerIndex % 2;
            bool oppositeSideBid = hasBid && !sameSideBid;

            double threshold = ResolveStage(context.RoundIndex) switch
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

        private bool ShouldTakeEarlyLuck(SuitCandidate candidate, int roundIndex)
        {
            // 早期“碰运气”受牌力调制，弱牌不盲目亮。
            int remainingOps = Math.Max(1, EarlyStageMaxRoundIndex - Math.Max(0, roundIndex) + 1);
            double pStep = 1.0 - Math.Pow(1.0 - RoundLuckProbability, 1.0 / remainingOps);

            double quality = Clamp((candidate.Score + 0.4) / 2.2, 0.35, 1.0);
            double finalP = Clamp(pStep * quality, 0, 1);
            return _rng.NextDouble() < finalP;
        }

        private static List<Card> BuildAttempt(SuitCandidate candidate, int currentBidPriority)
        {
            int targetCount = candidate.MaxBidPriority >= 1 && currentBidPriority < 1 ? 2 : 1;
            if (candidate.LevelCards.Count < targetCount)
                targetCount = candidate.LevelCards.Count;
            if (targetCount <= 0)
                return new List<Card>();

            return candidate.LevelCards.Take(targetCount).ToList();
        }

        private static bool HasTractor(GameConfig config, List<Card> trumpCards)
        {
            var pairReps = trumpCards
                .GroupBy(card => card)
                .Where(group => group.Count() >= 2)
                .Select(group => group.Key)
                .ToList();

            if (pairReps.Count < 2)
                return false;

            // 拖拉机至少由两对组成，优先检查两对是否成拖。
            for (int i = 0; i < pairReps.Count; i++)
            {
                for (int j = i + 1; j < pairReps.Count; j++)
                {
                    var candidate = new List<Card>
                    {
                        pairReps[i], pairReps[i],
                        pairReps[j], pairReps[j]
                    };
                    var pattern = new CardPattern(candidate, config);
                    if (pattern.IsTractor(candidate))
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

        private BidDecision CreateDecision(SuitCandidate candidate, List<Card> attemptCards, bool usedLuck)
        {
            var reasons = new List<string>();
            if (usedLuck)
                reasons.Add(ReasonLuck);
            if (candidate.C1)
                reasons.Add(ReasonC1);
            if (candidate.C2)
                reasons.Add(ReasonC2);
            if (candidate.C3)
                reasons.Add(ReasonC3);

            string? primaryReason = usedLuck
                ? ReasonLuck
                : reasons.FirstOrDefault();

            return new BidDecision
            {
                AttemptCards = attemptCards,
                PrimaryReason = primaryReason,
                Reasons = reasons,
                UsedLuck = usedLuck,
                RoundLuckProbability = RoundLuckProbability,
                CandidateScore = candidate.Score,
                Stage = ResolveStage(candidate.RoundIndex).ToString(),
                CandidateSuit = candidate.Suit.ToString(),
                CandidatePriority = candidate.MaxBidPriority,
                TrumpCount = candidate.TrumpCount,
                PairUnits = candidate.PairUnits,
                HasTractor = candidate.HasTractor
            };
        }

        private BidDecision EmptyDecision()
        {
            return new BidDecision
            {
                RoundLuckProbability = RoundLuckProbability
            };
        }

        private static double Clamp(double value, double min, double max)
        {
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        private enum BidStage
        {
            Early,
            Mid,
            Late
        }

        private sealed class SuitCandidate
        {
            public Suit Suit { get; }
            public List<Card> LevelCards { get; }
            public int MaxBidPriority { get; } // 0=单, 1=对
            public bool C1 { get; }
            public bool C2 { get; }
            public bool C3 { get; }
            public int TrumpCount { get; }
            public int PairUnits { get; }
            public bool HasTractor { get; }
            public double Score { get; }
            public int RoundIndex { get; }

            public SuitCandidate(
                Suit suit,
                List<Card> levelCards,
                int maxBidPriority,
                bool c1,
                bool c2,
                bool c3,
                int trumpCount,
                int pairUnits,
                bool hasTractor,
                int roundIndex,
                double score)
            {
                Suit = suit;
                LevelCards = levelCards;
                MaxBidPriority = maxBidPriority;
                C1 = c1;
                C2 = c2;
                C3 = c3;
                TrumpCount = trumpCount;
                PairUnits = pairUnits;
                HasTractor = hasTractor;
                RoundIndex = roundIndex;
                Score = score;
            }
        }
    }
}
