using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.V30.Lead
{
    public sealed class LeadPriorityResolverV30
    {
        public LeadCandidateV30 Resolve(IReadOnlyList<LeadCandidateV30> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new LeadCandidateV30
                {
                    CandidateId = "lead.fallback.probe",
                    Intent = LeadDecisionIntentV30.ProbeWeakSuit,
                    PriorityTier = 7,
                    TriggeredRules = new[] { "Lead-004" }
                };
            }

            int bestTier = candidates.Min(c => c.PriorityTier);
            var sameTier = candidates.Where(c => c.PriorityTier == bestTier);

            return sameTier
                .OrderByDescending(c => c.FutureValue)
                .ThenByDescending(c => c.ExpectedScore)
                .ThenBy(c => c.CandidateId, StringComparer.Ordinal)
                .First();
        }
    }
}
