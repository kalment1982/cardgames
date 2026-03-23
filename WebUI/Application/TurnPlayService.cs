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
        bool isLead = trickSnapshot.Count == 0;
        int leadPlayer = isLead ? playerIndex : trickSnapshot[0].PlayerIndex;
        string trickId = $"trick_{trickIndex:D4}";
        string turnId = $"turn_{game.CurrentTurnNo:D4}";
        var auditConfig = GameSessionService.BuildCurrentConfigForAudit(game);
        object[] handsBeforeTrick = isLead
            ? GameSessionService.SerializeHandsSnapshotForAudit(game)
            : Array.Empty<object>();
        var playerHandBeforePlay = GameSessionService.SerializeCardsForAudit(game.State.PlayerHands[playerIndex], auditConfig).Cast<object>().ToArray();
        var trickCardsBeforePlay = GameSessionService.SerializePlaysForAudit(trickSnapshot, auditConfig);
        int currentTrickScoreBefore = trickSnapshot.Sum(play => play.Cards.Sum(card => card.Score));
        int currentWinnerBefore = trickSnapshot.Count > 0
            ? new TrickJudge(auditConfig).DetermineWinner(trickSnapshot)
            : -1;
        object[] currentWinningCardsBefore = trickSnapshot.Count > 0
            ? GameSessionService.SerializeCardsForAudit(
                    trickSnapshot.FirstOrDefault(play => play.PlayerIndex == currentWinnerBefore)?.Cards ?? Enumerable.Empty<Card>(),
                    auditConfig)
                .Cast<object>()
                .ToArray()
            : Array.Empty<object>();

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

            await refreshUiAsync();
            await pushEventAsync(new
            {
                type = "play_rejected",
                actionId,
                actor,
                playerIndex,
                trickIndex,
                trickNo = trickIndex,
                trickId,
                turnId,
                trickPosition,
                isLead,
                leadPlayer,
                cards = serializeCards(cards),
                handsBeforeTrick,
                playerHandBeforePlay,
                trickCardsBeforePlay,
                currentTrickScoreBefore,
                currentWinnerBefore,
                currentWinningCardsBefore,
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
            trickNo = trickIndex,
            trickId,
            turnId,
            trickPosition,
            isLead,
            leadPlayer,
            cards = serializeCards(cards),
            leadCards = trickSnapshot.Count > 0 ? serializeCards(trickSnapshot[0].Cards) : Array.Empty<object>(),
            handsBeforeTrick,
            playerHandBeforePlay,
            playerHandAfterPlay = GameSessionService.SerializeCardsForAudit(game.State.PlayerHands[playerIndex], auditConfig).Cast<object>().ToArray(),
            trickCardsBeforePlay,
            trickCardsAfterPlay = GameSessionService.SerializePlaysForAudit(game.CurrentTrick, auditConfig),
            currentTrickScoreBefore,
            currentWinnerBefore,
            currentWinningCardsBefore,
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
                trickNo = currentTrickNo,
                trickId = $"trick_{currentTrickNo:D4}",
                winner,
                winnerIndex = winner,
                trickScore,
                trick_score = trickScore,
                defenderScoreBefore,
                defenderScoreAfter,
                defenderScoreDelta = defenderScoreAfter - defenderScoreBefore,
                leadPlayer,
                plays = serializePlays(completedTrick),
                trickCards = serializePlays(completedTrick),
                handsBeforeTrick,
                handsAfterTrick = GameSessionService.SerializeHandsSnapshotForAudit(game)
            });
        }

        if (game.State.Phase == GamePhase.Finished)
        {
            await pushEventAsync(new
            {
                type = "game_finished",
                defenderScore = game.State.DefenderScore,
                trickCount = getTrickNumber(),
                trickNo = getTrickNumber()
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
