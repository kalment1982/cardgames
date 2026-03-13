using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace WebUI.Application;

public sealed class TurnPlayService
{
    public async Task<bool> PlayAndTraceAsync(
        Game game,
        int playerIndex,
        List<Card> cards,
        string actor,
        Func<int> nextActionId,
        Func<int> getTrickNumber,
        Action<int> setTrickNumber,
        Action<List<TrickPlay>?> setDisplayTrick,
        Func<IEnumerable<Card>, object[]> serializeCards,
        Func<IEnumerable<TrickPlay>, object[]> serializePlays,
        Func<object, Task> pushEventAsync,
        Func<Task> refreshUiAsync)
    {
        int actionId = nextActionId();
        int defenderScoreBefore = game.State.DefenderScore;
        var trickSnapshot = game.CurrentTrick.Select(p => new TrickPlay(p.PlayerIndex, new List<Card>(p.Cards))).ToList();
        int trickIndex = getTrickNumber() + 1;
        int trickPosition = trickSnapshot.Count + 1;
        bool isLastPlayer = trickSnapshot.Count == 3;

        if (isLastPlayer)
        {
            setDisplayTrick(new List<TrickPlay>(trickSnapshot)
            {
                new TrickPlay(playerIndex, new List<Card>(cards))
            });
        }

        var playResult = game.PlayCardsEx(playerIndex, cards);
        bool success = playResult.Success;

        if (!success)
        {
            if (isLastPlayer)
                setDisplayTrick(null);

            await pushEventAsync(new
            {
                type = "play_rejected",
                actionId,
                actor,
                playerIndex,
                trickIndex,
                trickPosition,
                cards = serializeCards(cards),
                phase = game.State.Phase.ToString(),
                currentPlayer = game.State.CurrentPlayer,
                reasonCode = playResult.ReasonCode ?? ReasonCodes.UnknownError
            });
            return false;
        }

        await pushEventAsync(new
        {
            type = "play",
            actionId,
            actor,
            playerIndex,
            trickIndex,
            trickPosition,
            cards = serializeCards(cards),
            leadCards = trickSnapshot.Count > 0 ? serializeCards(trickSnapshot[0].Cards) : Array.Empty<object>(),
            defenderScoreBefore,
            defenderScoreAfter = game.State.DefenderScore
        });

        if (trickSnapshot.Count == 3)
        {
            int currentTrickNo = getTrickNumber() + 1;
            setTrickNumber(currentTrickNo);

            var completedTrick = trickSnapshot
                .Concat(new[] { new TrickPlay(playerIndex, new List<Card>(cards)) })
                .ToList();

            int trickScore = completedTrick.Sum(p => p.Cards.Sum(c => c.Score));
            int winner = game.State.CurrentPlayer;
            int defenderScoreAfter = game.State.DefenderScore;

            await pushEventAsync(new
            {
                type = "trick_end",
                trickIndex = currentTrickNo,
                winner,
                trickScore,
                defenderScoreBefore,
                defenderScoreAfter,
                defenderScoreDelta = defenderScoreAfter - defenderScoreBefore,
                plays = serializePlays(completedTrick)
            });
        }

        if (game.State.Phase == GamePhase.Finished)
        {
            await pushEventAsync(new
            {
                type = "game_finished",
                defenderScore = game.State.DefenderScore,
                trickCount = getTrickNumber()
            });
        }

        await refreshUiAsync();

        if (isLastPlayer)
        {
            await Task.Delay(1500);
            setDisplayTrick(null);
            await refreshUiAsync();
        }

        return true;
    }
}
