using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// V30 统一上下文。
    /// </summary>
    public sealed class RuleAIContextV30
    {
        public PhaseKindV30 Phase { get; init; } = PhaseKindV30.Unknown;

        public AIRole Role { get; init; } = AIRole.Opponent;

        public AIDifficulty Difficulty { get; init; } = AIDifficulty.Medium;

        public int PlayerIndex { get; init; } = -1;

        public int DealerIndex { get; init; } = -1;

        public List<Card> MyHand { get; init; } = new();

        public List<List<Card>> LegalActions { get; init; } = new();

        public List<Card> LeadCards { get; init; } = new();

        public List<Card> CurrentWinningCards { get; init; } = new();

        public List<Card> VisibleBottomCards { get; init; } = new();

        public GameConfig GameConfig { get; init; } = new();

        public V30FeatureFlags FeatureFlags { get; init; } = V30FeatureFlags.Default;

        public HandProfileV30 HandProfile { get; init; } = new();

        public MemorySnapshotV30 MemorySnapshot { get; init; } = new();

        public DecisionFrameV30 DecisionFrame { get; init; } = new();

        public int HandCount => MyHand.Count;

        public bool IsDealerSide => Role == AIRole.Dealer || Role == AIRole.DealerPartner;

        public bool PartnerWinning => DecisionFrame.PartnerWinning;

        public int TrickScore => DecisionFrame.CurrentTrickScore;

        public int CardsLeftMin => DecisionFrame.CardsLeftMin;

        public int TrumpCount => HandProfile.TrumpCount;

        public int PointCardCount => HandProfile.ScoreCardCount;

        public int HandPointScore => MyHand.Sum(card => card.Score);
    }
}
