using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Acceptance
{
    public class LeadAcceptanceTests
    {
        private readonly LeadPolicyV30 _policy = new LeadPolicyV30();

        [Fact]
        public void Lead001_DealerEarlyStableSideSuit_ShouldPreferStableSideSuit()
        {
            var decision = _policy.Decide(new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 1,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 8,
                HasLostSuitControl = false
            });

            Assert.Equal("lead001.dealer_stable_side", decision.Selected.CandidateId);
        }

        [Fact]
        public void Lead003_DrawTrumpRequiresClearBenefit_ShouldRejectIfNoBenefit()
        {
            var noBenefit = _policy.Decide(new LeadContextV30
            {
                HasProfitableForceTrump = false,
                HasProbePlan = true
            });

            var withBenefit = _policy.Decide(new LeadContextV30
            {
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 11,
                HasProbePlan = false
            });

            Assert.DoesNotContain(noBenefit.Candidates, c => c.CandidateId == "lead003.force_trump");
            Assert.Contains(withBenefit.Candidates, c => c.CandidateId == "lead003.force_trump");
            Assert.Equal("lead003.force_trump", withBenefit.Selected.CandidateId);
        }

        [Fact]
        public void Lead005_SafeThrowWithScoreAtLeast10_ShouldBeTopPriority()
        {
            var decision = _policy.Decide(new LeadContextV30
            {
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 10,
                SafeThrowFutureValue = 20,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 30
            });

            Assert.Equal("lead005.safe_throw.high", decision.Selected.CandidateId);
            Assert.Equal(1, decision.Selected.PriorityTier);
        }

        [Fact]
        public void Lead006_StableSuitTeamScoring_ShouldContinueAtConfidence70Percent()
        {
            var decision = _policy.Decide(new LeadContextV30
            {
                HasTeamSideSuitRun = true,
                TeamSideSuitFutureValue = 9,
                KeyOpponentLikelyNotVoid = true
            });

            Assert.Contains(decision.Candidates, c => c.CandidateId == "lead006.team_side_suit");
            Assert.Equal("lead006.team_side_suit", decision.Selected.CandidateId);
        }

        [Fact]
        public void Lead007_HandoffRequiresDualConditions_ShouldRejectBlindHandoff()
        {
            var blindHandoff = _policy.Decide(new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = false,
                HasProbePlan = true
            });

            var validHandoff = _policy.Decide(new LeadContextV30
            {
                HasClearOwnFollowUpLine = false,
                MateHasPositiveTakeoverEvidence = true,
                HasProbePlan = false
            });

            Assert.DoesNotContain(blindHandoff.Candidates, c => c.CandidateId == "lead007.handoff_to_mate");
            Assert.Contains(validHandoff.Candidates, c => c.CandidateId == "lead007.handoff_to_mate");
            Assert.Equal("lead007.handoff_to_mate", validHandoff.Selected.CandidateId);
        }

        [Fact]
        public void Lead008_PrepareThrow_ShouldRespectTrumpDrawHardConstraints()
        {
            var allowed = _policy.Decide(new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                FutureThrowExpectedScore = 15,
                IsProtectBottomMode = false,
                HasProbePlan = false
            });

            var blocked = _policy.Decide(new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                TrumpCountAfterForceTrump = 1,
                KeepsControlTrumpAfterForceTrump = false,
                KeepsTrumpPairAfterForceTrump = false,
                FutureThrowExpectedScore = 14,
                IsProtectBottomMode = true,
                HasProbePlan = true
            });

            Assert.Contains(allowed.Candidates, c => c.CandidateId == "lead008.force_trump_for_throw");
            Assert.DoesNotContain(blocked.Candidates, c => c.CandidateId == "lead008.force_trump_for_throw");
        }

        [Fact]
        public void Lead009_CreateVoid_ShouldOnlyBreakWeakNonScorePairs()
        {
            var allowed = _policy.Decide(new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = true,
                HasExplicitVoidFollowUpBenefit = true,
                VoidPlanFutureValue = 8,
                HasProbePlan = true
            });

            var blocked = _policy.Decide(new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = false,
                HasExplicitVoidFollowUpBenefit = true,
                VoidPlanFutureValue = 8,
                HasProbePlan = true
            });

            Assert.Contains(allowed.Candidates, c => c.CandidateId == "lead009.build_void");
            Assert.DoesNotContain(blocked.Candidates, c => c.CandidateId == "lead009.build_void");
        }
    }
}
