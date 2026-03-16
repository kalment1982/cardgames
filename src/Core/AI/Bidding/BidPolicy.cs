using System.Collections.Generic;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.Bidding
{
    /// <summary>
    /// 亮主/反主决策兼容入口，底层委托给 BidPolicy2。
    /// </summary>
    public sealed class BidPolicy
    {
        public const string ReasonLuck = BidPolicy2.ReasonLuck;
        public const string ReasonC1 = BidPolicy2.ReasonC1;
        public const string ReasonC2 = BidPolicy2.ReasonC2;
        public const string ReasonC3 = BidPolicy2.ReasonC3;
        public const int EarlyStageMaxRoundIndex = BidPolicy2.EarlyStageMaxRoundIndex;
        public const int MidStageMaxRoundIndex = BidPolicy2.MidStageMaxRoundIndex;

        private readonly BidPolicy2 _policy2;

        public double RoundLuckProbability => _policy2.RoundLuckProbability;

        public BidPolicy(int seed = 0, double minLuck = 0.1, double maxLuck = 0.3)
        {
            _policy2 = new BidPolicy2(seed, minLuck, maxLuck);
        }

        public sealed class DecisionContext
        {
            public int PlayerIndex { get; init; }
            public int DealerIndex { get; init; }
            public Rank LevelRank { get; init; }
            public List<Card> VisibleCards { get; init; } = new();
            public int RoundIndex { get; init; }
            public int CurrentBidPriority { get; init; }
            public int CurrentBidPlayer { get; init; }
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
                    ["round_luck_p"] = System.Math.Round(RoundLuckProbability, 4),
                    ["bid_score"] = System.Math.Round(CandidateScore, 4),
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
            if (context == null)
                return new BidDecision();

            var builder = new RuleAIContextBuilder(new GameConfig { LevelRank = context.LevelRank });
            var ruleContext = builder.BuildBidContext(
                context.VisibleCards,
                ResolveRole(context.PlayerIndex, context.DealerIndex),
                playerIndex: context.PlayerIndex,
                dealerIndex: context.DealerIndex,
                roundIndex: context.RoundIndex,
                currentBidPriority: context.CurrentBidPriority,
                currentBidPlayer: context.CurrentBidPlayer);

            var decision = _policy2.Decide(ruleContext);
            return new BidDecision
            {
                AttemptCards = decision.AttemptCards,
                PrimaryReason = decision.PrimaryReason,
                Reasons = decision.Reasons,
                UsedLuck = decision.UsedLuck,
                RoundLuckProbability = decision.RoundLuckProbability,
                CandidateScore = decision.CandidateScore,
                Stage = decision.Stage,
                CandidateSuit = decision.CandidateSuit,
                CandidatePriority = decision.CandidatePriority,
                TrumpCount = decision.TrumpCount,
                PairUnits = decision.PairUnits,
                HasTractor = decision.HasTractor
            };
        }

        private static AIRole ResolveRole(int playerIndex, int dealerIndex)
        {
            if (playerIndex == dealerIndex)
                return AIRole.Dealer;

            return playerIndex % 2 == dealerIndex % 2
                ? AIRole.DealerPartner
                : AIRole.Opponent;
        }
    }
}
