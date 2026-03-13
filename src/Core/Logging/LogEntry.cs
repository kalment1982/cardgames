using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TractorGame.Core.Logging
{
    public static class LogCategories
    {
        public const string Audit = "audit";
        public const string Decision = "decision";
        public const string Diag = "diag";
        public const string Perf = "perf";
    }

    public static class LogLevels
    {
        public const string Debug = "DEBUG";
        public const string Info = "INFO";
        public const string Warn = "WARN";
        public const string Error = "ERROR";
        public const string Fatal = "FATAL";
    }

    /// <summary>
    /// 统一日志信封（v1.1）。
    /// </summary>
    public sealed class LogEntry
    {
        [JsonPropertyName("schema_version")]
        public string SchemaVersion { get; set; } = "1.1";

        [JsonPropertyName("ts_utc")]
        public DateTime TsUtc { get; set; } = DateTime.UtcNow;

        [JsonPropertyName("level")]
        public string Level { get; set; } = LogLevels.Info;

        [JsonPropertyName("category")]
        public string Category { get; set; } = LogCategories.Audit;

        [JsonPropertyName("event")]
        public string Event { get; set; } = string.Empty;

        [JsonPropertyName("session_id")]
        public string? SessionId { get; set; }

        [JsonPropertyName("game_id")]
        public string? GameId { get; set; }

        [JsonPropertyName("round_id")]
        public string? RoundId { get; set; }

        [JsonPropertyName("trick_id")]
        public string? TrickId { get; set; }

        [JsonPropertyName("turn_id")]
        public string? TurnId { get; set; }

        [JsonPropertyName("seq")]
        public long Seq { get; set; }

        [JsonPropertyName("phase")]
        public string? Phase { get; set; }

        [JsonPropertyName("actor")]
        public string? Actor { get; set; }

        [JsonPropertyName("correlation_id")]
        public string? CorrelationId { get; set; }

        [JsonPropertyName("causation_id")]
        public string? CausationId { get; set; }

        [JsonPropertyName("payload")]
        public Dictionary<string, object?> Payload { get; set; } = new();

        [JsonPropertyName("metrics")]
        public Dictionary<string, double> Metrics { get; set; } = new();
    }
}
