using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 将策略结果整理成统一解释结构，供日志/回放共享。
    /// </summary>
    public sealed class DecisionExplainer
    {
        public DecisionExplanation Build(
            RuleAIContext context,
            ResolvedIntent intent,
            IReadOnlyList<ScoredAction> scoredActions,
            string phasePolicy,
            IReadOnlyList<string>? hardRuleRejects = null)
        {
            var top = scoredActions.Take(3).ToList();
            var selected = top.FirstOrDefault() ?? new ScoredAction();

            return new DecisionExplanation
            {
                Phase = context.Phase,
                PhasePolicy = phasePolicy,
                PrimaryIntent = intent.PrimaryIntent.ToString(),
                SecondaryIntent = intent.SecondaryIntent.ToString(),
                SelectedReason = selected.ReasonCode,
                CandidateCount = scoredActions.Count,
                TopCandidates = top.Select(action => RuleAIUtility.BuildReadableCandidate(action.Cards)).ToList(),
                TopScores = top.Select(action => action.Score).ToList(),
                SelectedAction = selected.Cards.Select(card => card.ToString()).ToList(),
                HardRuleRejects = hardRuleRejects?.ToList() ?? new List<string>(),
                RiskFlags = intent.RiskFlags.ToList(),
                Tags = new List<string> { phasePolicy, intent.Mode },
                CandidateFeatures = top.Select(action => new Dictionary<string, double>(action.Features)).ToList(),
                SelectedActionFeatures = new Dictionary<string, double>(selected.Features)
            };
        }
    }
}
