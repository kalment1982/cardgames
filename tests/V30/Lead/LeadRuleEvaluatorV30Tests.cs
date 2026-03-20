using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadRuleEvaluatorV30Tests
    {
        private readonly LeadRuleEvaluatorV30 _evaluator = new LeadRuleEvaluatorV30();

        [Fact]
        public void Lead001_DealerEarlyStableSideSuit_Triggers()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 1,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false
            };

            Assert.True(_evaluator.ShouldLead001DealerStableSideSuit(context));
        }

        [Fact]
        public void Lead001_DealerMidgameStableSide_WithClearEdge_Triggers()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 3,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 14,
                ProbeFutureValue = 10
            };

            Assert.True(_evaluator.ShouldLead001DealerStableSideSuit(context));
        }

        [Fact]
        public void Lead001_DealerPartnerMidgameStableSide_WithClearEdge_Triggers()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.DealerPartner,
                TrickIndex = 5,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 12,
                ProbeFutureValue = 9
            };

            Assert.True(_evaluator.ShouldLead001DealerStableSideSuit(context));
        }

        [Fact]
        public void Lead001_StableSideWithoutEnoughEdge_DoesNotTriggerMidgame()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 4,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 10,
                ProbeFutureValue = 9
            };

            Assert.False(_evaluator.ShouldLead001DealerStableSideSuit(context));
        }

        [Fact]
        public void Lead001_LostSuitControl_DoesNotTrigger()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 2,
                HasStableSideSuitRun = true,
                HasLostSuitControl = true
            };

            Assert.False(_evaluator.ShouldLead001DealerStableSideSuit(context));
        }

        [Fact]
        public void Lead002_StrongScoreSideLead_Triggers()
        {
            var context = new LeadContextV30
            {
                HasStrongScoreSideLead = true
            };

            Assert.True(_evaluator.ShouldLead002StrongScoreSideLead(context));
        }

        [Fact]
        public void Lead003_StrongScoreSideLead_BlocksForceTrump()
        {
            var blocked = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                HasStrongScoreSideLead = true
            };
            var allowed = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                HasStrongScoreSideLead = false
            };

            Assert.False(_evaluator.ShouldLead003ForceTrump(blocked));
            Assert.True(_evaluator.ShouldLead003ForceTrump(allowed));
        }

        [Fact]
        public void Lead003_StableSideOrThrowPlan_BlocksForceTrump()
        {
            var stableSide = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 12,
                ForceTrumpFutureValue = 13
            };
            var throwPlan = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                HasFutureThrowPlan = true,
                FutureThrowExpectedScore = 14,
                IsProtectBottomMode = false
            };

            Assert.False(_evaluator.ShouldLead003ForceTrump(stableSide));
            Assert.False(_evaluator.ShouldLead003ForceTrump(throwPlan));
        }

        [Fact]
        public void Lead007_DualConditionRequired_BlindHandoffBlocked()
        {
            var blind = new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = false
            };

            var valid = new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = true
            };

            Assert.False(_evaluator.ShouldLead007HandOff(blind));
            Assert.True(_evaluator.ShouldLead007HandOff(valid));
        }

        [Fact]
        public void Lead007_OwnFuturePlansBlockHandoff()
        {
            var futureThrow = new LeadContextV30
            {
                HasClearOwnFollowUpLine = true,
                MateHasPositiveTakeoverEvidence = true,
                HasFutureThrowPlan = true
            };
            var buildVoid = new LeadContextV30
            {
                HasClearOwnFollowUpLine = true,
                MateHasPositiveTakeoverEvidence = true,
                HasVoidBuildPlan = true
            };

            Assert.False(_evaluator.ShouldLead007HandOff(futureThrow));
            Assert.False(_evaluator.ShouldLead007HandOff(buildVoid));
        }

        [Fact]
        public void Lead007_DealerSideEarlyOrPositiveProbe_BlocksHandoff()
        {
            var earlyDealer = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 2,
                MateHasPositiveTakeoverEvidence = true,
                ProbeFutureValue = -1
            };
            var positiveProbe = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 5,
                MateHasPositiveTakeoverEvidence = true,
                ProbeFutureValue = 12
            };

            Assert.False(_evaluator.ShouldLead007HandOff(earlyDealer));
            Assert.False(_evaluator.ShouldLead007HandOff(positiveProbe));
        }

        [Fact]
        public void Lead008_ForceTrumpForThrow_RequiresHardConstraints()
        {
            var allowed = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 16,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                IsProtectBottomMode = false
            };
            var blockedByProtectBottom = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 18,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                IsProtectBottomMode = true
            };
            var blockedByLowExpectedGain = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 14,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                IsProtectBottomMode = false
            };

            Assert.True(_evaluator.ShouldLead008ForceTrumpForThrow(allowed));
            Assert.False(_evaluator.ShouldLead008ForceTrumpForThrow(blockedByProtectBottom));
            Assert.False(_evaluator.ShouldLead008ForceTrumpForThrow(blockedByLowExpectedGain));
        }

        [Fact]
        public void Lead009_RequiresWeakPairBreakAndExplicitBenefit()
        {
            var allowed = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = true,
                HasExplicitVoidFollowUpBenefit = true
            };
            var blocked = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = false,
                HasExplicitVoidFollowUpBenefit = true
            };

            Assert.True(_evaluator.ShouldLead009BuildVoid(allowed));
            Assert.False(_evaluator.ShouldLead009BuildVoid(blocked));
        }
    }
}
