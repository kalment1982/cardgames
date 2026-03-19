using System.Collections.Generic;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 意图解析结果。
    /// </summary>
    public sealed class ResolvedIntentV30
    {
        public DecisionIntentKindV30 PrimaryIntent { get; init; } = DecisionIntentKindV30.Unknown;

        public DecisionIntentKindV30 SecondaryIntent { get; init; } = DecisionIntentKindV30.Unknown;

        public string Mode { get; init; } = string.Empty;

        public double Priority { get; init; }

        public List<string> TriggeredRules { get; init; } = new();

        public List<string> VetoFlags { get; init; } = new();

        public List<string> RiskFlags { get; init; } = new();

        public double MaxCostBudget { get; init; } = 1.0;
    }
}

