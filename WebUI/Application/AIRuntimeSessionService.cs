using System;
using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;

namespace WebUI.Application;

public sealed class AIRuntimeSessionService
{
    private string? _roundId;
    private readonly Dictionary<int, AIPlayer> _players = new();
    private int _lastSyncedTrickNo;

    public AIPlayer GetOrCreatePlayer(Game game, int playerIndex, Func<int, AIPlayer> factory)
    {
        EnsureRound(game.RoundId);
        SyncCompletedTrick(game);

        if (_players.Count == 0)
        {
            for (int index = 0; index < 4; index++)
            {
                _players[index] = factory(index);
            }
        }

        return _players[playerIndex];
    }

    public void SyncCompletedTrick(Game game)
    {
        EnsureRound(game.RoundId);

        if (game.LastCompletedTrickNo <= _lastSyncedTrickNo || game.LastCompletedTrick.Count == 0)
            return;

        foreach (var aiPlayer in _players.Values)
        {
            aiPlayer.RecordTrick(game.LastCompletedTrick);
        }

        _lastSyncedTrickNo = game.LastCompletedTrickNo;
    }

    private void EnsureRound(string roundId)
    {
        if (string.Equals(_roundId, roundId, StringComparison.Ordinal))
            return;

        _roundId = roundId;
        _players.Clear();
        _lastSyncedTrickNo = 0;
    }
}
