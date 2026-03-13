using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class AITurnService
{
    public async Task RunUntilHumanTurnAsync(
        Game game,
        int currentSeed,
        Func<int> getActionCounter,
        Func<GameConfig> buildCurrentConfig,
        Func<int, AIRole> getRoleForPlayer,
        Func<List<Card>> getCurrentWinningCards,
        Func<int, bool> isPartnerWinning,
        Func<int, List<Card>, string, Task<bool>> playCardsAndTraceAsync,
        Func<object, Task> pushTestEventAsync,
        Action<string> showMessage)
    {
        while (game.State.CurrentPlayer != 0 && game.State.Phase == GamePhase.Playing)
        {
            int aiPlayer = game.State.CurrentPlayer;
            var aiHand = new List<Card>(game.State.PlayerHands[aiPlayer]);
            var role = getRoleForPlayer(aiPlayer);

            var aiPlayerObj = new AIPlayer(
                buildCurrentConfig(),
                AIDifficulty.Medium,
                currentSeed + aiPlayer + getActionCounter()
            );

            List<Card> aiCards = game.CurrentTrick.Count == 0
                ? aiPlayerObj.Lead(aiHand, role)
                : aiPlayerObj.Follow(
                    aiHand,
                    game.CurrentTrick[0].Cards,
                    getCurrentWinningCards(),
                    role,
                    isPartnerWinning(aiPlayer));

            bool isLastPlayer = game.CurrentTrick.Count == 3;
            bool success = await playCardsAndTraceAsync(aiPlayer, aiCards, "ai");
            if (!success)
            {
                showMessage($"AI {aiPlayer} 出牌失败");
                await pushTestEventAsync(new { type = "ai_play_failed", playerIndex = aiPlayer });
                break;
            }

            if (!isLastPlayer)
                await Task.Delay(450);
        }
    }
}

