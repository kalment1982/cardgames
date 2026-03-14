using System;
using System.IO;
using System.Text.Json;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// 加载训练好的Champion AI参数
    /// </summary>
    public static class ChampionLoader
    {
        private static AIStrategyParameters? _cachedChampion;
        private static DateTime _lastLoadTime = DateTime.MinValue;
        private static readonly TimeSpan CacheExpiry = TimeSpan.FromMinutes(5);

        public static AIStrategyParameters LoadChampion()
        {
            // 缓存5分钟，避免频繁读文件
            if (_cachedChampion != null && DateTime.UtcNow - _lastLoadTime < CacheExpiry)
            {
                return _cachedChampion.Clone();
            }

            try
            {
                // 尝试多个可能的路径
                var possiblePaths = new[]
                {
                    "data/evolution/champions/champion_current.json",
                    "../data/evolution/champions/champion_current.json",
                    "../../data/evolution/champions/champion_current.json",
                    "/Users/kalment/projects/tractor/cardgames/data/evolution/champions/champion_current.json"
                };

                foreach (var path in possiblePaths)
                {
                    if (File.Exists(path))
                    {
                        var json = File.ReadAllText(path);
                        var championData = JsonSerializer.Deserialize<ChampionData>(json);

                        if (championData?.Parameters != null)
                        {
                            _cachedChampion = championData.Parameters;
                            _lastLoadTime = DateTime.UtcNow;
                            Console.WriteLine($"[ChampionLoader] Loaded champion_v{championData.Generation} from {path}");
                            return _cachedChampion.Clone();
                        }
                    }
                }

                Console.WriteLine("[ChampionLoader] Champion file not found, using Hard preset");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ChampionLoader] Error loading champion: {ex.Message}");
            }

            // 如果加载失败，返回Hard预设
            return AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
        }

        private class ChampionData
        {
            public string? ChampionId { get; set; }
            public int Generation { get; set; }
            public AIStrategyParameters? Parameters { get; set; }
        }
    }
}
