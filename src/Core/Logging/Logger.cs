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
            var tsUtc = entry.TsUtc.Kind == DateTimeKind.Utc ? entry.TsUtc : entry.TsUtc.ToUniversalTime();
            var tsLocal = tsUtc.ToLocalTime();
            var filePath = GetFilePath(entry, tsUtc);

            lock (_syncRoot)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(filePath) ?? _rootPath);

                if (!_startedFiles.Contains(filePath))
                {
                    _startedFiles.Add(filePath);
                    File.AppendAllText(filePath, BuildGameHeader(entry, tsLocal), Encoding.UTF8);
                }

                var md = BuildEventMarkdown(entry, tsLocal);
                if (!string.IsNullOrWhiteSpace(md))
                    File.AppendAllText(filePath, md, Encoding.UTF8);

                var textLine = BuildTextTimelineLine(entry, tsLocal);
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

        private static string BuildGameHeader(LogEntry entry, DateTime tsLocal)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"# 拖拉机对局回放 ({tsLocal:yyyy-MM-dd HH:mm:ss} 本地时间)");
            sb.AppendLine($"- game_id: `{entry.GameId ?? "unknown"}`");
            sb.AppendLine($"- round_id: `{entry.RoundId ?? "unknown"}`");
            sb.AppendLine($"- session_id: `{entry.SessionId ?? "unknown"}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildEventMarkdown(LogEntry entry, DateTime tsLocal)
        {
            return entry.Event switch
            {
                "trump.finalized" => BuildTrumpMarkdown(entry, tsLocal),
                "turn.start" => BuildTurnStartMarkdown(entry, tsLocal),
                "trick.finish" => BuildTrickFinishMarkdown(entry, tsLocal),
                "round.finish" => BuildRoundFinishMarkdown(entry, tsLocal),
                _ => string.Empty
            };
        }

        private static string BuildTrumpMarkdown(LogEntry entry, DateTime tsLocal)
        {
            var trumpSuit = PayloadStringAny(entry, "trump_suit", "trumpSuit", "suit");
            var trumpPlayer = PayloadStringAny(entry, "trump_player", "trumpPlayer", "player_index", "playerIndex");

            var sb = new StringBuilder();
            sb.AppendLine($"## 亮主信息 ({tsLocal:HH:mm:ss})");
            sb.AppendLine($"- 主花色: `{SuitDisplay(trumpSuit)}`");
            sb.AppendLine($"- 亮主玩家: `{PlayerLabel(trumpPlayer)}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTurnStartMarkdown(LogEntry entry, DateTime tsLocal)
        {
            if (!PayloadBool(entry, "is_lead"))
                return string.Empty;

            var trickNo = PayloadStringAny(entry, "trick_no", "trickNo");
            var levelRank = PayloadStringAny(entry, "level_rank", "levelRank");
            var trumpSuit = PayloadStringAny(entry, "trump_suit", "trumpSuit");
            var dealerIndex = PayloadStringAny(entry, "dealer_index", "dealerIndex");
            var defenderScore = PayloadStringAny(entry, "defender_score", "defenderScore");
            var leadPlayer = PayloadStringAny(entry, "lead_player", "leadPlayer");

            var sb = new StringBuilder();
            sb.AppendLine($"## 第 {trickNo} 墩 ({tsLocal:HH:mm:ss})");
            sb.AppendLine($"- 打级: `{RankDisplay(levelRank)}`");
            sb.AppendLine($"- 主花色: `{SuitDisplay(trumpSuit)}`");
            sb.AppendLine($"- 庄家: `{PlayerLabel(dealerIndex)}`");
            sb.AppendLine($"- 首出玩家: `{PlayerLabel(leadPlayer)}`");
            sb.AppendLine($"- 当前闲家分: `{defenderScore}`");
            sb.AppendLine();
            sb.AppendLine("### 四家手牌（出牌前）");
            sb.AppendLine("| 玩家 | 手牌数 | 手牌（人性化分组） |");
            sb.AppendLine("|---|---:|---|");

            var hands = PayloadArray(entry, "hands_before_trick");
            foreach (var hand in hands)
            {
                var player = PropertyStringAny(hand, "player_index", "playerIndex");
                var count = PropertyStringAny(hand, "hand_count", "handCount");
                var cards = PropertyArrayAny(hand, "cards");
                var grouped = HumanizeCards(cards, levelRank, trumpSuit);
                sb.AppendLine($"| {EscapeMd(PlayerLabel(player))} | {EscapeMd(count)} | {EscapeMd(grouped)} |");
            }
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTrickFinishMarkdown(LogEntry entry, DateTime tsLocal)
        {
            var trickNo = PayloadStringAny(entry, "trick_no", "trickNo", "trickIndex");
            var winner = PayloadStringAny(entry, "winner_index", "winnerIndex", "winner");
            var trickScore = PayloadStringAny(entry, "trick_score", "trickScore");
            var before = PayloadStringAny(entry, "defender_score_before", "defenderScoreBefore");
            var after = PayloadStringAny(entry, "defender_score_after", "defenderScoreAfter");

            var basis = PayloadObject(entry, "winner_basis");
            var reason = PropertyString(basis, "reason");

            var sb = new StringBuilder();
            sb.AppendLine($"### 第 {trickNo} 墩结果 ({tsLocal:HH:mm:ss})");
            sb.AppendLine("| 玩家 | 出牌 | 牌型 | 类别 | 牌面分 | 甩牌回退 | 牌张属性 |");
            sb.AppendLine("|---|---|---|---|---:|---|---|");

            var playAnalysis = PayloadArray(entry, "play_analysis");
            var playMap = playAnalysis
                .Select(p => new
                {
                    Player = PropertyStringAny(p, "player_index", "playerIndex"),
                    Pattern = PropertyString(p, "pattern"),
                    Category = PropertyString(p, "category"),
                    Score = PropertyStringAny(p, "cards_score", "cardsScore"),
                    FallbackPatternType = PropertyStringAny(p, "fallback_pattern_type", "fallbackPatternType")
                })
                .ToDictionary(x => x.Player, x => x);

            var trickCards = PayloadArrayAny(entry, "trick_cards", "plays");
            foreach (var play in trickCards)
            {
                var player = PropertyStringAny(play, "player_index", "playerIndex");
                var cardsEl = PropertyArrayAny(play, "cards");
                var cards = string.Join(" ", CardDisplays(cardsEl));
                playMap.TryGetValue(player, out var info);
                var pattern = info?.Pattern ?? "";
                var category = info?.Category ?? "";
                var score = info?.Score ?? cardsEl.Sum(c => PropertyInt(c, "score")).ToString();
                var fallback = info?.FallbackPatternType ?? "";
                var traits = DescribePlayTraits(
                    cardsEl,
                    trickCards,
                    player,
                    trumpSuit: PayloadStringAny(entry, "trump_suit", "trumpSuit"),
                    levelRank: PayloadStringAny(entry, "level_rank", "levelRank"));
                sb.AppendLine($"| {EscapeMd(PlayerLabel(player))} | {EscapeMd(cards)} | {EscapeMd(pattern)} | {EscapeMd(category)} | {EscapeMd(score)} | {EscapeMd(fallback)} | {EscapeMd(traits)} |");
            }

            sb.AppendLine();
            sb.AppendLine($"- 赢家: `{PlayerLabel(winner)}`");
            sb.AppendLine($"- 下一首出玩家 = `{PlayerLabel(winner)}`");
            sb.AppendLine($"- 本墩分数: `{trickScore}`");
            sb.AppendLine($"- 闲家分变化: `{before} -> {after}`");
            var reasonText = string.IsNullOrWhiteSpace(reason) ? "来自事件字段 winner/winnerIndex" : reason;
            sb.AppendLine($"- 判定依据: `{reasonText}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildRoundFinishMarkdown(LogEntry entry, DateTime tsLocal)
        {
            var defenderScore = PayloadString(entry, "defender_score");
            var winnerSide = PayloadString(entry, "winner_side");

            var sb = new StringBuilder();
            sb.AppendLine($"## 本局结束 ({tsLocal:HH:mm:ss})");
            sb.AppendLine($"- 闲家总分: `{defenderScore}`");
            sb.AppendLine($"- 获胜方: `{winnerSide}`");
            sb.AppendLine();
            return sb.ToString();
        }

        private static string BuildTextTimelineLine(LogEntry entry, DateTime tsLocal)
        {
            var payload = FormatPayloadInline(entry.Payload);
            var metrics = FormatMetricsInline(entry.Metrics);

            var parts = new List<string>
            {
                $"- `{tsLocal:HH:mm:ss.fff}`",
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

        private static string PayloadStringAny(LogEntry entry, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (entry.Payload.TryGetValue(key, out var value) && value != null)
                    return ToSimpleString(value);
            }

            return string.Empty;
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

        private static List<JsonElement> PayloadArrayAny(LogEntry entry, params string[] keys)
        {
            foreach (var key in keys)
            {
                var arr = PayloadArray(entry, key);
                if (arr.Count > 0)
                    return arr;
            }

            return new List<JsonElement>();
        }

        private static List<JsonElement> PropertyArray(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var value) || value.ValueKind != JsonValueKind.Array)
                return new List<JsonElement>();

            return value.EnumerateArray().Select(x => x.Clone()).ToList();
        }

        private static List<JsonElement> PropertyArrayAny(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                var arr = PropertyArray(obj, key);
                if (arr.Count > 0)
                    return arr;
            }

            return new List<JsonElement>();
        }

        private static string PropertyString(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var value))
                return string.Empty;
            return ToSimpleString(value);
        }

        private static string PropertyStringAny(JsonElement obj, params string[] keys)
        {
            foreach (var key in keys)
            {
                var value = PropertyString(obj, key);
                if (!string.IsNullOrWhiteSpace(value))
                    return value;
            }

            return string.Empty;
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

        private static IEnumerable<string> CardDisplays(List<JsonElement> cardElements)
        {
            foreach (var card in cardElements)
            {
                yield return CardDisplay(card);
            }
        }

        private static string CardDisplay(JsonElement card)
        {
            var rank = PropertyString(card, "rank");
            var suit = PropertyString(card, "suit");
            if (string.Equals(rank, "BigJoker", StringComparison.OrdinalIgnoreCase))
                return "大🃏";
            if (string.Equals(rank, "SmallJoker", StringComparison.OrdinalIgnoreCase))
                return "小🃏";

            return $"{SuitShort(suit)}{RankDisplay(rank)}";
        }

        private static string HumanizeCards(List<JsonElement> cards, string levelRank, string trumpSuit)
        {
            if (cards.Count == 0)
                return "(空)";

            var grouped = new Dictionary<string, List<string>>
            {
                ["主"] = new List<string>(),
                ["♠"] = new List<string>(),
                ["♥"] = new List<string>(),
                ["♣"] = new List<string>(),
                ["♦"] = new List<string>()
            };

            int scoreCards = 0;
            int scoreSum = 0;
            foreach (var card in cards)
            {
                var display = CardDisplay(card);
                var suit = PropertyString(card, "suit");
                var rank = PropertyString(card, "rank");
                if (IsTrumpCard(suit, rank, trumpSuit, levelRank))
                    grouped["主"].Add(display);
                else
                    grouped[SuitShort(suit)].Add(display);

                var cardScore = PropertyInt(card, "score");
                if (cardScore > 0)
                {
                    scoreCards++;
                    scoreSum += cardScore;
                }
            }

            var parts = new List<string>();
            foreach (var key in new[] { "主", "♠", "♥", "♣", "♦" })
            {
                if (grouped[key].Count > 0)
                    parts.Add($"{key}[{string.Join(" ", grouped[key])}]");
            }

            var scoreText = scoreCards > 0 ? $"分牌{scoreCards}张/{scoreSum}分" : "无分牌";
            return $"{string.Join(" ｜ ", parts)} （共{cards.Count}张，{scoreText}）";
        }

        private static string DescribePlayTraits(List<JsonElement> cards, List<JsonElement> trickPlays, string player, string trumpSuit, string levelRank)
        {
            if (cards.Count == 0)
                return string.Empty;

            var traits = new List<string>();
            if (cards.All(c => IsTrumpCard(PropertyString(c, "suit"), PropertyString(c, "rank"), trumpSuit, levelRank)))
                traits.Add("主牌");
            else
                traits.Add("副牌");

            if (cards.Any(c => string.Equals(PropertyString(c, "rank"), levelRank, StringComparison.OrdinalIgnoreCase)))
                traits.Add("含级牌");

            var score = cards.Sum(c => PropertyInt(c, "score"));
            if (score > 0)
                traits.Add($"分牌{score}分");

            var firstPlayer = string.Empty;
            var leadCategory = string.Empty;
            if (trickPlays.Count > 0)
            {
                var leadPlay = trickPlays[0];
                firstPlayer = PropertyStringAny(leadPlay, "player_index", "playerIndex");
                var leadCards = PropertyArrayAny(leadPlay, "cards");
                if (leadCards.Count > 0)
                    leadCategory = CardCategory(leadCards[0], trumpSuit, levelRank);
            }

            if (string.Equals(firstPlayer, player, StringComparison.Ordinal))
                traits.Add("首攻");
            else
            {
                var selfCategory = CardCategory(cards[0], trumpSuit, levelRank);
                traits.Add(selfCategory == leadCategory ? "跟同门" : "垫牌/毙牌");
            }

            return string.Join(" · ", traits);
        }

        private static string CardCategory(JsonElement card, string trumpSuit, string levelRank)
        {
            var suit = PropertyString(card, "suit");
            var rank = PropertyString(card, "rank");
            return IsTrumpCard(suit, rank, trumpSuit, levelRank) ? "Trump" : suit;
        }

        private static bool IsTrumpCard(string suit, string rank, string trumpSuit, string levelRank)
        {
            if (string.Equals(rank, "BigJoker", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(rank, "SmallJoker", StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.IsNullOrWhiteSpace(levelRank) &&
                string.Equals(rank, levelRank, StringComparison.OrdinalIgnoreCase))
                return true;

            return !string.IsNullOrWhiteSpace(trumpSuit) &&
                   string.Equals(suit, trumpSuit, StringComparison.OrdinalIgnoreCase);
        }

        private static int PropertyInt(JsonElement obj, string key)
        {
            if (obj.ValueKind != JsonValueKind.Object || !obj.TryGetProperty(key, out var value))
                return 0;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var i))
                return i;

            if (int.TryParse(ToSimpleString(value), out var parsed))
                return parsed;

            return 0;
        }

        private static string PlayerLabel(string playerIndex)
        {
            if (!int.TryParse(playerIndex, out var p))
                return "未知";
            return $"{SeatName(p)}家";
        }

        private static string SeatName(int player) => player switch
        {
            0 => "南",
            1 => "东",
            2 => "北",
            3 => "西",
            _ => "?"
        };

        private static string SuitDisplay(string suit) => suit switch
        {
            "Spade" => "♠黑桃",
            "Heart" => "♥红桃",
            "Club" => "♣梅花",
            "Diamond" => "♦方块",
            _ => suit
        };

        private static string SuitShort(string suit) => suit switch
        {
            "Spade" => "♠",
            "Heart" => "♥",
            "Club" => "♣",
            "Diamond" => "♦",
            _ => "?"
        };

        private static string RankDisplay(string rank) => rank switch
        {
            "Two" => "2",
            "Three" => "3",
            "Four" => "4",
            "Five" => "5",
            "Six" => "6",
            "Seven" => "7",
            "Eight" => "8",
            "Nine" => "9",
            "Ten" => "10",
            "Jack" => "J",
            "Queen" => "Q",
            "King" => "K",
            "Ace" => "A",
            "SmallJoker" => "小🃏",
            "BigJoker" => "大🃏",
            _ => rank
        };

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
