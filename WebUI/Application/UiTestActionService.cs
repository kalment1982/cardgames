using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

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
        var hand = new List<Card>(vm.PlayerHand);
        var ai = new AIPlayer(config, AIDifficulty.Hard, currentSeed + actionCounter + 5000);
        int currentPlayer = game.State.CurrentPlayer;
        var otherPlayers = Enumerable.Range(0, 4)
            .Where(index => index != currentPlayer)
            .ToList();
        var knownBottomCards = currentPlayer == game.State.DealerIndex
            ? new List<Card>(game.State.BuriedCards)
            : null;

        List<Card> selected = game.CurrentTrick.Count == 0
            ? ai.Lead(
                new List<Card>(hand),
                role,
                myPosition: currentPlayer,
                opponentPositions: otherPlayers,
                knownBottomCards: knownBottomCards)
            : ai.Follow(
                new List<Card>(hand),
                game.CurrentTrick[0].Cards,
                currentWinningCards,
                role,
                partnerWinning);

        // 兜底：如果AI选牌不合法，自动生成保底合法选牌，避免测试死循环
        if (!IsSelectionValid(game, hand, selected))
        {
            selected = BuildFallbackSelection(game, hand, config);
        }

        return MapCardsToIndices(vm.PlayerHand, selected);
    }

    public List<int> BuildAutoBurySelection(
        GamePageViewModel vm,
        int currentSeed,
        GameConfig config)
    {
        var ai = new AIPlayer(config, AIDifficulty.Hard, currentSeed + 8000);
        var selected = ai.BuryBottom(new List<Card>(vm.PlayerHand));
        return MapCardsToIndices(vm.PlayerHand, selected);
    }

    public bool ForceFinalizeBid(Game game, Suit fallbackSuit = Suit.Spade)
    {
        if (game.State.Phase != GamePhase.Bidding)
            return false;

        while (!game.IsDealingComplete)
        {
            var dealResult = game.DealNextCardEx();
            if (!dealResult.Success)
                return false;
        }

        var finalizeResult = game.FinalizeTrumpEx(fallbackSuit);
        return finalizeResult.Success;
    }

    private static bool IsSelectionValid(Game game, List<Card> hand, List<Card> selected)
    {
        if (selected.Count == 0)
            return false;

        if (game.CurrentTrick.Count == 0)
        {
            var validator = new PlayValidator(game.State.TrumpSuit.HasValue
                ? new GameConfig { LevelRank = game.State.LevelRank, TrumpSuit = game.State.TrumpSuit }
                : new GameConfig { LevelRank = game.State.LevelRank });
            return validator.IsValidPlay(hand, selected);
        }

        var followValidator = new FollowValidator(game.State.TrumpSuit.HasValue
            ? new GameConfig { LevelRank = game.State.LevelRank, TrumpSuit = game.State.TrumpSuit }
            : new GameConfig { LevelRank = game.State.LevelRank });
        return followValidator.IsValidFollow(hand, game.CurrentTrick[0].Cards, selected);
    }

    private static List<Card> BuildFallbackSelection(Game game, List<Card> hand, GameConfig config)
    {
        if (hand.Count == 0)
            return new List<Card>();

        if (game.CurrentTrick.Count == 0)
            return new List<Card> { hand[0] };

        var lead = game.CurrentTrick[0].Cards;
        int need = lead.Count;
        var followValidator = new FollowValidator(config);
        var leadCategory = config.GetCardCategory(lead[0]);
        var leadSuit = lead[0].Suit;

        var sameCategory = hand
            .Where(c => config.GetCardCategory(c) == leadCategory && (leadCategory == CardCategory.Trump || c.Suit == leadSuit))
            .ToList();

        var comparer = new CardComparer(config);
        var candidates = new List<List<Card>>();

        if (sameCategory.Count >= need)
        {
            candidates.Add(sameCategory.OrderByDescending(c => c, comparer).Take(need).ToList());
            candidates.Add(sameCategory.OrderBy(c => c, comparer).Take(need).ToList());
        }
        else
        {
            var mustFollow = sameCategory.OrderByDescending(c => c, comparer).ToList();
            var remaining = RemoveCards(hand, mustFollow);
            var filler = remaining.OrderBy(c => c, comparer).Take(need - mustFollow.Count).ToList();
            candidates.Add(mustFollow.Concat(filler).ToList());
        }

        foreach (var candidate in candidates.Where(c => c.Count == need))
        {
            if (followValidator.IsValidFollow(hand, lead, candidate))
                return candidate;
        }

        // 最后兜底：尽量满足数量
        return hand.Take(System.Math.Min(need, hand.Count)).ToList();
    }

    private static List<Card> RemoveCards(List<Card> source, List<Card> toRemove)
    {
        var result = new List<Card>(source);
        foreach (var card in toRemove)
        {
            var idx = result.FindIndex(c => c.Equals(card));
            if (idx >= 0)
                result.RemoveAt(idx);
        }
        return result;
    }

    private static List<int> MapCardsToIndices(List<Card> hand, List<Card> selectedCards)
    {
        var result = new List<int>();
        var used = new bool[hand.Count];

        foreach (var card in selectedCards)
        {
            int idx = -1;
            for (int i = 0; i < hand.Count; i++)
            {
                if (used[i])
                    continue;
                if (!hand[i].Equals(card))
                    continue;

                idx = i;
                break;
            }

            if (idx >= 0)
            {
                used[idx] = true;
                result.Add(idx);
            }
        }

        return result;
    }
}
