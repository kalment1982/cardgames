using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadPriorityResolverV30Tests
    {
        private readonly LeadPriorityResolverV30 _resolver = new LeadPriorityResolverV30();

        [Fact]
        public void Resolve_PicksLowerPriorityTierFirst()
        {
            var selected = _resolver.Resolve(new[]
            {
                new LeadCandidateV30
                {
                    CandidateId = "lead003.force_trump",
                    Intent = LeadDecisionIntentV30.ForceTrump,
                    PriorityTier = 3,
                    FutureValue = 50
                },
                new LeadCandidateV30
                {
                    CandidateId = "lead001.dealer_stable_side",
                    Intent = LeadDecisionIntentV30.StableSideSuitRun,
                    PriorityTier = 2,
                    FutureValue = 1
                }
            });

            Assert.Equal("lead001.dealer_stable_side", selected.CandidateId);
        }

        [Fact]
        public void Resolve_Tier2UsesFutureValueTieBreak()
        {
            var selected = _resolver.Resolve(new[]
            {
                new LeadCandidateV30
                {
                    CandidateId = "lead006.team_side_suit",
                    Intent = LeadDecisionIntentV30.StableSideSuitRun,
                    PriorityTier = 2,
                    FutureValue = 8,
                    ExpectedScore = 0
                },
                new LeadCandidateV30
                {
                    CandidateId = "lead005.safe_throw.low",
                    Intent = LeadDecisionIntentV30.SafeThrow,
                    PriorityTier = 2,
                    FutureValue = 12,
                    ExpectedScore = 0
                }
            });

            Assert.Equal("lead005.safe_throw.low", selected.CandidateId);
        }

        [Fact]
        public void Resolve_EmptyCandidatesReturnsProbeFallback()
        {
            var selected = _resolver.Resolve(System.Array.Empty<LeadCandidateV30>());

            Assert.Equal("lead.fallback.probe", selected.CandidateId);
            Assert.Equal(LeadDecisionIntentV30.ProbeWeakSuit, selected.Intent);
        }
    }
}
