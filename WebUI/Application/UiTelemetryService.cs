using System;
using System.Collections.Generic;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class UiTelemetryService
{
    private readonly IGameLogger _logger = GameLoggerFactory.CreateDefault();
    private readonly LogRelayClient _relay;

    public UiTelemetryService(LogRelayClient relay)
    {
        _relay = relay;
    }

    public void LogActionClick(Game? game, string phase, string controlId, bool enabled)
    {
        LogUiEvent(game, phase, "ui.action.click", new Dictionary<string, object?>
        {
            ["control_id"] = controlId,
            ["enabled"] = enabled,
            ["phase"] = phase
        });
    }

    public void LogCardSelect(Game? game, string phase, Card card, int selectedCount, string action)
    {
        LogUiEvent(game, phase, "ui.card.select", new Dictionary<string, object?>
        {
            ["card"] = new Dictionary<string, object?>
            {
                ["suit"] = card.Suit.ToString(),
                ["rank"] = card.Rank.ToString(),
                ["score"] = card.Score,
                ["text"] = card.ToString()
            },
            ["selected_count"] = selectedCount,
            ["action"] = action
        });
    }

    public void LogToast(Game? game, string phase, string message)
    {
        LogUiEvent(game, phase, "ui.toast.show", new Dictionary<string, object?>
        {
            ["message_key"] = "runtime.toast",
            ["message_text"] = message
        });
    }

    private void LogUiEvent(Game? game, string phase, string eventName, Dictionary<string, object?> payload, string level = LogLevels.Info)
    {
        var entry = new LogEntry
        {
            Category = LogCategories.Diag,
            Level = level,
            Event = eventName,
            SessionId = game?.SessionId,
            GameId = game?.GameId,
            RoundId = game?.RoundId,
            Phase = phase,
            Actor = "ui",
            Payload = payload
        };

        if (OperatingSystem.IsBrowser())
        {
            _ = _relay.TryPostAsync(entry);
            return;
        }

        _logger.Log(entry);
    }
}
