using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.JSInterop;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;

namespace WebUI.Application;

public sealed class UiAutomationService
{
    private readonly IJSRuntime _js;
    private readonly IGameLogger _logger;
    private readonly LogRelayClient _relay;
    private bool _hooksReady;

    public bool IsEnabled { get; private set; }
    public int? ForcedSeed { get; private set; }

    public UiAutomationService(IJSRuntime js, LogRelayClient relay)
    {
        _js = js;
        _relay = relay;
        _logger = GameLoggerFactory.CreateDefault();
    }

    public void ConfigureFromUri(string uri)
    {
        var parsed = new Uri(uri);
        var query = ParseQuery(parsed.Query);

        if (query.TryGetValue("autotest", out var autoTest))
            IsEnabled = IsTruthy(autoTest);

        if (query.TryGetValue("seed", out var seedText) &&
            int.TryParse(seedText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var seed) &&
            seed > 0)
        {
            ForcedSeed = seed;
        }
    }

    public async Task InitializeTestHooksAsync(Game? game, int seed)
    {
        if (!IsEnabled)
            return;

        try
        {
            await _js.InvokeVoidAsync("tractorTest.clearEvents");
            _hooksReady = true;
            await PushEventAsync(new
            {
                type = "game_start",
                seed,
                levelRank = game?.State.LevelRank.ToString(),
                dealerIndex = game?.State.DealerIndex ?? -1
            }, game, "Dealing");
        }
        catch
        {
            _hooksReady = false;
        }
    }

    public async Task PushEventAsync(object payload, Game? game, string phase)
    {
        LogMappedTestEvent(payload, game, phase);

        if (!IsEnabled || !_hooksReady)
            return;

        try
        {
            await _js.InvokeVoidAsync("tractorTest.pushEvent", payload);
        }
        catch
        {
            // Ignore test hook exceptions to avoid breaking gameplay.
        }
    }

    private void LogMappedTestEvent(object payload, Game? game, string phase)
    {
        try
        {
            var element = JsonSerializer.SerializeToElement(payload);
            if (!element.TryGetProperty("type", out var typeElement))
                return;

            string? rawType = typeElement.GetString();
            if (string.IsNullOrWhiteSpace(rawType))
                return;

            string? normalizedEvent = rawType switch
            {
                "game_start" => "game.start",
                "play" => "play.accept",
                "play_rejected" => "play.reject",
                "trick_end" => "trick.finish",
                "game_finished" => "game.finish",
                "trump_finalized" => "trump.finalized",
                "bury_bottom" => "bury.accept",
                "bid_trump" => "trump.bid.accept",
                "ai_play_failed" => "error.validation",
                _ => null
            };

            if (normalizedEvent == null)
                return;

            string level = rawType switch
            {
                "play_rejected" => LogLevels.Warn,
                "ai_play_failed" => LogLevels.Warn,
                _ => LogLevels.Info
            };

            string category = rawType switch
            {
                "ai_play_failed" => LogCategories.Diag,
                _ => LogCategories.Audit
            };

            var normalizedPayload = JsonElementToValue(element) as Dictionary<string, object?> ?? new Dictionary<string, object?>();
            normalizedPayload["raw_type"] = rawType;
            NormalizePayload(normalizedEvent, normalizedPayload);
            AugmentPayloadFromGame(normalizedEvent, normalizedPayload, game);

            var entry = new LogEntry
            {
                Category = category,
                Level = level,
                Event = normalizedEvent,
                SessionId = game?.SessionId,
                GameId = game?.GameId,
                RoundId = game?.RoundId,
                TrickId = FirstNonEmptyString(normalizedPayload, "trick_id", "trickId") ?? game?.CurrentTrickId,
                TurnId = FirstNonEmptyString(normalizedPayload, "turn_id", "turnId") ?? game?.CurrentTurnId,
                Phase = phase,
                Actor = FirstNonEmptyString(normalizedPayload, "actor") ?? "ui",
                Payload = normalizedPayload
            };

            if (OperatingSystem.IsBrowser())
            {
                _ = _relay.TryPostAsync(entry);
                return;
            }

            _logger.Log(entry);
        }
        catch
        {
            // Mapping failure should not impact game flow.
        }
    }

