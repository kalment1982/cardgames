using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Follow
{
    public enum FollowOverlayIntentV30
    {
        PassToMate,
        TakeLead,
        TakeScore,
        PrepareEndgame,
        MinimizeLoss
    }

    public enum FollowWinSecurityV30
    {
        Fragile,
        Stable,
        Lock
    }

    public sealed class FollowCandidateViewV30
    {
        public List<Card> Cards { get; init; } = new();
        public bool CanBeatCurrentWinner { get; init; }
        public FollowWinSecurityV30 Security { get; init; } = FollowWinSecurityV30.Fragile;
        public int OverlayScore { get; init; }
        public int ControlSpendCost { get; init; }
        public int CandidatePoints { get; init; }
        public int DiscardStrengthCost { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public sealed class FollowDecisionV30
    {
        public List<Card> SelectedCards { get; init; } = new();
        public FollowOverlayIntentV30 Intent { get; init; } = FollowOverlayIntentV30.MinimizeLoss;
        public List<FollowCandidateViewV30> RankedCandidates { get; init; } = new();
        public string SelectedReason { get; init; } = string.Empty;
    }
}
