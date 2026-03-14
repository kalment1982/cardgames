using System;
using System.IO;
using System.Text.Json;

namespace TractorGame.Core.AI.Evolution.ReleaseManager
{
    public sealed class EvolutionStateStore
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public EvolutionState Load(EvolutionConfig config, string championHash)
        {
            if (File.Exists(config.StateFilePath))
            {
                var json = File.ReadAllText(config.StateFilePath);
                var state = JsonSerializer.Deserialize<EvolutionState>(json);
                if (state != null)
                    return state;
            }

            return new EvolutionState
            {
                Generation = 0,
                ConsecutiveNoPromotion = 0,
                CurrentChampionHash = championHash,
                UpdatedAtUtc = DateTime.UtcNow
            };
        }

        public void Save(EvolutionConfig config, EvolutionState state)
        {
            state.UpdatedAtUtc = DateTime.UtcNow;
            Directory.CreateDirectory(Path.GetDirectoryName(config.StateFilePath) ?? config.DataRootPath);
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(config.StateFilePath, json);
        }
    }
}
