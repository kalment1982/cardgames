using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 埋底策略 2.0。
    /// </summary>
    public sealed class BuryPolicy2
    {
        private readonly BuryCandidateGenerator _candidateGenerator;
        private readonly IntentResolver _intentResolver;
        private readonly ActionScorer _actionScorer;
        private readonly DecisionExplainer _explainer;

        public BuryPolicy2(
            BuryCandidateGenerator candidateGenerator,
            IntentResolver intentResolver,
            ActionScorer actionScorer,
            DecisionExplainer explainer)
        {
            _candidateGenerator = candidateGenerator;
            _intentResolver = intentResolver;
            _actionScorer = actionScorer;
            _explainer = explainer;
        }

        public PhaseDecision Decide(RuleAIContext context)
        {
            var candidates = _candidateGenerator.Generate(context);
            var intent = _intentResolver.Resolve(context, candidates);
            var scored = _actionScorer.Score(context, intent, candidates);
            var explanation = _explainer.Build(context, intent, scored, "BuryPolicy2");

            return new PhaseDecision
            {
                Phase = context.Phase,
                SelectedCards = scored.Count > 0 ? new List<Card>(scored[0].Cards) : new List<Card>(),
                Intent = intent,
                ScoredActions = scored,
                Explanation = explanation
            };
        }
    }
}
