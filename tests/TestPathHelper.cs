using System;
using System.IO;
using System.Linq;

namespace TractorGame.Tests
{
    internal static class TestPathHelper
    {
        public static string ResolveFromRepoRoot(params string[] parts)
        {
            var root = FindRepoRoot();
            return Path.Combine(new[] { root }.Concat(parts).ToArray());
        }

        private static string FindRepoRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                var csproj = Path.Combine(dir.FullName, "TractorGame.csproj");
                if (File.Exists(csproj))
                    return dir.FullName;
                dir = dir.Parent;
            }

            // Fallback to current directory to avoid hard failure.
            return Directory.GetCurrentDirectory();
        }
    }
}
