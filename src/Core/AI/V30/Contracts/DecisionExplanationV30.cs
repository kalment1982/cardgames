using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// V30 决策解释结构。
    /// </summary>
    public sealed class DecisionExplanationV30
    {
        [JsonPropertyName("phase")]
        public string Phase { get; init; } = "unknown";

        [JsonPropertyName("primary_intent")]
        public string PrimaryIntent { get; init; } = "Unknown";

        [JsonPropertyName("secondary_intent")]
        public string SecondaryIntent { get; init; } = "Unknown";

        [JsonPropertyName("triggered_rules")]
        public List<string> TriggeredRules { get; init; } = new();

        [JsonPropertyName("candidate_count")]
        public int CandidateCount { get; init; }

        [JsonPropertyName("candidate_summary")]
        public List<string> CandidateSummary { get; init; } = new();

        [JsonPropertyName("rejected_reasons")]
        public List<string> RejectedReasons { get; init; } = new();

        [JsonPropertyName("selected_action")]
        public List<string> SelectedAction { get; init; } = new();

        [JsonPropertyName("selected_reason")]
        public string SelectedReason { get; init; } = "unspecified";

        [JsonPropertyName("known_facts")]
        public Dictionary<string, string> KnownFacts { get; init; } = new();

        [JsonPropertyName("estimated_facts")]
        public Dictionary<string, string> EstimatedFacts { get; init; } = new();

        [JsonPropertyName("win_security")]
        public string WinSecurity { get; init; } = "Unknown";

        [JsonPropertyName("bottom_mode")]
        public string BottomMode { get; init; } = "Normal";

        [JsonPropertyName("generated_at_utc")]
        public DateTime GeneratedAtUtc { get; init; } = DateTime.UtcNow;

        /// <summary>
        /// 预留给 Explain 包注入结构化上下文；Contracts 仅做容器。
        /// </summary>
        [JsonPropertyName("log_context")]
        public object? LogContext { get; init; }
    }
}

