using System.Collections.Generic;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 统一的 AI 决策解释结构。
    /// </summary>
    public sealed class DecisionExplanation
    {
        public PhaseKind Phase { get; init; } = PhaseKind.Unknown;

        public string PhasePolicy { get; init; } = string.Empty;

        public string PrimaryIntent { get; init; } = "Unknown";

        public string SecondaryIntent { get; init; } = "Unknown";

        public string SelectedReason { get; init; } = "unspecified";

        public int CandidateCount { get; init; }

        public List<string> TopCandidates { get; init; } = new();

        public List<double> TopScores { get; init; } = new();

        public List<string> SelectedAction { get; init; } = new();

        public List<string> HardRuleRejects { get; init; } = new();

        public List<string> RiskFlags { get; init; } = new();

        public List<string> Tags { get; init; } = new();

        public List<Dictionary<string, double>> CandidateFeatures { get; init; } = new();

        public Dictionary<string, double> SelectedActionFeatures { get; init; } = new();
    }
}
