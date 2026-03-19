using System.Linq;
using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadPolicyV30Tests
    {
        private readonly LeadPolicyV30 _policy = new LeadPolicyV30();

        [Fact]
        public void Decide_Lead007_BlindHandoffMustNotBeSelected()
        {
            var context = new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = false,
                HasProbePlan = true,
                ProbeFutureValue = 1
            };

            var decision = _policy.Decide(context);

            Assert.DoesNotContain(decision.Candidates, c => c.CandidateId == "lead007.handoff_to_mate");
            Assert.Equal("lead004.low_value_probe", decision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead007_ValidDualConditionCanSelectHandoff()
        {
            var context = new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = true,
                HasProbePlan = true
            };

            var decision = _policy.Decide(context);

            Assert.Contains(decision.Candidates, c => c.CandidateId == "lead007.handoff_to_mate");
            Assert.Equal("lead007.handoff_to_mate", decision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead007_OwnFollowUpLineBlocksHandoffEvenWithMateEvidence()
        {
            var context = new LeadContextV30
            {
                HasClearOwnFollowUpLine = true,
                MateHasPositiveTakeoverEvidence = true,
                HasFutureThrowPlan = true,
                HasProbePlan = true,
                ProbeFutureValue = 1
            };

            var decision = _policy.Decide(context);

            Assert.DoesNotContain(decision.Candidates, c => c.CandidateId == "lead007.handoff_to_mate");
            Assert.Equal("lead004.low_value_probe", decision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead008_HighControlThreePairBeatsProbe()
        {
            var context = new LeadContextV30
            {
                HasFutureThrowPlan = true,
                ThreePairControlLevel = PairControlLevelV30.HighPairControl,
                FutureThrowExpectedScore = 20,
                HasProbePlan = true
            };

            var decision = _policy.Decide(context);

            Assert.Contains(decision.Candidates, c => c.CandidateId == "lead008.three_pair.high_control");
            Assert.Equal("lead008.three_pair.high_control", decision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead008_LowPairConsumeIsGeneratedWhenNoHighControl()
        {
            var context = new LeadContextV30
            {
                HasFutureThrowPlan = true,
                ThreePairControlLevel = PairControlLevelV30.LowPairConsume,
                FutureThrowExpectedScore = 11,
                HasProbePlan = true
            };

            var decision = _policy.Decide(context);

            Assert.Contains(decision.Candidates, c => c.CandidateId == "lead008.three_pair.low_pair_consume");
            Assert.Equal("lead008.three_pair.low_pair_consume", decision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead008_ForceTrumpForThrowHonorsHardConstraints()
        {
            var allowed = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 17,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                IsProtectBottomMode = false,
                HasProbePlan = true
            };
            var blocked = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 17,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                IsProtectBottomMode = true,
                HasProbePlan = true
            };

            var allowedDecision = _policy.Decide(allowed);
            var blockedDecision = _policy.Decide(blocked);

            Assert.Contains(allowedDecision.Candidates, c => c.CandidateId == "lead008.force_trump_for_throw");
            Assert.DoesNotContain(blockedDecision.Candidates, c => c.CandidateId == "lead008.force_trump_for_throw");
        }

        [Fact]
        public void Decide_Lead009_OnlyWeakPairVoidPlanEligible()
        {
            var valid = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = true,
                HasExplicitVoidFollowUpBenefit = true,
                VoidPlanFutureValue = 9,
                HasProbePlan = true
            };
            var invalid = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = false,
                HasExplicitVoidFollowUpBenefit = true,
                VoidPlanFutureValue = 9,
                HasProbePlan = true
            };

            var validDecision = _policy.Decide(valid);
            var invalidDecision = _policy.Decide(invalid);

            Assert.Contains(validDecision.Candidates, c => c.CandidateId == "lead009.build_void");
            Assert.DoesNotContain(invalidDecision.Candidates, c => c.CandidateId == "lead009.build_void");
        }

        [Fact]
        public void Decide_Lead001_DealerEarlyWindowWorksButTrick3FallsBack()
        {
            var early = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 2,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 8,
                HasProbePlan = true
            };
            var late = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 3,
                HasStableSideSuitRun = true,
                HasLostSuitControl = false,
                StableSideSuitFutureValue = 8,
                HasProbePlan = true
            };

            var earlyDecision = _policy.Decide(early);
            var lateDecision = _policy.Decide(late);

            Assert.Equal("lead001.dealer_stable_side", earlyDecision.Selected.CandidateId);
            Assert.DoesNotContain(lateDecision.Candidates, c => c.CandidateId == "lead001.dealer_stable_side");
            Assert.Equal("lead004.low_value_probe", lateDecision.Selected.CandidateId);
        }

        [Fact]
        public void Decide_Lead002_StrongScoreSideLead_BeatsForceTrump()
        {
            var context = new LeadContextV30
            {
                HasStrongScoreSideLead = true,
                StrongScoreSideLeadFutureValue = 12,
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 20,
                HasProbePlan = true
            };

            var decision = _policy.Decide(context);

            Assert.Contains(decision.Candidates, c => c.CandidateId == "lead002.score_side_cash");
            Assert.DoesNotContain(decision.Candidates, c => c.CandidateId == "lead003.force_trump");
            Assert.Equal("lead002.score_side_cash", decision.Selected.CandidateId);
        }
    }
}
