using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TractorGame.Core.AI.V30.Explain
{
    /// <summary>
    /// V30 decision-log context, independent from policy logic.
    /// </summary>
    public sealed class AIDecisionLogContextV30
    {
        [JsonPropertyName("trace_id")]
        public string TraceId { get; set; } = string.Empty;

        [JsonPropertyName("round_id")]
        public string RoundId { get; set; } = string.Empty;

        [JsonPropertyName("trick_index")]
        public int TrickIndex { get; set; }

        [JsonPropertyName("turn_index")]
        public int TurnIndex { get; set; }

        [JsonPropertyName("player_index")]
        public int PlayerIndex { get; set; } = -1;

        [JsonPropertyName("seat_tag")]
        public string SeatTag { get; set; } = string.Empty;

        [JsonPropertyName("source_policy")]
        public string SourcePolicy { get; set; } = "RuleAI-V30";

        [JsonPropertyName("tags")]
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>();
    }
}
