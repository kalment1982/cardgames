using TractorGame.Core.AI.V21;

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
            if ((context.Role != LeadRoleV30.Dealer && context.Role != LeadRoleV30.DealerPartner) ||
                !context.HasStableSideSuitRun ||
                context.HasLostSuitControl)
            {
                return false;
            }

            if (context.TrickIndex <= 2)
                return true;

            if (context.HasStrongScoreSideLead)
                return false;

            return context.TrickIndex <= 8 &&
                   context.StableSideSuitFutureValue >= context.ProbeFutureValue + 2;
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
            if (!context.HasProfitableForceTrump || context.HasStrongScoreSideLead)
                return false;

            // Suppress force trump when in a profitable run (unless endgame or already trump-squeezing)
            if (context.LineState?.IsInRun == true &&
                context.LineState.ActiveLine != LeadLineKind.TrumpSqueeze &&
                context.EndgameLevel == EndgameLevel.None)
                return false;

            if ((context.Role == LeadRoleV30.Dealer || context.Role == LeadRoleV30.DealerPartner) &&
                context.TrickIndex <= 4 &&
                context.HasStableSideSuitRun &&
                !context.HasLostSuitControl)
            {
                return false;
            }

            if (context.HasStableSideSuitRun &&
                !context.HasLostSuitControl &&
                context.StableSideSuitFutureValue >= context.ForceTrumpFutureValue - 2)
            {
                return false;
            }

            if (context.HasTeamSideSuitRun &&
                context.TeamSideSuitFutureValue >= context.ForceTrumpFutureValue - 2)
            {
                return false;
            }

            if (context.HasFutureThrowPlan &&
                context.FutureThrowExpectedScore >= 10 &&
                !context.IsProtectBottomMode)
            {
                return false;
            }

            return true;
        }

        public bool ShouldLead007HandOff(LeadContextV30 context)
        {
            bool dealerSideTooEarly =
                (context.Role == LeadRoleV30.Dealer || context.Role == LeadRoleV30.DealerPartner) &&
                context.TrickIndex < 4;
            if (dealerSideTooEarly)
                return false;

            return context.MateHasPositiveTakeoverEvidence &&
                   !context.HasClearOwnFollowUpLine &&
                   !context.HasSafeThrowPlan &&
                   !context.HasStableSideSuitRun &&
                   !context.HasProfitableForceTrump &&
                   !context.HasFutureThrowPlan &&
                   !context.HasVoidBuildPlan &&
                   context.ProbeFutureValue <= 0;
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
