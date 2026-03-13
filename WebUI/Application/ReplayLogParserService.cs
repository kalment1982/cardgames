using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace WebUI.Application;

public sealed class ReplayLogParserService
{
    public ReplayParseResult Parse(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            return ReplayParseResult.Fail("日志内容为空。");
        }

        var jsonLines = content
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.StartsWith("{", StringComparison.Ordinal) && line.Contains("\"event\"", StringComparison.Ordinal))
            .ToList();

        if (jsonLines.Count == 0)
        {
            return ReplayParseResult.Fail("未识别到 JSONL 事件。请加载 logs/raw/*.jsonl。");
        }

        return ParseJsonLines(jsonLines);
    }

    private static ReplayParseResult ParseJsonLines(List<string> lines)
    {
        var frames = new SortedDictionary<int, ReplayTrickFrame>();
        var warnings = new List<string>();
        string? gameId = null;
        string? roundId = null;
        string? trumpSuit = null;

        foreach (var line in lines)
        {
            JsonDocument? doc = null;
            try
            {
                doc = JsonDocument.Parse(line);
                var root = doc.RootElement;
                var eventName = ReadString(root, "event");
                if (string.IsNullOrWhiteSpace(eventName))
                    continue;

                gameId ??= ReadString(root, "game_id");
                roundId ??= ReadString(root, "round_id");

                if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                    continue;

                if (eventName == "trump.finalized")
                {
                    trumpSuit = ReadString(payload, "trump_suit") ?? trumpSuit;
                    continue;
                }

                if (eventName == "turn.start" && ReadBool(payload, "is_lead"))
                {
                    var trickNo = ReadInt(payload, "trick_no");
                    if (trickNo <= 0)
                        continue;

                    var frame = EnsureFrame(frames, trickNo);
                    frame.LevelRank = ReadString(payload, "level_rank") ?? frame.LevelRank;
                    frame.TrumpSuit = ReadString(payload, "trump_suit") ?? trumpSuit ?? frame.TrumpSuit;
                    frame.DealerIndex = ReadInt(payload, "dealer_index", frame.DealerIndex);
                    frame.LeadPlayer = ReadInt(payload, "lead_player", frame.LeadPlayer);
                    frame.DefenderScoreBefore = ReadInt(payload, "defender_score", frame.DefenderScoreBefore);
                    frame.HandsBefore = ParseHands(payload);
                    continue;
                }

                if (eventName == "trick.finish")
                {
                    var trickNo = ReadInt(payload, "trick_no");
                    if (trickNo <= 0)
                    {
                        var trickId = ReadString(root, "trick_id");
                        trickNo = ParseTrickNoFromId(trickId);
                    }

                    if (trickNo <= 0)
                    {
                        warnings.Add("发现 trick.finish，但缺少 trick_no。");
                        continue;
                    }

                    var frame = EnsureFrame(frames, trickNo);
                    frame.TrumpSuit ??= trumpSuit;
                    frame.WinnerIndex = ReadInt(payload, "winner_index", frame.WinnerIndex);
                    frame.TrickScore = ReadInt(payload, "trick_score", frame.TrickScore);
                    frame.DefenderScoreBefore = ReadInt(payload, "defender_score_before", frame.DefenderScoreBefore);
                    frame.DefenderScoreAfter = ReadInt(payload, "defender_score_after", frame.DefenderScoreAfter);
                    frame.WinnerReason = ParseWinnerReason(payload) ?? frame.WinnerReason;
                    frame.Plays = ParsePlays(payload);
                    frame.PlayAnalysis = ParsePlayAnalysis(payload);
                }
            }
            catch
            {
                // ignore malformed line
            }
            finally
            {
                doc?.Dispose();
            }
        }

        var tricks = frames.Values.OrderBy(t => t.TrickNo).ToList();
        if (tricks.Count == 0)
        {
            return ReplayParseResult.Fail("日志中未解析到可回放的墩信息。");
        }

        return ReplayParseResult.Ok(gameId, roundId, tricks, warnings);
    }

    private static ReplayTrickFrame EnsureFrame(IDictionary<int, ReplayTrickFrame> map, int trickNo)
    {
        if (!map.TryGetValue(trickNo, out var frame))
        {
            frame = new ReplayTrickFrame { TrickNo = trickNo };
            map[trickNo] = frame;
        }

        return frame;
    }

    private static List<ReplayPlayerHand> ParseHands(JsonElement payload)
    {
        var result = new List<ReplayPlayerHand>();
        if (!payload.TryGetProperty("hands_before_trick", out var handsElement) || handsElement.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var hand in handsElement.EnumerateArray())
        {
            var playerIndex = ReadInt(hand, "player_index");
            var cards = ReadCardTexts(hand, "cards");
            result.Add(new ReplayPlayerHand
            {
                PlayerIndex = playerIndex,
                HandCount = ReadInt(hand, "hand_count", cards.Count),
                Cards = cards
            });
        }

        return result.OrderBy(h => h.PlayerIndex).ToList();
    }

    private static List<ReplayPlayerPlay> ParsePlays(JsonElement payload)
    {
        var result = new List<ReplayPlayerPlay>();
        if (!payload.TryGetProperty("trick_cards", out var trickCards) || trickCards.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var play in trickCards.EnumerateArray())
        {
            result.Add(new ReplayPlayerPlay
            {
                PlayerIndex = ReadInt(play, "player_index"),
                Cards = ReadCardTexts(play, "cards")
            });
        }

        return result.OrderBy(p => p.PlayerIndex).ToList();
    }

    private static Dictionary<int, ReplayPlayAnalysis> ParsePlayAnalysis(JsonElement payload)
    {
        var result = new Dictionary<int, ReplayPlayAnalysis>();
        if (!payload.TryGetProperty("play_analysis", out var analysisArray) || analysisArray.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in analysisArray.EnumerateArray())
        {
            var idx = ReadInt(item, "player_index");
            if (idx < 0)
                continue;

            result[idx] = new ReplayPlayAnalysis
            {
                Pattern = ReadString(item, "pattern") ?? string.Empty,
                Category = ReadString(item, "category") ?? string.Empty,
                CardScore = ReadInt(item, "cards_score")
            };
        }

        return result;
    }

    private static string? ParseWinnerReason(JsonElement payload)
    {
        if (!payload.TryGetProperty("winner_basis", out var winnerBasis) || winnerBasis.ValueKind != JsonValueKind.Object)
            return null;

        return ReadString(winnerBasis, "reason");
    }

    private static List<string> ReadCardTexts(JsonElement parent, string propName)
    {
        var cards = new List<string>();
        if (!parent.TryGetProperty(propName, out var cardsElement) || cardsElement.ValueKind != JsonValueKind.Array)
            return cards;

        foreach (var card in cardsElement.EnumerateArray())
        {
            var text = ReadString(card, "text");
            if (!string.IsNullOrWhiteSpace(text))
                cards.Add(text);
        }

        return cards;
    }

    private static string? ReadString(JsonElement element, string propName)
    {
        if (!element.TryGetProperty(propName, out var v))
            return null;

        if (v.ValueKind == JsonValueKind.String)
            return v.GetString();

        return v.ToString();
    }

    private static int ReadInt(JsonElement element, string propName, int fallback = 0)
    {
        if (!element.TryGetProperty(propName, out var v))
            return fallback;

        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt32(out var n))
            return n;

        if (int.TryParse(v.ToString(), out var parsed))
            return parsed;

        return fallback;
    }

    private static bool ReadBool(JsonElement element, string propName)
    {
        if (!element.TryGetProperty(propName, out var v))
            return false;

        if (v.ValueKind == JsonValueKind.True) return true;
        if (v.ValueKind == JsonValueKind.False) return false;
        return bool.TryParse(v.ToString(), out var parsed) && parsed;
    }

    private static int ParseTrickNoFromId(string? trickId)
    {
        if (string.IsNullOrWhiteSpace(trickId))
            return 0;

        var parts = trickId.Split('_');
        if (parts.Length < 2)
            return 0;

        return int.TryParse(parts[^1], out var no) ? no : 0;
    }
}

