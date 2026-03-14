using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TractorGame.Core.AI.Evolution.DataEngine
{
    public sealed class LogReader
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        public IEnumerable<GameEvent> ReadEvents(string logPath)
        {
            if (string.IsNullOrWhiteSpace(logPath) || !File.Exists(logPath))
                yield break;

            using var stream = File.OpenRead(logPath);
            using var reader = new StreamReader(stream);
            while (!reader.EndOfStream)
            {
                var line = reader.ReadLine();
                if (string.IsNullOrWhiteSpace(line))
                    continue;

                GameEvent? evt;
                try
                {
                    evt = JsonSerializer.Deserialize<GameEvent>(line, JsonOptions);
                }
                catch
                {
                    continue;
                }

                if (evt != null)
                    yield return evt;
            }
        }

        public IEnumerable<GameEvent> ReadEventsFromDirectory(string rootPath)
        {
            if (string.IsNullOrWhiteSpace(rootPath) || !Directory.Exists(rootPath))
                yield break;

            foreach (var file in Directory.EnumerateFiles(rootPath, "*.jsonl", SearchOption.AllDirectories))
            {
                foreach (var evt in ReadEvents(file))
                    yield return evt;
            }
        }
    }
}
