using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TractorGame.Core.AI.Evolution.DataEngine
{
    public sealed class GameEvent
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = string.Empty;

        [JsonPropertyName("ts_utc")]
        public DateTime TsUtc { get; set; }

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("round_id")]
        public string? RoundId { get; set; }

        [JsonPropertyName("game_id")]
        public string? GameId { get; set; }

        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, JsonElement> Payload { get; set; } = new();

        [JsonPropertyName("metrics")]
        public Dictionary<string, double> Metrics { get; set; } = new();
    }

    public sealed class TrainingSample
    {
        public string Difficulty { get; set; } = "hard";
        public string Role { get; set; } = "opponent";
        public string Phase { get; set; } = "mid";
        public string Pattern { get; set; } = "single";
        public bool IsHardCase { get; set; }
        public DateTime TimestampUtc { get; set; }
        public double FreshnessWeight { get; set; } = 1.0;
    }

    public sealed class DataQualityReport
    {
        public DateTime GeneratedAtUtc { get; set; } = DateTime.UtcNow;
        public int Generation { get; set; }
        public int TotalEvents { get; set; }
        public int UniqueEvents { get; set; }
        public int SchemaMatchedEvents { get; set; }
        public int AiDecisionEvents { get; set; }
        public int CompleteAiDecisionEvents { get; set; }
        public int DuplicateEventCount { get; set; }
        public int GamesCount { get; set; }

        public double SchemaMatchRate => TotalEvents == 0 ? 0 : (double)SchemaMatchedEvents / TotalEvents;
        public double AiDecisionCompleteness => AiDecisionEvents == 0 ? 0 : (double)CompleteAiDecisionEvents / AiDecisionEvents;
        public double DuplicateRate => TotalEvents == 0 ? 0 : (double)DuplicateEventCount / TotalEvents;
        public double AvgEventsPerGame => GamesCount == 0 ? 0 : (double)UniqueEvents / GamesCount;
    }
}