public sealed class ReplayParseResult
{
    public bool Success { get; private set; }
    public string Message { get; private set; } = string.Empty;
    public string? GameId { get; private set; }
    public string? RoundId { get; private set; }
    public List<ReplayTrickFrame> Tricks { get; private set; } = new();
    public List<string> Warnings { get; private set; } = new();

    public static ReplayParseResult Ok(string? gameId, string? roundId, List<ReplayTrickFrame> tricks, List<string> warnings)
    {
        return new ReplayParseResult
        {
            Success = true,
            Message = $"已解析 {tricks.Count} 墩。",
            GameId = gameId,
            RoundId = roundId,
            Tricks = tricks,
            Warnings = warnings
        };
    }

    public static ReplayParseResult Fail(string message)
    {
        return new ReplayParseResult
        {
            Success = false,
            Message = message
        };
    }
}

public sealed class ReplayTrickFrame
{
    public int TrickNo { get; set; }
    public string? LevelRank { get; set; }
    public string? TrumpSuit { get; set; }
    public int DealerIndex { get; set; } = -1;
    public int LeadPlayer { get; set; } = -1;
    public int DefenderScoreBefore { get; set; }
    public int DefenderScoreAfter { get; set; }
    public int WinnerIndex { get; set; } = -1;
    public int TrickScore { get; set; }
    public string? WinnerReason { get; set; }
    public List<ReplayPlayerHand> HandsBefore { get; set; } = new();
    public List<ReplayPlayerPlay> Plays { get; set; } = new();
    public Dictionary<int, ReplayPlayAnalysis> PlayAnalysis { get; set; } = new();
}

public sealed class ReplayPlayerHand
{
    public int PlayerIndex { get; set; }
    public int HandCount { get; set; }
    public List<string> Cards { get; set; } = new();
}

public sealed class ReplayPlayerPlay
{
    public int PlayerIndex { get; set; }
    public List<string> Cards { get; set; } = new();
}

public sealed class ReplayPlayAnalysis
{
    public string Pattern { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int CardScore { get; set; }
}