    private static void NormalizePayload(string normalizedEvent, Dictionary<string, object?> payload)
    {
        CopyIfMissing(payload, "trick_no", "trickNo", "trickIndex", "trick_index");
        CopyIfMissing(payload, "turn_no", "turnNo", "turn_index");
        CopyIfMissing(payload, "trick_id", "trickId");
        CopyIfMissing(payload, "turn_id", "turnId");
        CopyIfMissing(payload, "player_index", "playerIndex");
        CopyIfMissing(payload, "dealer_index", "dealerIndex");
        CopyIfMissing(payload, "level_rank", "levelRank");
        CopyIfMissing(payload, "trump_suit", "trumpSuit");
        CopyIfMissing(payload, "defender_score_before", "defenderScoreBefore");
        CopyIfMissing(payload, "defender_score_after", "defenderScoreAfter");
        CopyIfMissing(payload, "defender_score_delta", "defenderScoreDelta");
        CopyIfMissing(payload, "lead_player", "leadPlayer");
        CopyIfMissing(payload, "play_position", "trickPosition", "playPosition");
        CopyIfMissing(payload, "is_lead", "isLead");
        CopyIfMissing(payload, "hands_before_trick", "handsBeforeTrick");
        CopyIfMissing(payload, "hands_after_trick", "handsAfterTrick");
        CopyIfMissing(payload, "player_hand_before_play", "playerHandBeforePlay");
        CopyIfMissing(payload, "player_hand_after_play", "playerHandAfterPlay");
        CopyIfMissing(payload, "trick_cards_before_play", "trickCardsBeforePlay");
        CopyIfMissing(payload, "trick_cards_after_play", "trickCardsAfterPlay");
        CopyIfMissing(payload, "trick_cards", "trickCards", "plays");
        CopyIfMissing(payload, "winner_index", "winnerIndex", "winner");
        CopyIfMissing(payload, "trick_score", "trickScore");
        CopyIfMissing(payload, "current_trick_score_before", "currentTrickScoreBefore");
        CopyIfMissing(payload, "current_winner_before", "currentWinnerBefore");
        CopyIfMissing(payload, "current_winning_cards_before", "currentWinningCardsBefore");
        CopyIfMissing(payload, "current_winner", "currentWinner");
        CopyIfMissing(payload, "current_winning_cards", "currentWinningCards");
        CopyIfMissing(payload, "current_trick_score", "currentTrickScore");
        CopyIfMissing(payload, "buried_cards", "cards");
        CopyIfMissing(payload, "bottom_cards", "bottomCards");
        CopyIfMissing(payload, "next_player", "nextPlayer");
        CopyIfMissing(payload, "current_player", "currentPlayer");

        if (string.Equals(normalizedEvent, "trick.finish", StringComparison.Ordinal))
        {
            payload["type"] = "trick_end";
            payload.TryAdd("raw_type", "trick_end");
        }
    }

    private static void AugmentPayloadFromGame(string normalizedEvent, Dictionary<string, object?> payload, Game? game)
    {
        if (game == null)
            return;

        var facts = GameSessionService.BuildAuditFactSnapshot(
            game,
            includeHands: normalizedEvent == "game.finish",
            includeBottomCards: normalizedEvent is "bury.accept" or "trump.finalized" or "game.finish");

        foreach (var pair in facts)
            payload.TryAdd(pair.Key, pair.Value);

        if (normalizedEvent == "trump.finalized")
        {
            payload.TryAdd("dealer_hand_count_before_bottom", game.State.PlayerHands[game.State.DealerIndex].Count);
            payload.TryAdd("dealer_hand_after_bottom", GameSessionService.SerializeCardsForAudit(game.State.PlayerHands[game.State.DealerIndex]));
        }

        if (normalizedEvent == "bury.accept")
        {
            int dealer = game.State.DealerIndex;
            payload.TryAdd("dealer_index", dealer);
            payload.TryAdd("dealer_hand_count_after", game.State.PlayerHands[dealer].Count);
            payload.TryAdd("dealer_hand_after", GameSessionService.SerializeCardsForAudit(game.State.PlayerHands[dealer]));
            payload["bottom_cards"] = GameSessionService.SerializeCardsForAudit(game.State.BuriedCards);
        }

        if (normalizedEvent == "game.finish")
        {
            payload.TryAdd("winner_side", game.State.DefenderScore >= 80 ? "defender" : "dealer");
            payload.TryAdd("bottom_points", game.BottomCardsSnapshot.Sum(card => card.Score));
        }
    }

    private static object? JsonElementToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => JsonElementToValue(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(JsonElementToValue)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number => element.TryGetInt64(out var i64) ? i64 : element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Null => null,
            _ => element.ToString()
        };
    }

    private static Dictionary<string, string> ParseQuery(string query)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(query))
            return dict;

        var text = query.StartsWith("?") ? query[1..] : query;
        foreach (var part in text.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var kv = part.Split('=', 2);
            var key = Uri.UnescapeDataString(kv[0]);
            var value = kv.Length > 1 ? Uri.UnescapeDataString(kv[1]) : string.Empty;
            dict[key] = value;
        }

        return dict;
    }

    private static bool IsTruthy(string value)
    {
        return value.Equals("1", StringComparison.OrdinalIgnoreCase)
            || value.Equals("true", StringComparison.OrdinalIgnoreCase)
            || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyIfMissing(Dictionary<string, object?> payload, string targetKey, params string[] sourceKeys)
    {
        if (payload.ContainsKey(targetKey))
            return;

        foreach (var sourceKey in sourceKeys)
        {
            if (payload.TryGetValue(sourceKey, out var value))
            {
                payload[targetKey] = value;
                return;
            }
        }
    }

    private static string? FirstNonEmptyString(Dictionary<string, object?> payload, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!payload.TryGetValue(key, out var value) || value == null)
                continue;

            var text = value.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                return text;
        }

        return null;
    }
}
