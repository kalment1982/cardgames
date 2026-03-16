using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 规则 AI 2.1 的统一上下文。
    /// </summary>
    public sealed class RuleAIContext
    {
        public PhaseKind Phase { get; init; } = PhaseKind.Unknown;

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

        public RuleProfile RuleProfile { get; init; } = new();

        public DifficultyProfile DifficultyProfile { get; init; } = new();

        public StyleProfile StyleProfile { get; init; } = new();

        public HandProfile HandProfile { get; init; } = new();

        public MemorySnapshot MemorySnapshot { get; init; } = new();

        public InferenceSnapshot InferenceSnapshot { get; init; } = new();

        public DecisionFrame DecisionFrame { get; init; } = new();

        public int BidRoundIndex { get; init; }

        public int CurrentBidPriority { get; init; } = -1;

        public int CurrentBidPlayer { get; init; } = -1;

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
