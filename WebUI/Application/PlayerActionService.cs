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

        var levelCards = GetBidLevelCards(game, vm, suit);
        if (levelCards.Count == 0)
            return false;

        // 从高优先级（多张）到低优先级（单张）尝试，确保按钮状态与真实亮主判定一致。
        for (var count = levelCards.Count; count >= 1; count--)
        {
            var attempt = levelCards.Take(count).ToList();
            if (game.CanBidTrumpEx(0, attempt).Success)
                return true;
        }

        return false;
    }

    public List<Card> GetBidLevelCards(Game game, GamePageViewModel vm, Suit suit)
    {
        return vm.PlayerHand.Where(c => c.Rank == game.State.LevelRank && c.Suit == suit).ToList();
    }
}
