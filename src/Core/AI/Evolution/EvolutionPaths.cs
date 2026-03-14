using System;
using System.IO;

namespace TractorGame.Core.AI.Evolution
{
    internal static class EvolutionPaths
    {
        public static string ResolveRepoRoot()
        {
            var fromCurrent = TryFindRoot(Directory.GetCurrentDirectory());
            if (!string.IsNullOrWhiteSpace(fromCurrent))
                return fromCurrent;

            var fromBase = TryFindRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(fromBase))
                return fromBase;

            return Directory.GetCurrentDirectory();
        }

        public static void EnsureEvolutionDirectories(EvolutionConfig config)
        {
            Directory.CreateDirectory(config.DataRootPath);
            Directory.CreateDirectory(config.ChampionsPath);
            Directory.CreateDirectory(config.CandidatesPath);
            Directory.CreateDirectory(config.ReportsPath);
            Directory.CreateDirectory(config.LogsPath);
        }

        private static string? TryFindRoot(string startPath)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startPath));
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "TractorGame.csproj")))
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }
    }
}
