using System.Linq;
using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadModelsV30Tests
    {
        [Fact]
        public void LeadContext_Defaults_AreConservative()
        {
            var context = new LeadContextV30();

            Assert.Equal(LeadRoleV30.Opponent, context.Role);
            Assert.False(context.HasSafeThrowPlan);
            Assert.True(context.HasProbePlan);
            Assert.Equal(PairControlLevelV30.None, context.ThreePairControlLevel);
        }

        [Fact]
        public void LeadCandidate_CanCarryRulesAndPriority()
        {
            var candidate = new LeadCandidateV30
            {
                CandidateId = "lead001.dealer_stable_side",
                Intent = LeadDecisionIntentV30.StableSideSuitRun,
                PriorityTier = 2,
                ExpectedScore = 0,
                FutureValue = 8,
                TriggeredRules = new[] { "Lead-001" }
            };

            Assert.Equal("lead001.dealer_stable_side", candidate.CandidateId);
            Assert.Equal(LeadDecisionIntentV30.StableSideSuitRun, candidate.Intent);
            Assert.Single(candidate.TriggeredRules);
            Assert.Equal("Lead-001", candidate.TriggeredRules.First());
        }

        [Fact]
        public void LeadDecision_DefaultSelected_IsProbeFallback()
        {
            var decision = new LeadDecisionV30();

            Assert.Equal("lead.fallback.probe", decision.Selected.CandidateId);
            Assert.Equal(LeadDecisionIntentV30.ProbeWeakSuit, decision.Selected.Intent);
            Assert.Equal(7, decision.Selected.PriorityTier);
        }
    }
}
