using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace WebUI.Application;

public sealed class ReviewApiClient
{
    private readonly HttpClient _httpClient;

    public ReviewApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<ReviewSessionListItem>> GetSessionsAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var url = limit.HasValue && limit.Value > 0
            ? $"api/review/sessions?limit={limit.Value}"
            : "api/review/sessions";

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
            return Array.Empty<ReviewSessionListItem>();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;

        var sessionArray = root.ValueKind == JsonValueKind.Array
            ? root
            : GetProperty(root, "sessions");

        if (sessionArray.ValueKind != JsonValueKind.Array)
            return Array.Empty<ReviewSessionListItem>();

        var list = new List<ReviewSessionListItem>();
        foreach (var item in sessionArray.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var mapped = ParseSessionListItem(item);
            if (string.IsNullOrWhiteSpace(mapped.SessionId))
            {
                mapped.SessionId = BuildFallbackSessionId(mapped);
            }

            if (!string.IsNullOrWhiteSpace(mapped.SessionId))
                list.Add(mapped);
        }

        return list
            .OrderByDescending(session => session.StartedAtUtc ?? DateTime.MinValue)
            .ThenBy(session => session.SessionId, StringComparer.Ordinal)
            .ToList();
    }

    public async Task<ReviewSessionDetail?> GetSessionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            return null;

        using var response = await _httpClient.GetAsync($"api/review/session/{Uri.EscapeDataString(sessionId)}", cancellationToken);
        if (!response.IsSuccessStatusCode)
            return null;

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return null;

        var detailRoot = GetProperty(root, "session");
        if (detailRoot.ValueKind != JsonValueKind.Object)
            detailRoot = root;

        var summaryRoot = GetProperty(detailRoot, "summary");
        if (summaryRoot.ValueKind != JsonValueKind.Object)
            summaryRoot = detailRoot;

        var detail = new ReviewSessionDetail
        {
            Summary = ParseSummary(summaryRoot),
            BottomCards = ParseStringList(GetProperty(detailRoot, "bottomCards"), GetProperty(detailRoot, "bottom_cards")),
            Tricks = ParseTricks(GetProperty(detailRoot, "tricks")),
            Warnings = ParseStringList(GetProperty(detailRoot, "warnings"))
        };

        if (string.IsNullOrWhiteSpace(detail.Summary.SessionId))
            detail.Summary.SessionId = sessionId;

        if (detail.BottomCards.Count == 0)
            detail.BottomCards = ParseStringList(GetProperty(summaryRoot, "bottomCards"), GetProperty(summaryRoot, "bottom_cards"));

        detail.Tricks = detail.Tricks
            .OrderBy(trick => trick.TrickNo)
            .ThenBy(trick => trick.TrickId, StringComparer.Ordinal)
            .ToList();

        return detail;
    }

    private static ReviewSessionListItem ParseSessionListItem(JsonElement item)
    {
        return new ReviewSessionListItem
        {
            SessionId = ReadString(item, "sessionId", "session_id") ?? string.Empty,
            RoundId = ReadString(item, "roundId", "round_id"),
            GameId = ReadString(item, "gameId", "game_id"),
            SourceTag = ReadString(item, "sourceTag", "source_tag"),
            SourceLabel = ReadString(item, "sourceLabel", "source_label"),
            StartedAtUtc = ReadDateTime(item, "startedAtUtc", "started_at_utc", "startedAt", "started_at"),
            DealerIndex = ReadInt(item, "dealerIndex", "dealer_index", fallback: -1),
            LevelRank = ReadString(item, "levelRank", "level_rank"),
            TrumpSuit = ReadString(item, "trumpSuit", "trump_suit"),
            DefenderScore = ReadInt(item, "defenderScore", "defender_score"),
            TrickCount = ReadInt(item, "trickCount", "trick_count"),
            AiLineSummary = ReadString(item, "aiLineSummary", "ai_line_summary"),
            AiLineBreakdown = ParseAiLineBreakdown(GetProperty(item, "aiLineBreakdown"), GetProperty(item, "ai_line_breakdown")),
            PlayerAiLines = ParsePlayerAiLines(GetProperty(item, "playerAiLines"), GetProperty(item, "player_ai_lines"))
        };
    }

    private static ReviewSessionSummary ParseSummary(JsonElement summary)
    {
        return new ReviewSessionSummary
        {
            SessionId = ReadString(summary, "sessionId", "session_id") ?? string.Empty,
            RoundId = ReadString(summary, "roundId", "round_id"),
            GameId = ReadString(summary, "gameId", "game_id"),
            SourceTag = ReadString(summary, "sourceTag", "source_tag"),
            SourceLabel = ReadString(summary, "sourceLabel", "source_label"),
            StartedAtUtc = ReadDateTime(summary, "startedAtUtc", "started_at_utc", "startedAt", "started_at"),
            DealerIndex = ReadInt(summary, "dealerIndex", "dealer_index", fallback: -1),
            LevelRank = ReadString(summary, "levelRank", "level_rank"),
            TrumpSuit = ReadString(summary, "trumpSuit", "trump_suit"),
            DefenderScore = ReadInt(summary, "defenderScore", "defender_score"),
            TrickCount = ReadInt(summary, "trickCount", "trick_count"),
            AiLineSummary = ReadString(summary, "aiLineSummary", "ai_line_summary"),
            AiLineBreakdown = ParseAiLineBreakdown(GetProperty(summary, "aiLineBreakdown"), GetProperty(summary, "ai_line_breakdown")),
            PlayerAiLines = ParsePlayerAiLines(GetProperty(summary, "playerAiLines"), GetProperty(summary, "player_ai_lines"))
        };
    }

    private static ReviewAiLineBreakdown ParseAiLineBreakdown(JsonElement primary, JsonElement secondary)
    {
        var element = primary.ValueKind == JsonValueKind.Object ? primary : secondary;
        if (element.ValueKind != JsonValueKind.Object)
            return new ReviewAiLineBreakdown();

        return new ReviewAiLineBreakdown
        {
            V30Decisions = ReadInt(element, "v30Decisions", "v30_decisions"),
            V21Decisions = ReadInt(element, "v21Decisions", "v21_decisions"),
            LegacyDecisions = ReadInt(element, "legacyDecisions", "legacy_decisions"),
            OtherDecisions = ReadInt(element, "otherDecisions", "other_decisions")
        };
    }

    private static List<ReviewPlayerAiLine> ParsePlayerAiLines(JsonElement primary, JsonElement secondary)
    {
        var element = primary.ValueKind == JsonValueKind.Array ? primary : secondary;
        if (element.ValueKind != JsonValueKind.Array)
            return new List<ReviewPlayerAiLine>();

        var lines = new List<ReviewPlayerAiLine>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var playerIndex = ReadInt(item, "playerIndex", "player_index", fallback: -1);
            var aiLine = ReadString(item, "aiLine", "ai_line");
            if (playerIndex < 0 || string.IsNullOrWhiteSpace(aiLine))
                continue;

            lines.Add(new ReviewPlayerAiLine
            {
                PlayerIndex = playerIndex,
                AiLine = aiLine!
            });
        }

        return lines
            .OrderBy(item => item.PlayerIndex)
            .ToList();
    }

    private static List<ReviewTrickFrame> ParseTricks(JsonElement tricksElement)
    {
        if (tricksElement.ValueKind != JsonValueKind.Array)
            return new List<ReviewTrickFrame>();

        var tricks = new List<ReviewTrickFrame>();
        foreach (var item in tricksElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var trick = new ReviewTrickFrame
            {
                TrickNo = ReadInt(item, "trickNo", "trick_no"),
                TrickId = ReadString(item, "trickId", "trick_id"),
                LeadPlayer = ReadInt(item, "leadPlayer", "lead_player", fallback: -1),
                WinnerIndex = ReadInt(item, "winnerIndex", "winner_index", fallback: -1),
                WinnerReason = ReadString(item, "winnerReason", "winner_reason"),
                TrickScore = ReadInt(item, "trickScore", "trick_score"),
                DefenderScoreBefore = ReadInt(item, "defenderScoreBefore", "defender_score_before"),
                DefenderScoreAfter = ReadInt(item, "defenderScoreAfter", "defender_score_after"),
                HandsBefore = ParseHands(GetProperty(item, "handsBefore"), GetProperty(item, "hands_before")),
                Plays = ParsePlays(GetProperty(item, "plays")),
                Decisions = ParseDecisions(GetProperty(item, "decisions"))
            };

            if (string.IsNullOrWhiteSpace(trick.TrickId) && trick.TrickNo > 0)
                trick.TrickId = $"trick_{trick.TrickNo:D4}";

            if (trick.Plays.Count > 0 && trick.LeadPlayer < 0)
                trick.LeadPlayer = trick.Plays.OrderBy(play => play.PlayOrder).First().PlayerIndex;

            tricks.Add(trick);
        }

        return tricks;
    }

    private static List<ReviewPlayerHandFrame> ParseHands(JsonElement primary, JsonElement secondary)
    {
        var element = primary.ValueKind == JsonValueKind.Array ? primary : secondary;
        if (element.ValueKind != JsonValueKind.Array)
            return new List<ReviewPlayerHandFrame>();

        var hands = new List<ReviewPlayerHandFrame>();
        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var cards = ParseStringList(GetProperty(item, "cards"));
            hands.Add(new ReviewPlayerHandFrame
            {
                PlayerIndex = ReadInt(item, "playerIndex", "player_index", fallback: -1),
                HandCount = ReadInt(item, "handCount", "hand_count", fallback: cards.Count),
                Cards = cards
            });
        }

        return hands.OrderBy(hand => hand.PlayerIndex).ToList();
    }

    private static List<ReviewPlayFrame> ParsePlays(JsonElement playsElement)
    {
        if (playsElement.ValueKind != JsonValueKind.Array)
            return new List<ReviewPlayFrame>();

        var plays = new List<ReviewPlayFrame>();
        int order = 1;
        foreach (var item in playsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            var parsedOrder = ReadInt(item, "playOrder", "play_order", "order", fallback: order);
            plays.Add(new ReviewPlayFrame
            {
                PlayerIndex = ReadInt(item, "playerIndex", "player_index", fallback: -1),
                PlayOrder = parsedOrder <= 0 ? order : parsedOrder,
                Cards = ParseStringList(GetProperty(item, "cards"))
            });
            order++;
        }

        return plays.OrderBy(play => play.PlayOrder).ToList();
    }

    private static List<ReviewDecisionFrame> ParseDecisions(JsonElement decisionsElement)
    {
        if (decisionsElement.ValueKind != JsonValueKind.Array)
            return new List<ReviewDecisionFrame>();

        var decisions = new List<ReviewDecisionFrame>();
        foreach (var item in decisionsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                continue;

            decisions.Add(new ReviewDecisionFrame
            {
                DecisionTraceId = ReadString(item, "decisionTraceId", "decision_trace_id"),
                PlayerIndex = ReadInt(item, "playerIndex", "player_index", fallback: -1),
                Phase = ReadString(item, "phase"),
                Path = ReadString(item, "path"),
                PhasePolicy = ReadString(item, "phasePolicy", "phase_policy"),
                AiLine = ReadString(item, "aiLine", "ai_line"),
                PrimaryIntent = ReadString(item, "primaryIntent", "primary_intent"),
                SecondaryIntent = ReadString(item, "secondaryIntent", "secondary_intent"),
                SelectedReason = ReadString(item, "selectedReason", "selected_reason"),
                SelectedCandidateId = ReadString(item, "selectedCandidateId", "selected_candidate_id"),
                TriggeredRules = ParseStringList(GetProperty(item, "triggeredRules"), GetProperty(item, "triggered_rules")),
                SelectedCards = ParseStringList(GetProperty(item, "selectedCards"), GetProperty(item, "selected_cards")),
                TurnId = ReadString(item, "turnId", "turn_id"),
                PlayPosition = ReadInt(item, "playPosition", "play_position"),
                V30Mode = ReadNestedString(item, new[] { "bundleV30", "bundle_v30" }, "mode"),
                V30PrimaryIntent = ReadNestedString(item, new[] { "bundleV30", "bundle_v30" }, "primaryIntent", "primary_intent"),
                V30SelectedReason = ReadNestedString(item, new[] { "bundleV30", "bundle_v30" }, "selectedReason", "selected_reason"),
                V30CandidateCount = ReadNestedInt(item, new[] { "bundleV30", "bundle_v30" }, "candidateCount", "candidate_count")
            });
        }

        foreach (var decision in decisions)
        {
            if (string.IsNullOrWhiteSpace(decision.AiLine))
                decision.AiLine = InferAiLine(decision);
        }

        return decisions
            .OrderBy(decision => ParseTurnOrder(decision.TurnId, decision.PlayPosition))
            .ThenBy(decision => decision.PlayerIndex)
            .ToList();
    }

    private static int ParseTurnOrder(string? turnId, int playPosition)
    {
        if (!string.IsNullOrWhiteSpace(turnId))
        {
            var digits = new string(turnId.Where(char.IsDigit).ToArray());
            if (int.TryParse(digits, out var parsed))
                return parsed;
        }

        if (playPosition > 0)
            return playPosition;

        return int.MaxValue;
    }

    private static string BuildFallbackSessionId(ReviewSessionListItem item)
    {
        var round = item.RoundId;
        if (!string.IsNullOrWhiteSpace(round))
            return round;

        var game = item.GameId;
        if (!string.IsNullOrWhiteSpace(game))
            return game;

        return string.Empty;
    }

    private static JsonElement GetProperty(JsonElement element, params string[] names)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return default;

        foreach (var prop in element.EnumerateObject())
        {
            foreach (var name in names)
            {
                if (string.Equals(prop.Name, name, StringComparison.OrdinalIgnoreCase))
                    return prop.Value;
            }
        }

        return default;
    }

    private static string? ReadString(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (prop.ValueKind == JsonValueKind.String)
            return prop.GetString();
        if (prop.ValueKind == JsonValueKind.Number || prop.ValueKind == JsonValueKind.True || prop.ValueKind == JsonValueKind.False)
            return prop.ToString();
        return null;
    }

    private static int ReadInt(JsonElement element, string name1, string? name2 = null, string? name3 = null, int fallback = 0)
    {
        var names = new List<string> { name1 };
        if (!string.IsNullOrWhiteSpace(name2))
            names.Add(name2);
        if (!string.IsNullOrWhiteSpace(name3))
            names.Add(name3);

        var prop = GetProperty(element, names.ToArray());
        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
            return number;
        if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            return parsed;
        return fallback;
    }

    private static DateTime? ReadDateTime(JsonElement element, params string[] names)
    {
        var prop = GetProperty(element, names);
        if (prop.ValueKind == JsonValueKind.String)
        {
            var text = prop.GetString();
            if (DateTime.TryParse(
                    text,
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                    out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static List<string> ParseStringList(params JsonElement[] elements)
    {
        foreach (var element in elements)
        {
            if (element.ValueKind != JsonValueKind.Array)
                continue;

            var values = new List<string>();
            foreach (var item in element.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    var text = item.GetString();
                    if (!string.IsNullOrWhiteSpace(text))
                        values.Add(text);
                    continue;
                }

                if (item.ValueKind == JsonValueKind.Object)
                {
                    var text = ReadString(item, "text", "card", "value", "name");
                    if (!string.IsNullOrWhiteSpace(text))
                        values.Add(text);
                }
            }

            return values;
        }

        return new List<string>();
    }

    private static string? ReadNestedString(JsonElement element, string[] parentNames, params string[] childNames)
    {
        foreach (var parentName in parentNames)
        {
            var parent = GetProperty(element, parentName);
            if (parent.ValueKind != JsonValueKind.Object)
                continue;

            var value = ReadString(parent, childNames);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static int ReadNestedInt(JsonElement element, string[] parentNames, params string[] childNames)
    {
        foreach (var parentName in parentNames)
        {
            var parent = GetProperty(element, parentName);
            if (parent.ValueKind != JsonValueKind.Object)
                continue;

            var prop = GetProperty(parent, childNames);
            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var number))
                return number;
            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
                return parsed;
        }

        return 0;
    }

    private static string InferAiLine(ReviewDecisionFrame decision)
    {
        if (!string.IsNullOrWhiteSpace(decision.Path))
        {
            if (decision.Path.Contains("v30", StringComparison.OrdinalIgnoreCase))
                return "V30";
            if (decision.Path.Contains("v21", StringComparison.OrdinalIgnoreCase))
                return "V21";
        }

        if (!string.IsNullOrWhiteSpace(decision.PhasePolicy))
        {
            if (decision.PhasePolicy.Contains("V30", StringComparison.OrdinalIgnoreCase))
                return "V30";
            if (decision.PhasePolicy.Contains("V21", StringComparison.OrdinalIgnoreCase))
                return "V21";
        }

        return "Unknown";
    }
}
