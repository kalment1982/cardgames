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
    private bool _hooksReady;

    public bool IsEnabled { get; private set; }
    public int? ForcedSeed { get; private set; }

    public UiAutomationService(IJSRuntime js)
    {
        _js = js;
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
        if (!IsEnabled || !_hooksReady)
            return;

        LogMappedTestEvent(payload, game, phase);

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

            _logger.Log(new LogEntry
            {
                Category = category,
                Level = level,
                Event = normalizedEvent,
                SessionId = game?.SessionId,
                GameId = game?.GameId,
                RoundId = game?.RoundId,
                Phase = phase,
                Actor = "ui",
                Payload = normalizedPayload
            });
        }
        catch
        {
            // Mapping failure should not impact game flow.
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
}

