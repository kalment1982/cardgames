using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace TractorGame.Core.AI.V30.Explain
{
    /// <summary>
    /// V30 explain layer: transforms input into a stable decision bundle.
    /// No policy logic should live here.
    /// </summary>
    public sealed class DecisionExplainerV30
    {
        public DecisionBundleV30 Build(DecisionExplainInputV30 input)
        {
            if (input == null)
                throw new ArgumentNullException(nameof(input));

            var candidates = input.CandidateSummary ?? new List<DecisionCandidateV30>();
            var selectedAction = input.SelectedAction ?? candidates.FirstOrDefault()?.Action ?? new List<string>();
            var selectedReason = string.IsNullOrWhiteSpace(input.SelectedReason)
                ? candidates.FirstOrDefault()?.ReasonCode ?? "no_candidate"
                : input.SelectedReason;

            return new DecisionBundleV30
            {
                Phase = input.Phase ?? string.Empty,
                PrimaryIntent = input.PrimaryIntent ?? string.Empty,
                SecondaryIntent = input.SecondaryIntent ?? string.Empty,
                TriggeredRules = SafeList(input.TriggeredRules),
                CandidateCount = input.CandidateCount > 0 ? input.CandidateCount : candidates.Count,
                CandidateSummary = candidates.Select(CloneCandidate).ToList(),
                RejectedReasons = SafeList(input.RejectedReasons),
                SelectedAction = new List<string>(selectedAction),
                SelectedReason = selectedReason,
                KnownFacts = input.KnownFacts != null
                    ? new Dictionary<string, string>(input.KnownFacts)
                    : new Dictionary<string, string>(),
                EstimatedFacts = input.EstimatedFacts != null
                    ? input.EstimatedFacts.Select(CloneEstimatedFact).ToList()
                    : new List<EstimatedFactV30>(),
                WinSecurity = input.WinSecurity ?? string.Empty,
                BottomMode = input.BottomMode ?? string.Empty,
                GeneratedAtUtc = (input.GeneratedAtUtc ?? DateTimeOffset.UtcNow).ToString("O")
            };
        }

        private static List<string> SafeList(IReadOnlyList<string>? value)
        {
            return value == null ? new List<string>() : new List<string>(value);
        }

        private static DecisionCandidateV30 CloneCandidate(DecisionCandidateV30 source)
        {
            return new DecisionCandidateV30
            {
                Action = source.Action != null ? new List<string>(source.Action) : new List<string>(),
                Score = source.Score,
                ReasonCode = source.ReasonCode ?? string.Empty,
                Features = source.Features != null
                    ? new Dictionary<string, double>(source.Features)
                    : new Dictionary<string, double>()
            };
        }

        private static EstimatedFactV30 CloneEstimatedFact(EstimatedFactV30 source)
        {
            return new EstimatedFactV30
            {
                Key = source.Key ?? string.Empty,
                Value = source.Value ?? string.Empty,
                Confidence = source.Confidence,
                Evidence = source.Evidence ?? string.Empty
            };
        }
    }

    public sealed class DecisionExplainInputV30
    {
        public string? Phase { get; set; }
        public string? PrimaryIntent { get; set; }
        public string? SecondaryIntent { get; set; }
        public int CandidateCount { get; set; }
        public IReadOnlyList<string>? TriggeredRules { get; set; }
        public IReadOnlyList<DecisionCandidateV30>? CandidateSummary { get; set; }
        public IReadOnlyList<string>? RejectedReasons { get; set; }
        public IReadOnlyList<string>? SelectedAction { get; set; }
        public string? SelectedReason { get; set; }
        public IReadOnlyDictionary<string, string>? KnownFacts { get; set; }
        public IReadOnlyList<EstimatedFactV30>? EstimatedFacts { get; set; }
        public string? WinSecurity { get; set; }
        public string? BottomMode { get; set; }
        public DateTimeOffset? GeneratedAtUtc { get; set; }
    }

    public sealed class DecisionCandidateV30
    {
        [JsonPropertyName("action")]
        public List<string> Action { get; set; } = new List<string>();

        [JsonPropertyName("score")]
        public double Score { get; set; }

        [JsonPropertyName("reason_code")]
        public string ReasonCode { get; set; } = string.Empty;

        [JsonPropertyName("features")]
        public Dictionary<string, double> Features { get; set; } = new Dictionary<string, double>();
    }

    /// <summary>
    /// Probabilistic inferred fact.
    /// Must stay separated from known_facts.
    /// </summary>
    public sealed class EstimatedFactV30
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Value { get; set; } = string.Empty;

        [JsonPropertyName("confidence")]
        public double Confidence { get; set; }

        [JsonPropertyName("evidence")]
        public string Evidence { get; set; } = string.Empty;
    }

    public sealed class DecisionBundleV30
    {
        [JsonPropertyName("phase")]
        public string Phase { get; set; } = string.Empty;

        [JsonPropertyName("primary_intent")]
        public string PrimaryIntent { get; set; } = string.Empty;

        [JsonPropertyName("secondary_intent")]
        public string SecondaryIntent { get; set; } = string.Empty;

        [JsonPropertyName("triggered_rules")]
        public List<string> TriggeredRules { get; set; } = new List<string>();

        [JsonPropertyName("candidate_count")]
        public int CandidateCount { get; set; }

        [JsonPropertyName("candidate_summary")]
        public List<DecisionCandidateV30> CandidateSummary { get; set; } = new List<DecisionCandidateV30>();

        [JsonPropertyName("rejected_reasons")]
        public List<string> RejectedReasons { get; set; } = new List<string>();

        [JsonPropertyName("selected_action")]
        public List<string> SelectedAction { get; set; } = new List<string>();

        [JsonPropertyName("selected_reason")]
        public string SelectedReason { get; set; } = string.Empty;

        [JsonPropertyName("known_facts")]
        public Dictionary<string, string> KnownFacts { get; set; } = new Dictionary<string, string>();

        [JsonPropertyName("estimated_facts")]
        public List<EstimatedFactV30> EstimatedFacts { get; set; } = new List<EstimatedFactV30>();

        [JsonPropertyName("win_security")]
        public string WinSecurity { get; set; } = string.Empty;

        [JsonPropertyName("bottom_mode")]
        public string BottomMode { get; set; } = string.Empty;

        [JsonPropertyName("generated_at_utc")]
        public string GeneratedAtUtc { get; set; } = string.Empty;

        [JsonPropertyName("log_context")]
        public AIDecisionLogContextV30? LogContext { get; set; }
    }
}
