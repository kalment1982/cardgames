using System.Collections.Generic;
using TractorGame.Core.AI.V21;

namespace TractorGame.Core.AI.V30.Lead
{
    public enum LeadRoleV30
    {
        Dealer,
        DealerPartner,
        Opponent
    }

    public enum LeadDecisionIntentV30
    {
        SafeThrow,
        StableSideSuitRun,
        ForceTrump,
        HandOffToMate,
        PrepareFutureThrow,
        BuildVoid,
        ProbeWeakSuit
    }

    public enum PairControlLevelV30
    {
        None,
        HighPairControl,
        LowPairConsume
    }

    public sealed class LeadContextV30
    {
        public LeadRoleV30 Role { get; init; } = LeadRoleV30.Opponent;
        public int TrickIndex { get; init; }

        public bool HasSafeThrowPlan { get; init; }
        public int SafeThrowExpectedScore { get; init; }
        public int SafeThrowFutureValue { get; init; }

        public bool HasStableSideSuitRun { get; init; }
        public int StableSideSuitFutureValue { get; init; }
        public bool HasLostSuitControl { get; init; }

        public bool HasStrongScoreSideLead { get; init; }
        public int StrongScoreSideLeadFutureValue { get; init; }

        public bool HasTeamSideSuitRun { get; init; }
        public int TeamSideSuitFutureValue { get; init; }
        public bool KeyOpponentLikelyNotVoid { get; init; }

        public bool HasProfitableForceTrump { get; init; }
        public int ForceTrumpFutureValue { get; init; }

        public bool HasClearOwnFollowUpLine { get; init; }
        public bool MateHasPositiveTakeoverEvidence { get; init; }

        public bool HasFutureThrowPlan { get; init; }
        public PairControlLevelV30 ThreePairControlLevel { get; init; } = PairControlLevelV30.None;

        public bool HasForceTrumpForThrowPlan { get; init; }
        public int FutureThrowExpectedScore { get; init; }
        public int TrumpCountAfterForceTrump { get; init; }
        public bool KeepsControlTrumpAfterForceTrump { get; init; }
        public bool KeepsTrumpPairAfterForceTrump { get; init; }
        public bool IsProtectBottomMode { get; init; }

        public bool HasVoidBuildPlan { get; init; }
        public bool VoidBreaksOnlyWeakNonScorePairs { get; init; }
        public bool HasExplicitVoidFollowUpBenefit { get; init; }
        public int VoidPlanFutureValue { get; init; }

        public bool HasProbePlan { get; init; } = true;
        public int ProbeFutureValue { get; init; }

        public LeadLineStateV30? LineState { get; init; }
        public EndgameLevel EndgameLevel { get; init; } = EndgameLevel.None;
    }

    public sealed class LeadCandidateV30
    {
        public string CandidateId { get; init; } = string.Empty;
        public LeadDecisionIntentV30 Intent { get; init; }
        public int PriorityTier { get; init; }
        public int ExpectedScore { get; init; }
        public int FutureValue { get; init; }
        public IReadOnlyList<string> TriggeredRules { get; init; } = new List<string>();
    }

    public sealed class LeadDecisionV30
    {
        public LeadCandidateV30 Selected { get; init; } = new LeadCandidateV30
        {
            CandidateId = "lead.fallback.probe",
            Intent = LeadDecisionIntentV30.ProbeWeakSuit,
            PriorityTier = 7,
            TriggeredRules = new List<string> { "Lead-004" }
        };

        public IReadOnlyList<LeadCandidateV30> Candidates { get; init; } = new List<LeadCandidateV30>();
    }
}
