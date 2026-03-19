namespace TractorGame.Core.AI.V30.Lead
{
    public sealed class LeadPolicyV30
    {
        private readonly LeadCandidateGeneratorV30 _generator;
        private readonly LeadPriorityResolverV30 _resolver;

        public LeadPolicyV30(
            LeadCandidateGeneratorV30? generator = null,
            LeadPriorityResolverV30? resolver = null)
        {
            _generator = generator ?? new LeadCandidateGeneratorV30();
            _resolver = resolver ?? new LeadPriorityResolverV30();
        }

        public LeadDecisionV30 Decide(LeadContextV30 context)
        {
            var candidates = _generator.Generate(context);
            var selected = _resolver.Resolve(candidates);

            return new LeadDecisionV30
            {
                Selected = selected,
                Candidates = candidates
            };
        }
    }
}
