using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Evolution.PolicyFactory;

namespace TractorGame.Core.AI.Evolution.ReleaseManager
{
    public sealed class ChampionRegistry
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public ChampionSnapshot LoadOrCreateSeed(EvolutionConfig config)
        {
            var currentPath = CurrentChampionPath(config);
            if (File.Exists(currentPath))
            {
                var json = File.ReadAllText(currentPath);
                var snapshot = JsonSerializer.Deserialize<ChampionSnapshot>(json);
                if (snapshot != null)
                    return snapshot;
            }

            var seed = new ChampionSnapshot
            {
                ChampionId = "champion_v0",
                Generation = 0,
                Parameters = AIStrategyParameters.CreatePreset(config.EvaluationDifficulty).Normalize(),
                CreatedAtUtc = DateTime.UtcNow
            };
            seed.GenomeHash = new ParameterGenome(seed.Parameters).ComputeHash();

            SaveSnapshot(config, seed, "seed");
            return seed;
        }

        public ChampionSnapshot Promote(EvolutionConfig config, CandidateEvaluation winner)
        {
            var snapshot = new ChampionSnapshot
            {
                ChampionId = $"champion_v{winner.Candidate.Generation}",
                Generation = winner.Candidate.Generation,
                Parameters = winner.Candidate.Parameters.Clone(),
                GenomeHash = winner.Candidate.GenomeHash,
                CreatedAtUtc = DateTime.UtcNow
            };

            SaveSnapshot(config, snapshot, "promote");
            return snapshot;
        }

        public void AppendRegistryEntry(EvolutionConfig config, object record)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(config.RegistryFilePath) ?? config.DataRootPath);
            var line = JsonSerializer.Serialize(record);
            File.AppendAllText(config.RegistryFilePath, line + Environment.NewLine);
            NormalizeRegistry(config.RegistryFilePath);
        }

        private void SaveSnapshot(EvolutionConfig config, ChampionSnapshot snapshot, string reason)
        {
            Directory.CreateDirectory(config.ChampionsPath);
            var currentPath = CurrentChampionPath(config);
            var versionPath = Path.Combine(config.ChampionsPath, $"{snapshot.ChampionId}_{snapshot.CreatedAtUtc:yyyyMMdd_HHmmss}.json");
            var json = JsonSerializer.Serialize(snapshot, JsonOptions);

            File.WriteAllText(currentPath, json);
            File.WriteAllText(versionPath, json);

            AppendRegistryEntry(config, new
            {
                ts_utc = DateTime.UtcNow,
                action = reason,
                champion_id = snapshot.ChampionId,
                generation = snapshot.Generation,
                genome_hash = snapshot.GenomeHash
            });

            PruneHistory(config, keep: 5);
        }

        private static void PruneHistory(EvolutionConfig config, int keep)
        {
            var files = new DirectoryInfo(config.ChampionsPath)
                .GetFiles("champion_v*.json", SearchOption.TopDirectoryOnly)
                .OrderByDescending(file => file.CreationTimeUtc)
                .ToList();

            foreach (var file in files.Skip(Math.Max(0, keep)))
            {
                try
                {
                    file.Delete();
                }
                catch
                {
                    // Keep best effort.
                }
            }
        }

        private static string CurrentChampionPath(EvolutionConfig config)
        {
            return Path.Combine(config.ChampionsPath, "champion_current.json");
        }

        private static void NormalizeRegistry(string registryPath)
        {
            if (string.IsNullOrWhiteSpace(registryPath) || !File.Exists(registryPath))
                return;

            var lines = File.ReadAllLines(registryPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToList();
            if (lines.Count == 0)
                return;

            var lastIndexByKey = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var i = 0; i < lines.Count; i++)
            {
                var key = BuildDedupKey(lines[i]);
                if (key == null)
                    continue;

                lastIndexByKey[key] = i;
            }

            var normalized = new List<string>(lines.Count);
            for (var i = 0; i < lines.Count; i++)
            {
                var key = BuildDedupKey(lines[i]);
                if (key == null || lastIndexByKey[key] == i)
                    normalized.Add(lines[i]);
            }

            File.WriteAllLines(registryPath, normalized);
        }

        private static string? BuildDedupKey(string line)
        {
            try
            {
                using var doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                if (!root.TryGetProperty("action", out var actionElement) || actionElement.ValueKind != JsonValueKind.String)
                    return null;

                var action = actionElement.GetString();
                if (string.IsNullOrWhiteSpace(action))
                    return null;

                if (!string.Equals(action, "generation_complete", StringComparison.Ordinal)
                    && !string.Equals(action, "promote", StringComparison.Ordinal))
                {
                    return null;
                }

                if (!root.TryGetProperty("generation", out var generationElement) || generationElement.ValueKind != JsonValueKind.Number)
                    return null;

                return $"{action}:{generationElement.GetInt32()}";
            }
            catch
            {
                return null;
            }
        }
    }
}
