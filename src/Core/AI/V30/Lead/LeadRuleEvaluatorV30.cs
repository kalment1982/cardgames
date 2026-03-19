namespace TractorGame.Core.AI.V30.Lead
{
    public sealed class LeadRuleEvaluatorV30
    {
        public bool IsHighValueSafeThrow(LeadContextV30 context)
        {
            return context.HasSafeThrowPlan && context.SafeThrowExpectedScore >= 10;
        }

        public bool IsLowValueSafeThrow(LeadContextV30 context)
        {
            return context.HasSafeThrowPlan && context.SafeThrowExpectedScore < 10;
        }

        public bool ShouldLead001DealerStableSideSuit(LeadContextV30 context)
        {
            return context.Role == LeadRoleV30.Dealer &&
                   context.TrickIndex <= 2 &&
                   context.HasStableSideSuitRun &&
                   !context.HasLostSuitControl;
        }

        public bool ShouldLead002StrongScoreSideLead(LeadContextV30 context)
        {
            return context.HasStrongScoreSideLead;
        }

        public bool ShouldLead006TeamSideSuit(LeadContextV30 context)
        {
            return context.HasTeamSideSuitRun && context.KeyOpponentLikelyNotVoid;
        }

        public bool ShouldLead003ForceTrump(LeadContextV30 context)
        {
            return context.HasProfitableForceTrump && !context.HasStrongScoreSideLead;
        }

        public bool ShouldLead007HandOff(LeadContextV30 context)
        {
            return context.MateHasPositiveTakeoverEvidence &&
                   !context.HasClearOwnFollowUpLine &&
                   !context.HasSafeThrowPlan &&
                   !context.HasStableSideSuitRun &&
                   !context.HasProfitableForceTrump &&
                   !context.HasFutureThrowPlan &&
                   !context.HasVoidBuildPlan;
        }

        public bool ShouldLead008ThreePairPlan(LeadContextV30 context)
        {
            return context.HasFutureThrowPlan && context.ThreePairControlLevel != PairControlLevelV30.None;
        }

        public bool ShouldLead008ForceTrumpForThrow(LeadContextV30 context)
        {
            if (!context.HasForceTrumpForThrowPlan)
                return false;

            bool keepsControlResource =
                (context.TrumpCountAfterForceTrump >= 2 && context.KeepsControlTrumpAfterForceTrump) ||
                context.KeepsTrumpPairAfterForceTrump;

            return keepsControlResource &&
                   context.FutureThrowExpectedScore >= 15 &&
                   !context.IsProtectBottomMode;
        }

        public bool ShouldLead009BuildVoid(LeadContextV30 context)
        {
            return context.HasVoidBuildPlan &&
                   context.VoidBreaksOnlyWeakNonScorePairs &&
                   context.HasExplicitVoidFollowUpBenefit;
        }
    }
}
