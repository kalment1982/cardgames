using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;

namespace WebUI.Application;

public sealed class PlayerActionService
{
    public void ToggleSelection(GamePageViewModel vm, int index)
    {
        if (vm.SelectedCardIndices.Contains(index))
            vm.SelectedCardIndices.Remove(index);
        else
            vm.SelectedCardIndices.Add(index);
    }

    public List<Card> GetSelectedCards(GamePageViewModel vm)
    {
        return vm.SelectedCardIndices.OrderBy(i => i).Select(i => vm.PlayerHand[i]).ToList();
    }

    public bool CanBid(Game game, GamePageViewModel vm, Suit suit)
    {
        if (game.State.Phase != GamePhase.Bidding)
            return false;

        return vm.PlayerHand.Any(c => c.Rank == game.State.LevelRank && c.Suit == suit);
    }

    public List<Card> GetBidLevelCards(Game game, GamePageViewModel vm, Suit suit)
    {
        return vm.PlayerHand.Where(c => c.Rank == game.State.LevelRank && c.Suit == suit).ToList();
    }
}

