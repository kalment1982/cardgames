using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.Evolution.DataEngine
{
    public sealed class DataQualityChecker
    {
        public DataQualityReport Check(IEnumerable<GameEvent> events)
        {
            var list = events?.ToList() ?? new List<GameEvent>();
            var report = new DataQualityReport
            {
                TotalEvents = list.Count,
                SchemaMatchedEvents = list.Count(e => IsSupportedSchema(e.SchemaVersion)),
                AiDecisionEvents = list.Count(e => string.Equals(e.Event, "ai.decision", StringComparison.OrdinalIgnoreCase))
            };

            var eventKeys = new HashSet<string>(StringComparer.Ordinal);
            var gameIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var evt in list)
            {
                var gameId = string.IsNullOrWhiteSpace(evt.GameId)
                    ? evt.RoundId ?? "-"
                    : evt.GameId;
                var key = $"{gameId}:{evt.Seq}";
                if (!eventKeys.Add(key))
                    report.DuplicateEventCount++;

                if (!string.IsNullOrWhiteSpace(evt.GameId))
                    gameIds.Add(evt.GameId!);
            }

            report.UniqueEvents = eventKeys.Count;
            report.GamesCount = gameIds.Count;
            report.CompleteAiDecisionEvents = list.Count(IsCompleteAiDecisionEvent);
            return report;
        }

        private static bool IsSupportedSchema(string schema)
        {
            if (string.IsNullOrWhiteSpace(schema))
                return false;

            return schema == "1.1" || schema == "1.2";
        }

        private static bool IsCompleteAiDecisionEvent(GameEvent evt)
        {
            if (!string.Equals(evt.Event, "ai.decision", StringComparison.OrdinalIgnoreCase))
                return false;

            var payload = evt.Payload;
            var metrics = evt.Metrics;

            if (payload == null || metrics == null)
                return false;

            var payloadOk = payload.ContainsKey("ai_player_index")
                            && payload.ContainsKey("ai_difficulty")
                            && payload.ContainsKey("decision_type")
                            && payload.ContainsKey("candidate_count")
                            && payload.ContainsKey("selected_cards");

            var metricsOk = metrics.ContainsKey("decision_latency_ms")
                            && metrics.ContainsKey("is_legal")
                            && metrics.ContainsKey("is_blunder")
                            && metrics.ContainsKey("is_optimal");

            return payloadOk && metricsOk;
        }
    }
}
