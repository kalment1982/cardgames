using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 阶段决策输出。
    /// </summary>
    public sealed class PhaseDecisionV30
    {
        public PhaseKindV30 Phase { get; init; } = PhaseKindV30.Unknown;

        public List<Card> SelectedCards { get; init; } = new();

        public ResolvedIntentV30 Intent { get; init; } = new();

        public List<ScoredActionV30> ScoredActions { get; init; } = new();

        public DecisionExplanationV30 Explanation { get; init; } = new();

        public string SelectedReason => Explanation.SelectedReason;
    }
}

