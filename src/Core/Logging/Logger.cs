using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    /// 人类可读日志 Sink（按 UTC 日期 + 小时切分）。
    /// </summary>
    public sealed class ReadableTextLogSink : ILogSink
    {
        private readonly string _rootPath;
        private readonly string _filePrefix;
        private readonly object _syncRoot = new();

        public ReadableTextLogSink(string rootPath = "logs/readable", string filePrefix = "tractor")
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
            var filePath = Path.Combine(dir, $"{_filePrefix}-{day}-{hour}.log");

            var line = BuildLine(entry, ts);

            lock (_syncRoot)
            {
                Directory.CreateDirectory(dir);
                File.AppendAllText(filePath, line + Environment.NewLine, Encoding.UTF8);
            }
        }

        private static string BuildLine(LogEntry entry, DateTime tsUtc)
        {
            var payload = FormatPayload(entry.Payload);
            var metrics = FormatMetrics(entry.Metrics);

            var parts = new List<string>
            {
                $"{tsUtc:HH:mm:ss.fff}Z",
                $"[{entry.Level}]",
                entry.Event
            };

            if (!string.IsNullOrWhiteSpace(entry.Category)) parts.Add($"cat={entry.Category}");
            if (!string.IsNullOrWhiteSpace(entry.GameId)) parts.Add($"game={entry.GameId}");
            if (!string.IsNullOrWhiteSpace(entry.RoundId)) parts.Add($"round={entry.RoundId}");
            if (!string.IsNullOrWhiteSpace(entry.Phase)) parts.Add($"phase={entry.Phase}");
            if (!string.IsNullOrWhiteSpace(entry.Actor)) parts.Add($"actor={entry.Actor}");
            if (entry.Seq > 0) parts.Add($"seq={entry.Seq}");
            if (!string.IsNullOrWhiteSpace(payload)) parts.Add($"payload={payload}");
            if (!string.IsNullOrWhiteSpace(metrics)) parts.Add($"metrics={metrics}");

            return string.Join(" | ", parts);
        }

        private static string FormatPayload(Dictionary<string, object?> payload)
        {
            if (payload == null || payload.Count == 0)
                return string.Empty;

            return string.Join(", ", payload.Take(8).Select(kv => $"{kv.Key}={SafeValue(kv.Value)}"));
        }

        private static string FormatMetrics(Dictionary<string, double> metrics)
        {
            if (metrics == null || metrics.Count == 0)
                return string.Empty;

            return string.Join(", ", metrics.Select(kv => $"{kv.Key}={kv.Value:0.###}"));
        }

        private static string SafeValue(object? value)
        {
            if (value == null)
                return "null";

            string text;
            if (value is string s)
            {
                text = s;
            }
            else
            {
                try
                {
                    text = JsonSerializer.Serialize(value);
                }
                catch
                {
                    text = value.ToString() ?? string.Empty;
                }
            }
            text = text.Replace(Environment.NewLine, " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (text.Length > 60)
                text = text.Substring(0, 57) + "...";

            return text;
        }
    }

    /// <summary>
    /// 组合 Sink：一条日志同时写入多个目标。
    /// </summary>
    public sealed class CompositeLogSink : ILogSink
    {
        private readonly IReadOnlyList<ILogSink> _sinks;

        public CompositeLogSink(params ILogSink[] sinks)
        {
            _sinks = sinks ?? Array.Empty<ILogSink>();
        }

        public void Write(LogEntry entry)
        {
            foreach (var sink in _sinks)
            {
                sink.Write(entry);
            }
        }
    }

    /// <summary>
    /// 对局回放 Markdown Sink：按“每墩”输出可读分析。
    /// </summary>
    public sealed class MarkdownReplayLogSink : ILogSink
    {
        private readonly string _rootPath;
        private readonly string _filePrefix;
        private readonly object _syncRoot = new();
        private readonly HashSet<string> _startedFiles = new();
        private readonly HashSet<string> _timelineStartedFiles = new();

        public MarkdownReplayLogSink(string rootPath = "logs/replay", string filePrefix = "tractor")
        {
            _rootPath = rootPath;
            _filePrefix = filePrefix;
        }

        public void Write(LogEntry entry)
        {
            var ts = entry.TsUtc.Kind == DateTimeKind.Utc ? entry.TsUtc : entry.TsUtc.ToUniversalTime();
            var filePath = GetFilePath(entry, ts);

            lock (_syncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _rootPath);

                if (!_startedFiles.Contains(filePath))
                {
                    _startedFiles.Add(filePath);
                    File.AppendAllText(filePath, BuildGameHeader(entry, ts), Encoding.UTF8);
                }

                var md = BuildEventMarkdown(entry, ts);
                if (!string.IsNullOrWhiteSpace(md))
                    File.AppendAllText(filePath, md, Encoding.UTF8);

                var textLine = BuildTextTimelineLine(entry, ts);
                if (!string.IsNullOrWhiteSpace(textLine))
                {
                    if (!_timelineStartedFiles.Contains(filePath))
                    {
                        _timelineStartedFiles.Add(filePath);
                        File.AppendAllText(filePath, "## 文本事件流\n\n", Encoding.UTF8);
                    }

                    File.AppendAllText(filePath, textLine + Environment.NewLine, Encoding.UTF8);
                }
            }
        }

        private string GetFilePath(LogEntry entry, DateTime tsUtc)
        {
            var day = tsUtc.ToString("yyyy-MM-dd");
            var dir = Path.Combine(_rootPath, day);
            var gameToken = string.IsNullOrWhiteSpace(entry.GameId)
                ? tsUtc.ToString("yyyyMMdd-HH")
                : entry.GameId!;
            return Path.Combine(dir, $"{_filePrefix}-{gameToken}.md");
        }

        private static string BuildGameHeader(LogEntry entry, DateTime tsUtc)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"# 拖拉机对局回放 ({tsUtc:yyyy-MM-dd HH:mm:ss} UTC)");
            sb.AppendLine($"- game_id: `{entry.GameId ?? "unknown"}`");
            sb.AppendLine($"- round_id: `{entry.RoundId ?? "unknown"}`");
            sb.AppendLine($"- session_id: `{entry.SessionId ?? "unknown"}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildEventMarkdown(LogEntry entry, DateTime tsUtc)
        {
            return entry.Event switch
            {
                "trump.finalized" => BuildTrumpMarkdown(entry, tsUtc),
                "turn.start" => BuildTurnStartMarkdown(entry, tsUtc),
                "trick.finish" => BuildTrickFinishMarkdown(entry, tsUtc),
                "round.finish" => BuildRoundFinishMarkdown(entry, tsUtc),
                _ => string.Empty
            };
        }

        private static string BuildTrumpMarkdown(LogEntry entry, DateTime tsUtc)
        {
            var trumpSuit = PayloadString(entry, "trump_suit");
            var trumpPlayer = PayloadString(entry, "trump_player");

            var sb = new StringBuilder();
            sb.AppendLine($"## 亮主信息 ({tsUtc:HH:mm:ss})");
            sb.AppendLine($"- 主花色: `{trumpSuit}`");
            sb.AppendLine($"- 亮主玩家: `player_{trumpPlayer}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTurnStartMarkdown(LogEntry entry, DateTime tsUtc)
        {
            if (!PayloadBool(entry, "is_lead"))
                return string.Empty;

            var trickNo = PayloadString(entry, "trick_no");
            var levelRank = PayloadString(entry, "level_rank");
            var trumpSuit = PayloadString(entry, "trump_suit");
            var dealerIndex = PayloadString(entry, "dealer_index");
            var defenderScore = PayloadString(entry, "defender_score");
            var leadPlayer = PayloadString(entry, "lead_player");

            var sb = new StringBuilder();
            sb.AppendLine($"## 第 {trickNo} 墩 ({tsUtc:HH:mm:ss})");
            sb.AppendLine($"- 打级: `{levelRank}`");
            sb.AppendLine($"- 主花色: `{trumpSuit}`");
            sb.AppendLine($"- 庄家: `player_{dealerIndex}`");
            sb.AppendLine($"- 首出玩家: `player_{leadPlayer}`");
            sb.AppendLine($"- 当前闲家分: `{defenderScore}`");
            sb.AppendLine();
            sb.AppendLine("### 四家手牌（出牌前）");
            sb.AppendLine("| 玩家 | 手牌数 | 手牌 |");
            sb.AppendLine("|---|---:|---|");

            var hands = PayloadArray(entry, "hands_before_trick");
            foreach (var hand in hands)
            {
                var player = PropertyString(hand, "player_index");
                var count = PropertyString(hand, "hand_count");
                var cards = string.Join(" ", CardTexts(PropertyArray(hand, "cards")));
                sb.AppendLine($"| player_{EscapeMd(player)} | {EscapeMd(count)} | {EscapeMd(cards)} |");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTrickFinishMarkdown(LogEntry entry, DateTime tsUtc)
        {
            var trickNo = PayloadString(entry, "trick_no");
            var winner = PayloadString(entry, "winner_index");
            var trickScore = PayloadString(entry, "trick_score");
            var before = PayloadString(entry, "defender_score_before");
            var after = PayloadString(entry, "defender_score_after");

            var basis = PayloadObject(entry, "winner_basis");
            var reason = PropertyString(basis, "reason");

            var sb = new StringBuilder();
            sb.AppendLine($"### 第 {trickNo} 墩结果 ({tsUtc:HH:mm:ss})");
            sb.AppendLine("| 玩家 | 出牌 | 牌型 | 类别 | 牌面分 |");
            sb.AppendLine("|---|---|---|---|---:|");

            var playAnalysis = PayloadArray(entry, "play_analysis");
            var playMap = playAnalysis
                .Select(p => new
                {
                    Player = PropertyString(p, "player_index"),
                    Pattern = PropertyString(p, "pattern"),
                    Category = PropertyString(p, "category"),
                    Score = PropertyString(p, "cards_score")
                })
                .ToDictionary(x => x.Player, x => x);

            var trickCards = PayloadArray(entry, "trick_cards");
            foreach (var play in trickCards)
            {
                var player = PropertyString(play, "player_index");
                var cards = string.Join(" ", CardTexts(PropertyArray(play, "cards")));
                playMap.TryGetValue(player, out var info);
                var pattern = info?.Pattern ?? "";
                var category = info?.Category ?? "";
                var score = info?.Score ?? "0";
                sb.AppendLine($"| player_{EscapeMd(player)} | {EscapeMd(cards)} | {EscapeMd(pattern)} | {EscapeMd(category)} | {EscapeMd(score)} |");
            }

            sb.AppendLine();
            sb.AppendLine($"- 赢家: `player_{winner}`");
            sb.AppendLine($"- 本墩分数: `{trickScore}`");
            sb.AppendLine($"- 闲家分变化: `{before} -> {after}`");
            sb.AppendLine($"- 判定依据: `{reason}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildRoundFinishMarkdown(LogEntry entry, DateTime tsUtc)
        {
            var defenderScore = PayloadString(entry, "defender_score");
            var winnerSide = PayloadString(entry, "winner_side");

            var sb = new StringBuilder();
            sb.AppendLine($"## 本局结束 ({tsUtc:HH:mm:ss})");
            sb.AppendLine($"- 闲家总分: `{defenderScore}`");
            sb.AppendLine($"- 获胜方: `{winnerSide}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTextTimelineLine(LogEntry entry, DateTime tsUtc)
        {
            var payload = FormatPayloadInline(entry.Payload);
            var metrics = FormatMetricsInline(entry.Metrics);

            var parts = new List<string>
            {
                $"- `{tsUtc:HH:mm:ss.fff}Z`",
                $"`[{entry.Level}]`",
                $"`{entry.Event}`"
            };

            if (!string.IsNullOrWhiteSpace(entry.Phase)) parts.Add($"phase=`{entry.Phase}`");
            if (!string.IsNullOrWhiteSpace(entry.Actor)) parts.Add($"actor=`{entry.Actor}`");
            if (entry.Seq > 0) parts.Add($"seq=`{entry.Seq}`");
            if (!string.IsNullOrWhiteSpace(payload)) parts.Add($"payload: {payload}");
            if (!string.IsNullOrWhiteSpace(metrics)) parts.Add($"metrics: {metrics}");

            return string.Join(" | ", parts);
        }

        private static string FormatPayloadInline(Dictionary<string, object?> payload)
        {
            if (payload == null || payload.Count == 0)
                return string.Empty;

            return string.Join(", ", payload.Take(10).Select(kv => $"{kv.Key}={InlineValue(kv.Value)}"));
        }

        private static string FormatMetricsInline(Dictionary<string, double> metrics)
        {
            if (metrics == null || metrics.Count == 0)
                return string.Empty;

            return string.Join(", ", metrics.Select(kv => $"{kv.Key}={kv.Value:0.###}"));
        }

        private static string InlineValue(object? value)
        {
            if (value == null)
                return "null";

            string text;
            if (value is string s)
            {
                text = s;
            }
            else
            {
                try
                {
                    text = JsonSerializer.Serialize(value);
                }
                catch
                {
                    text = value.ToString() ?? string.Empty;
                }
            }

            text = text.Replace(Environment.NewLine, " ").Replace('\n', ' ').Replace('\r', ' ').Trim();
            if (text.Length > 120)
                text = text.Substring(0, 117) + "...";

            return text.Replace("|", "\\|");
        }

        private static bool PayloadBool(LogEntry entry, string key)
        {
            if (!entry.Payload.TryGetValue(key, out var value) || value == null)
                return false;

            if (value is bool b) return b;
            if (bool.TryParse(value.ToString(), out var parsed)) return parsed;
            return false;
        }

        private static string PayloadString(LogEntry entry, string key)
        {
            if (!entry.Payload.TryGetValue(key, out var value) || value == null)
                return string.Empty;
            return ToSimpleString(value);
        }

        private static JsonElement PayloadObject(LogEntry entry, string key)
        {
            if (!entry.Payload.TryGetValue(key, out var value))
                return default;
            return ToElement(value);
        }

        private static List<JsonElement> PayloadArray(LogEntry entry, string key)
        {
            if (!entry.Payload.TryGetValue(key, out var value))
                return new List<JsonElement>();

            var el = ToElement(value);
            if (el.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();

            return el.EnumerateArray().Select(x => x.Clone()).ToList();
        }

        private static List<JsonElement> PropertyArray(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();

            return value.EnumerateArray().Select(x => x.Clone()).ToList();
        }

        private static string PropertyString(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var value))
                return string.Empty;
            return ToSimpleString(value);
        }

        private static IEnumerable<string> CardTexts(List<JsonElement> cardElements)
        {
            foreach (var card in cardElements)
            {
                var text = PropertyString(card, "text");
                if (!string.IsNullOrWhiteSpace(text))
                    yield return text;
            }
        }

        private static JsonElement ToElement(object? value)
        {
            if (value == null)
                return default;
            if (value is JsonElement el)
                return el.Clone();

            try
            {
                using var doc = JsonDocument.Parse(JsonSerializer.Serialize(value));
                return doc.RootElement.Clone();
            }
            catch
            {
                return default;
            }
        }

        private static string ToSimpleString(object value)
        {
            if (value is JsonElement el)
            {
                return el.ValueKind switch
                {
                    JsonValueKind.String => el.GetString() ?? string.Empty,
                    JsonValueKind.Number => el.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Null => string.Empty,
                    _ => el.GetRawText()
                };
            }

            return value.ToString() ?? string.Empty;
        }

        private static string EscapeMd(string value)
        {
            return (value ?? string.Empty).Replace("|", "\\|");
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
