using System;
using System.IO;

namespace TractorGame.Core.Logging
{
    /// <summary>
    /// 统一创建默认日志管道。
    /// </summary>
    public static class GameLoggerFactory
    {
        private static readonly object SyncRoot = new();
        private static IGameLogger? _sharedLogger;

        public static IGameLogger CreateDefault()
        {
            if (IsDisabled())
                return NullGameLogger.Instance;

            lock (SyncRoot)
            {
                _sharedLogger ??= BuildLogger();
                return _sharedLogger;
            }
        }

        public static string GetDefaultLogRootPath()
        {
            var env = Environment.GetEnvironmentVariable("TRACTOR_LOG_ROOT");
            if (!string.IsNullOrWhiteSpace(env))
                return Path.GetFullPath(env);

            var repoRoot = ResolveRepoRoot();
            return Path.GetFullPath(Path.Combine(repoRoot, "logs", "raw"));
        }

        private static IGameLogger BuildLogger()
        {
            // WebAssembly 运行在浏览器沙箱内，不写本地文件。
            if (OperatingSystem.IsBrowser())
                return NullGameLogger.Instance;

            var rawRoot = GetDefaultLogRootPath();
            var repoRoot = ResolveRepoRoot();
            var replayRoot = Path.GetFullPath(Path.Combine(repoRoot, "logs", "replay"));
            var decisionRoot = Path.GetFullPath(Path.Combine(repoRoot, "logs", "decision"));

            var sink = new CompositeLogSink(
                new JsonLineLogSink(rawRoot),
                new MarkdownReplayLogSink(replayRoot),
                new AIDecisionBundleLogSink(decisionRoot));

            return new CoreLogger(sink);
        }

        private static bool IsDisabled()
        {
            var flag = Environment.GetEnvironmentVariable("TRACTOR_LOG_DISABLE");
            if (string.IsNullOrWhiteSpace(flag))
                return false;

            return flag == "1" || flag.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static string ResolveRepoRoot()
        {
            var fromCurrent = TryFindRepoRoot(Directory.GetCurrentDirectory());
            if (!string.IsNullOrWhiteSpace(fromCurrent))
                return fromCurrent;

            var fromBase = TryFindRepoRoot(AppContext.BaseDirectory);
            if (!string.IsNullOrWhiteSpace(fromBase))
                return fromBase;

            return Directory.GetCurrentDirectory();
        }

        private static string? TryFindRepoRoot(string startPath)
        {
            var dir = new DirectoryInfo(Path.GetFullPath(startPath));
            while (dir != null)
            {
                var hasProjectFile = File.Exists(Path.Combine(dir.FullName, "TractorGame.csproj"));
                var hasSolutionFile = File.Exists(Path.Combine(dir.FullName, "tractor.sln"));
                if (hasProjectFile || hasSolutionFile)
                    return dir.FullName;

                dir = dir.Parent;
            }

            return null;
        }
    }
}
