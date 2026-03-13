using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class UiTestActionService
{
    public List<int> BuildAutoPlaySelection(
        Game game,
        GamePageViewModel vm,
        int currentSeed,
        int actionCounter,
        GameConfig config,
        AIRole role,
        List<Card> currentWinningCards,
        bool partnerWinning)
    {
        var ai = new AIPlayer(config, AIDifficulty.Hard, currentSeed + actionCounter + 5000);

        List<Card> selected = game.CurrentTrick.Count == 0
            ? ai.Lead(new List<Card>(vm.PlayerHand), role)
            : ai.Follow(
                new List<Card>(vm.PlayerHand),
                game.CurrentTrick[0].Cards,
                currentWinningCards,
                role,
                partnerWinning);

        return selected
            .Select(card => vm.PlayerHand.IndexOf(card))
            .Where(index => index >= 0)
            .Distinct()
            .ToList();
    }

    public List<int> BuildAutoBurySelection(
        GamePageViewModel vm,
        int currentSeed,
        GameConfig config)
    {
        var ai = new AIPlayer(config, AIDifficulty.Hard, currentSeed + 8000);
        var selected = ai.BuryBottom(new List<Card>(vm.PlayerHand));
        return selected
            .Select(card => vm.PlayerHand.IndexOf(card))
            .Where(index => index >= 0)
            .Distinct()
            .ToList();
    }

    public bool ForceFinalizeBid(Game game, Suit fallbackSuit = Suit.Spade)
    {
        if (game.State.Phase != GamePhase.Bidding)
            return false;

        game.FinalizeTrump(fallbackSuit);
        return true;
    }
}

