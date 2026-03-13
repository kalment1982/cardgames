using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;

namespace TractorGame.Core.Logging
{
    public interface ILogSink
    {
        void Write(LogEntry entry);
    }

    public interface IGameLogger
    {
        void Log(LogEntry entry);
    }

    /// <summary>
    /// 空实现，保证日志异常不影响业务流。
    /// </summary>
    public sealed class NullGameLogger : IGameLogger
    {
        public static readonly NullGameLogger Instance = new();

        private NullGameLogger()
        {
        }

        public void Log(LogEntry entry)
        {
            // no-op
        }
    }

    /// <summary>
    /// 内存 Sink：用于测试和本地调试。
    /// </summary>
    public sealed class InMemoryLogSink : ILogSink
    {
        private readonly object _syncRoot = new();
        private readonly List<LogEntry> _entries = new();

        public IReadOnlyList<LogEntry> Entries
        {
            get
            {
                lock (_syncRoot)
                {
                    return _entries.ToArray();
                }
            }
        }

        public void Write(LogEntry entry)
        {
            lock (_syncRoot)
            {
                _entries.Add(entry);
            }
        }
    }

    /// <summary>
    /// JSONL 文件 Sink（按 UTC 日期 + 小时切分）。
    /// </summary>
    public sealed class JsonLineLogSink : ILogSink
    {
        private readonly string _rootPath;
        private readonly string _filePrefix;
        private readonly object _syncRoot = new();
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = false
        };

        public JsonLineLogSink(string rootPath = "logs/raw", string filePrefix = "tractor")
        {
            _rootPath = rootPath;
            _filePrefix = filePrefix;
        }

        public void Write(LogEntry entry)
        {
            var ts = entry.TsUtc.Kind == DateTimeKind.Utc ? entry.TsUtc : entry.TsUtc.ToUniversalTime();
            var day = ts.ToString("yyyy-MM-dd");
            var hour = ts.ToString("HH");

            var dir = Path.Combine(_rootPath, day);
            var filePath = Path.Combine(dir, $"{_filePrefix}-{day}-{hour}.jsonl");
            var json = JsonSerializer.Serialize(entry, _jsonOptions);

            lock (_syncRoot)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(filePath, json + Environment.NewLine, Encoding.UTF8);
            }
        }
    }

    /// <summary>
    /// 规范日志管道：统一补齐 ts/seq 并写入 sink。
    /// </summary>
    public sealed class CoreLogger : IGameLogger
    {
        private readonly ILogSink _sink;
        private readonly ConcurrentDictionary<string, long> _roundSeq = new();
        private long _globalSeq;
        private long _auditDropped;

        public CoreLogger(ILogSink sink)
        {
            _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        }

        public long AuditDroppedCount => Interlocked.Read(ref _auditDropped);

        public void Log(LogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Event))
                return;

            if (entry.TsUtc == default)
                entry.TsUtc = DateTime.UtcNow;
            else if (entry.TsUtc.Kind != DateTimeKind.Utc)
                entry.TsUtc = entry.TsUtc.ToUniversalTime();

            if (entry.Seq <= 0)
                entry.Seq = NextSeq(entry.RoundId);

            if (entry.Payload == null)
                entry.Payload = new Dictionary<string, object?>();
            if (entry.Metrics == null)
                entry.Metrics = new Dictionary<string, double>();

            try
            {
                _sink.Write(entry);
            }
            catch (Exception ex)
            {
                // 不抛出异常，保证主流程不受影响。
                if (string.Equals(entry.Category, LogCategories.Audit, StringComparison.OrdinalIgnoreCase))
                    Interlocked.Increment(ref _auditDropped);

                try
                {
                    Console.Error.WriteLine($"[CoreLogger] sink write failed: {ex.GetType().Name}: {ex.Message}");
                }
                catch
                {
                    // Ignore secondary failures.
                }
            }
        }

        private long NextSeq(string? roundId)
        {
            if (!string.IsNullOrWhiteSpace(roundId))
                return _roundSeq.AddOrUpdate(roundId, 1, (_, current) => current + 1);

            return Interlocked.Increment(ref _globalSeq);
        }
    }
}
