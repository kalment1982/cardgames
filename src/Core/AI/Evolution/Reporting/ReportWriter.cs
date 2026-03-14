using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using TractorGame.Core.AI.Evolution.DataEngine;

namespace TractorGame.Core.AI.Evolution.Reporting
{
    public sealed class ReportWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public string WriteGenerationReport(
            EvolutionConfig config,
            EvolutionRunResult result,
            IReadOnlyList<CandidateEvaluation> layer1,
            IReadOnlyList<CandidateEvaluation> layer2,
            IReadOnlyList<CandidateEvaluation> layer3)
        {
            Directory.CreateDirectory(config.ReportsPath);
            var reportPath = Path.Combine(config.ReportsPath, $"generation_{result.Generation:D3}_report.md");

            var sb = new StringBuilder();
            sb.AppendLine($"# Evolution Generation {result.Generation:D3} Report");
            sb.AppendLine();
            sb.AppendLine($"- Started (UTC): {result.StartedAtUtc:O}");
            sb.AppendLine($"- Finished (UTC): {result.FinishedAtUtc:O}");
            sb.AppendLine($"- Candidate count: {result.CandidateCount}");
            sb.AppendLine($"- Promoted: {(result.Promoted ? "YES" : "NO")}");
            sb.AppendLine($"- Reason: {result.PromotionReason}");
            sb.AppendLine($"- Champion before: {result.ChampionBeforeHash}");
            sb.AppendLine($"- Champion after: {result.ChampionAfterHash}");
            sb.AppendLine();

            if (result.DataQualityReport != null)
            {
                WriteDataQuality(sb, result.DataQualityReport);
            }

            WriteLayerTable(sb, "Layer1", layer1);
            WriteLayerTable(sb, "Layer2", layer2);
            WriteLayerTable(sb, "Layer3", layer3);

            File.WriteAllText(reportPath, sb.ToString(), Encoding.UTF8);
            return reportPath;
        }

        public void WriteCandidateParameters(EvolutionConfig config, IReadOnlyList<CandidateProfile> candidates)
        {
            Directory.CreateDirectory(config.CandidatesPath);
            foreach (var candidate in candidates)
            {
                var filePath = Path.Combine(config.CandidatesPath, $"{candidate.CandidateId}.json");
                var content = JsonSerializer.Serialize(candidate, JsonOptions);
                File.WriteAllText(filePath, content);
            }
        }

        private static void WriteDataQuality(StringBuilder sb, DataQualityReport report)
        {
            sb.AppendLine("## Data Quality");
            sb.AppendLine();
            sb.AppendLine("| Metric | Value |");
            sb.AppendLine("|---|---|");
            sb.AppendLine($"| Generation | {report.Generation} |");
            sb.AppendLine($"| Total events | {report.TotalEvents} |");
            sb.AppendLine($"| Unique events | {report.UniqueEvents} |");
            sb.AppendLine($"| Schema match rate | {report.SchemaMatchRate:P2} |");
            sb.AppendLine($"| ai.decision events | {report.AiDecisionEvents} |");
            sb.AppendLine($"| ai.decision completeness | {report.AiDecisionCompleteness:P2} |");
            sb.AppendLine($"| Duplicate events | {report.DuplicateEventCount} |");
            sb.AppendLine($"| Duplicate rate | {report.DuplicateRate:P2} |");
            sb.AppendLine($"| Games count | {report.GamesCount} |");
            sb.AppendLine($"| Avg events/game | {report.AvgEventsPerGame:F2} |");
            sb.AppendLine();
        }

        private static void WriteLayerTable(StringBuilder sb, string title, IReadOnlyList<CandidateEvaluation> rows)
        {
            sb.AppendLine($"## {title}");
            sb.AppendLine();
            sb.AppendLine("| Candidate | Games | WinRate | Dealer WR | Defender WR | CI Low | CI High | Illegal | Avg Lat(ms) | P99(ms) | Diversity |");
            sb.AppendLine("|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|");

            foreach (var row in rows.OrderByDescending(r => r.WinRate))
            {
                sb.AppendLine(string.Format(
                    CultureInfo.InvariantCulture,
                    "| {0} | {1} | {2:P2} | {3:P2} | {4:P2} | {5:P2} | {6:P2} | {7:P2} | {8:F2} | {9:F2} | {10:P2} |",
                    row.Candidate.CandidateId,
                    row.Games,
                    row.WinRate,
                    row.CandidateDealerWinRate,
                    row.CandidateDefenderWinRate,
                    row.WinRateCiLow,
                    row.WinRateCiHigh,
                    row.CandidateIllegalRate,
                    row.CandidateAvgLatencyMs,
                    row.CandidateP99LatencyMs,
                    row.CandidateDiversity));
            }

            sb.AppendLine();
        }
    }
}
