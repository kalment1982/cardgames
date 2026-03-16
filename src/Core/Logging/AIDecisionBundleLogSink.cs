using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace TractorGame.Core.Logging
{
    /// <summary>
    /// 将 ai.bundle 事件额外写成单独的 pretty JSON 文件，便于按决策排查。
    /// </summary>
    public sealed class AIDecisionBundleLogSink : ILogSink
    {
        private readonly string _rootPath;
        private readonly object _syncRoot = new();
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true
        };

        public AIDecisionBundleLogSink(string rootPath = "logs/decision")
        {
            _rootPath = rootPath;
        }

        public void Write(LogEntry entry)
        {
            if (entry == null || !string.Equals(entry.Event, "ai.bundle", StringComparison.Ordinal))
                return;

            var tsUtc = entry.TsUtc.Kind == DateTimeKind.Utc ? entry.TsUtc : entry.TsUtc.ToUniversalTime();
            var day = tsUtc.ToString("yyyy-MM-dd");
            var roundToken = SanitizePathSegment(entry.RoundId, "round_unknown");
            var trickToken = SanitizePathSegment(entry.TrickId, "phase_" + (entry.Phase ?? "unknown").ToLowerInvariant());
            var traceToken = SanitizePathSegment(ResolveTraceId(entry), $"decision_{entry.Seq:D6}");
            var dir = Path.Combine(_rootPath, day, roundToken, trickToken);
            var filePath = Path.Combine(dir, $"{traceToken}.json");
            var json = JsonSerializer.Serialize(entry, _jsonOptions);

            lock (_syncRoot)
            {
                Directory.CreateDirectory(dir);
                File.WriteAllText(filePath, json, Encoding.UTF8);
            }
        }

        private static string ResolveTraceId(LogEntry entry)
        {
            if (TryReadString(entry.Payload, "decision_trace_id", out var traceId) && !string.IsNullOrWhiteSpace(traceId))
                return traceId!;

            if (!string.IsNullOrWhiteSpace(entry.CorrelationId))
                return entry.CorrelationId!;

            return $"decision_{entry.Seq:D6}";
        }

        private static bool TryReadString(Dictionary<string, object?> payload, string key, out string? value)
        {
            value = null;
            if (payload == null || !payload.TryGetValue(key, out var raw) || raw == null)
                return false;

            switch (raw)
            {
                case string text:
                    value = text;
                    return true;
                case JsonElement element when element.ValueKind == JsonValueKind.String:
                    value = element.GetString();
                    return true;
                case JsonElement element when element.ValueKind == JsonValueKind.Number:
                    value = element.ToString();
                    return true;
                default:
                    value = raw.ToString();
                    return !string.IsNullOrWhiteSpace(value);
            }
        }

        private static string SanitizePathSegment(string? value, string fallback)
        {
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            var cleaned = value.Trim();
            foreach (var invalid in Path.GetInvalidFileNameChars())
            {
                cleaned = cleaned.Replace(invalid, '_');
            }

            return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
        }
    }
}
