using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 首发策略 2.0。
    /// </summary>
    public sealed class LeadPolicy2
    {
        private readonly LeadCandidateGenerator _candidateGenerator;
        private readonly IntentResolver _intentResolver;
        private readonly ActionScorer _actionScorer;
        private readonly DecisionExplainer _explainer;

        public LeadPolicy2(
            LeadCandidateGenerator candidateGenerator,
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
            scored = ReorderForStructuredLead(context, intent, scored);
            var explanation = _explainer.Build(context, intent, scored, "LeadPolicy2");

            return new PhaseDecision
            {
                Phase = context.Phase,
                SelectedCards = scored.Count > 0 ? new List<Card>(scored[0].Cards) : new List<Card>(),
                Intent = intent,
                ScoredActions = scored,
                Explanation = explanation
            };
        }

        private static List<ScoredAction> ReorderForStructuredLead(
            RuleAIContext context,
            ResolvedIntent intent,
            List<ScoredAction> scored)
        {
            if (scored.Count <= 1)
                return scored;

            if (intent.PrimaryIntent == DecisionIntentKind.ProtectBottom ||
                intent.PrimaryIntent == DecisionIntentKind.PrepareEndgame)
                return scored;

            var config = context.GameConfig;

            var tractor = scored
                .Where(action =>
                {
                    if (action.Cards.Count < 4 || action.Cards.Count % 2 != 0)
                        return false;

                    var pattern = new CardPattern(action.Cards, config);
                    return pattern.IsTractor(action.Cards);
                })
                .OrderByDescending(action => action.Cards.Count)
                .ThenByDescending(action => action.Score)
                .FirstOrDefault();

            if (tractor == null)
                return scored;

            scored.Remove(tractor);
            scored.Insert(0, tractor);
            return scored;
        }
    }
}
