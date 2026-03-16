using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class AITurnService
{
    private readonly RuleAIOptions _ruleAIOptions;
    private readonly GameSessionService _gameSessionService;
    private readonly AIDecisionLoggerFactory _decisionLoggerFactory;
    private readonly AIRuntimeSessionService _aiRuntimeSessionService;

    public AITurnService(
        RuleAIOptionsProvider ruleAIOptionsProvider,
        GameSessionService gameSessionService,
        AIDecisionLoggerFactory decisionLoggerFactory,
        AIRuntimeSessionService aiRuntimeSessionService)
    {
        _ruleAIOptions = ruleAIOptionsProvider.Options;
        _gameSessionService = gameSessionService;
        _decisionLoggerFactory = decisionLoggerFactory;
        _aiRuntimeSessionService = aiRuntimeSessionService;
    }

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

            // 使用训练好的Champion参数
            var championParams = ChampionLoader.LoadChampion();
            var aiPlayerObj = _aiRuntimeSessionService.GetOrCreatePlayer(
                game,
                aiPlayer,
                index => new AIPlayer(
                    buildCurrentConfig(),
                    AIDifficulty.Hard,
                    currentSeed + index + getActionCounter(),
                    championParams,
                    decisionLogger: _decisionLoggerFactory.Create(),
                    ruleAIOptions: _ruleAIOptions));

            var otherPlayers = Enumerable.Range(0, 4)
                .Where(index => index != aiPlayer)
                .ToList();
            var knownBottomCards = aiPlayer == game.State.DealerIndex
                ? new List<Card>(game.State.BuriedCards)
                : null;
            var logContext = _gameSessionService.BuildAIDecisionLogContext(game, aiPlayer);

            List<Card> aiCards = game.CurrentTrick.Count == 0
                ? aiPlayerObj.Lead(
                    aiHand,
                    role,
                    myPosition: aiPlayer,
                    opponentPositions: otherPlayers,
                    knownBottomCards: knownBottomCards,
                    logContext: logContext)
                : aiPlayerObj.Follow(
                    aiHand,
                    game.CurrentTrick[0].Cards,
                    getCurrentWinningCards(),
                    role,
                    isPartnerWinning(aiPlayer),
                    trickScore: game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score)),
                    logContext: logContext,
                    visibleBottomCards: knownBottomCards);

            bool isLastPlayer = game.CurrentTrick.Count == 3;
            bool success = await playCardsAndTraceAsync(aiPlayer, aiCards, "ai");
            if (!success)
            {
                if (LegalPlayResolver.TryResolve(game, aiPlayer, buildCurrentConfig(), out var fallbackCards))
                {
                    success = await playCardsAndTraceAsync(aiPlayer, fallbackCards, "ai");
                }

                if (!success)
                {
                    showMessage($"AI {aiPlayer} 出牌失败");
                    await pushTestEventAsync(new { type = "ai_play_failed", playerIndex = aiPlayer });
                    break;
                }
            }

            _aiRuntimeSessionService.SyncCompletedTrick(game);

            if (!isLastPlayer)
                await Task.Delay(450);
        }
    }
}
