using System.Linq;
using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadCandidateGeneratorV30Tests
    {
        private readonly LeadCandidateGeneratorV30 _generator = new LeadCandidateGeneratorV30();

        [Fact]
        public void Generate_IncludesHighSafeThrowAsTier1()
        {
            var context = new LeadContextV30
            {
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 15,
                SafeThrowFutureValue = 12,
                HasProbePlan = false
            };

            var candidates = _generator.Generate(context);

            var safeThrow = candidates.Single(c => c.CandidateId == "lead005.safe_throw.high");
            Assert.Equal(1, safeThrow.PriorityTier);
            Assert.Equal(LeadDecisionIntentV30.SafeThrow, safeThrow.Intent);
        }

        [Fact]
        public void Generate_LowSafeThrowFallsToTier2()
        {
            var context = new LeadContextV30
            {
                HasSafeThrowPlan = true,
                SafeThrowExpectedScore = 0,
                SafeThrowFutureValue = 9,
                HasProbePlan = false
            };

            var candidates = _generator.Generate(context);

            var safeThrow = candidates.Single(c => c.CandidateId == "lead005.safe_throw.low");
            Assert.Equal(2, safeThrow.PriorityTier);
        }

        [Fact]
        public void Generate_Lead008ThreePair_ChoosesHighOrLowCandidateId()
        {
            var high = new LeadContextV30
            {
                HasFutureThrowPlan = true,
                ThreePairControlLevel = PairControlLevelV30.HighPairControl,
                FutureThrowExpectedScore = 18,
                HasProbePlan = false
            };
            var low = new LeadContextV30
            {
                HasFutureThrowPlan = true,
                ThreePairControlLevel = PairControlLevelV30.LowPairConsume,
                FutureThrowExpectedScore = 12,
                HasProbePlan = false
            };

            var highCandidates = _generator.Generate(high);
            var lowCandidates = _generator.Generate(low);

            Assert.Contains(highCandidates, c => c.CandidateId == "lead008.three_pair.high_control");
            Assert.Contains(lowCandidates, c => c.CandidateId == "lead008.three_pair.low_pair_consume");
        }

        [Fact]
        public void Generate_Lead008ForceTrumpForThrow_RespectsHardConstraintResult()
        {
            var allowed = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 16,
                TrumpCountAfterForceTrump = 2,
                KeepsControlTrumpAfterForceTrump = true,
                HasProbePlan = false
            };
            var blocked = new LeadContextV30
            {
                HasForceTrumpForThrowPlan = true,
                FutureThrowExpectedScore = 16,
                TrumpCountAfterForceTrump = 1,
                KeepsControlTrumpAfterForceTrump = false,
                KeepsTrumpPairAfterForceTrump = false,
                HasProbePlan = false
            };

            var allowedCandidates = _generator.Generate(allowed);
            var blockedCandidates = _generator.Generate(blocked);

            Assert.Contains(allowedCandidates, c => c.CandidateId == "lead008.force_trump_for_throw");
            Assert.DoesNotContain(blockedCandidates, c => c.CandidateId == "lead008.force_trump_for_throw");
        }

        [Fact]
        public void Generate_Lead009OnlyWhenWeakPairAndBenefit()
        {
            var allowed = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = true,
                HasExplicitVoidFollowUpBenefit = true,
                HasProbePlan = false
            };
            var blocked = new LeadContextV30
            {
                HasVoidBuildPlan = true,
                VoidBreaksOnlyWeakNonScorePairs = false,
                HasExplicitVoidFollowUpBenefit = true,
                HasProbePlan = false
            };

            var allowedCandidates = _generator.Generate(allowed);
            var blockedCandidates = _generator.Generate(blocked);

            Assert.Contains(allowedCandidates, c => c.CandidateId == "lead009.build_void");
            Assert.DoesNotContain(blockedCandidates, c => c.CandidateId == "lead009.build_void");
        }

        [Fact]
        public void Generate_NoStrategyFallsBackToProbe()
        {
            var context = new LeadContextV30
            {
                HasProbePlan = false
            };

            var candidates = _generator.Generate(context);

            Assert.Single(candidates);
            Assert.Equal("lead004.low_value_probe", candidates[0].CandidateId);
        }
    }
}
