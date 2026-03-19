using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Bottom
{
    public enum BottomScoreBandV30
    {
        Low = 0,
        Medium = 1,
        High = 2
    }

    public enum BottomOperationalModeV30
    {
        NormalOperation = 0,
        ProtectBottomAttention = 1,
        StrongProtectBottom = 2
    }

    public enum BottomContestModeV30
    {
        NormalContest = 0,
        ContestBottomAttention = 1,
        StrongContestBottom = 2
    }

    public enum PlanStabilityV30
    {
        Fragile = 0,
        Stable = 1,
        Lock = 2
    }

    public enum BottomPlanGoalV30
    {
        NoBottomLine = 0,
        SingleBottomPreferred = 1,
        DoubleBottomPreferred = 2
    }

    public enum BottomScoreSignalTypeV30
    {
        SuitExhaustedScoreUnseen = 0,
        MultiSuitExhaustedScoreUnseen = 1,
        ExplicitHighBottomEvidence = 2
    }

    public enum WinSecurityTierV30
    {
        FragileWin = 0,
        StableWin = 1,
        LockWin = 2
    }

    public sealed class BottomModeInputV30
    {
        public AIRole Role { get; init; } = AIRole.Opponent;

        public int DefenderScore { get; init; }

        public int BottomPoints { get; init; }

        public int RemainingContestableScore { get; init; }

        public int EstimatedBottomPoints { get; init; } = 10;

        public int BottomMultiplier { get; init; } = 2;
    }

    public sealed class BottomModeDecisionV30
    {
        public BottomScoreBandV30 BottomScoreBand { get; init; }

        public BottomOperationalModeV30 OperationalMode { get; init; }

        public BottomContestModeV30 ContestMode { get; init; }
    }

    public sealed class BottomScoreSignalV30
    {
        public BottomScoreSignalTypeV30 SignalType { get; init; }

        public double Confidence { get; init; } = 1.0;

        public int SuggestedPoints { get; init; }
    }

    public sealed class BottomPlanInputV30
    {
        public int DefenderScore { get; init; }

        public int SingleBottomGainPoints { get; init; }

        public int DoubleBottomGainPoints { get; init; }

        public PlanStabilityV30 SinglePlanStability { get; init; } = PlanStabilityV30.Stable;

        public PlanStabilityV30 DoublePlanStability { get; init; } = PlanStabilityV30.Fragile;
    }

    public sealed class BottomPlanDecisionV30
    {
        public BottomPlanGoalV30 Goal { get; init; }

        public bool CanWinWithSingleBottom { get; init; }

        public bool CanWinWithDoubleBottom { get; init; }

        public bool ShouldPreservePairsAndTractors { get; init; }

        public string Reason { get; init; } = string.Empty;
    }

    public sealed class JokerControlInputV30
    {
        public bool BigJokerUnplayedLikelyInRearOpponent { get; init; }

        public bool RearOpponentLikelyHasStrongerTrumpStructure { get; init; }

        public WinSecurityTierV30 SmallJokerSecurity { get; init; } = WinSecurityTierV30.StableWin;
    }

    public sealed class JokerControlDecisionV30
    {
        public bool ShouldPlaySmallJokerFirst { get; init; }

        public string Reason { get; init; } = string.Empty;
    }

    public sealed class EndgameControlDecisionV30
    {
        public bool FreezeTrumpResources { get; init; }

        public bool AllowConcedeLowPointTrick { get; init; }
    }

    public sealed class EndgameControlInputV30
    {
        public BottomOperationalModeV30 OperationalMode { get; init; } = BottomOperationalModeV30.NormalOperation;

        public int CurrentTrickPoints { get; init; }

        public IReadOnlyList<Card> TrumpCards { get; init; } = new List<Card>();
    }
}
