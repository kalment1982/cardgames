using System;
using TractorGame.Core.Logging;

namespace WebUI.Application;

public sealed class AIDecisionLoggerFactory
{
    private readonly IGameLogger _logger;

    public AIDecisionLoggerFactory(LogRelayClient relay)
    {
        _logger = OperatingSystem.IsBrowser()
            ? new RelayGameLogger(relay)
            : GameLoggerFactory.CreateDefault();
    }

    public IGameLogger Create()
    {
        return _logger;
    }

    private sealed class RelayGameLogger : IGameLogger
    {
        private readonly LogRelayClient _relay;

        public RelayGameLogger(LogRelayClient relay)
        {
            _relay = relay;
        }

        public void Log(LogEntry entry)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.Event))
                return;

            _ = _relay.TryPostAsync(entry);
        }
    }
}
