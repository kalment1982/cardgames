using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace WebUI.Application;

public sealed class GameSessionService
{
    public (Game Game, int Seed) StartNewGame(int? forcedSeed, int dealerIndex = 0, Rank levelRank = Rank.Two)
    {
        int seed = forcedSeed ?? DateTime.Now.Millisecond;
        var game = new Game(seed);
        game.State.DealerIndex = ((dealerIndex % 4) + 4) % 4;
        game.State.LevelRank = levelRank;
        game.StartGame();
        return (game, seed);
    }

    public void UpdateViewModel(Game game, GamePageViewModel vm)
    {
        vm.PlayerHand = new List<Card>(game.State.PlayerHands[0]);
        SortHand(vm.PlayerHand, BuildCurrentConfig(game));
        vm.DefenderScore = game.State.DefenderScore;

        if (game.State.TrumpSuit.HasValue)
        {
            vm.TrumpSuit = GetSuitSymbol(game.State.TrumpSuit.Value);
            vm.OpponentTrumpSuit = vm.TrumpSuit;
        }
        else if (game.CurrentBidSuit.HasValue)
        {
            // 发牌/竞叫阶段：尚未定主时，展示当前亮主/反主花色。
            var biddingSuit = GetSuitSymbol(game.CurrentBidSuit.Value);
            vm.TrumpSuit = biddingSuit;
            vm.OpponentTrumpSuit = biddingSuit;
        }
        else
        {
            vm.TrumpSuit = "NT";
            vm.OpponentTrumpSuit = "NT";
        }

        vm.ShowBiddingPanel = game.State.Phase == GamePhase.Bidding;
        vm.ShowBuryButton = game.State.Phase == GamePhase.Burying && game.State.DealerIndex == 0;
        vm.ShowViewBottomButton = game.State.DealerIndex == 0
            && (game.State.Phase == GamePhase.Burying
                || game.State.Phase == GamePhase.Playing
                || game.State.Phase == GamePhase.Finished);
    }

    public GameConfig BuildCurrentConfig(Game game)
    {
        return new GameConfig
        {
            LevelRank = game.State.LevelRank,
            TrumpSuit = game.State.TrumpSuit
        };
    }

    public AIRole GetRoleForPlayer(Game game, int playerIndex)
    {
        if (playerIndex == game.State.DealerIndex)
            return AIRole.Dealer;

        return playerIndex % 2 == game.State.DealerIndex % 2 ? AIRole.DealerPartner : AIRole.Opponent;
    }

    public List<Card> GetCurrentWinningCards(Game game)
    {
        if (game.CurrentTrick.Count == 0)
            return new List<Card>();

        var judge = new TrickJudge(BuildCurrentConfig(game));
        int winner = judge.DetermineWinner(game.CurrentTrick);
        var winnerPlay = game.CurrentTrick.FirstOrDefault(p => p.PlayerIndex == winner);
        return winnerPlay != null ? new List<Card>(winnerPlay.Cards) : new List<Card>(game.CurrentTrick[0].Cards);
    }

    public bool IsPartnerWinning(Game game, int playerIndex)
    {
        if (game.CurrentTrick.Count == 0)
            return false;

        var judge = new TrickJudge(BuildCurrentConfig(game));
        int winner = judge.DetermineWinner(game.CurrentTrick);
        return winner != playerIndex && winner % 2 == playerIndex % 2;
    }

    public static string GetSuitSymbol(Suit suit)
    {
        return suit switch
        {
            Suit.Spade => "♠",
            Suit.Heart => "♥",
            Suit.Club => "♣",
            Suit.Diamond => "♦",
            _ => ""
        };
    }

    private static void SortHand(List<Card> playerHand, GameConfig config)
    {
        if (playerHand.Count == 0)
            return;

        var comparer = new CardComparer(config);
        var trumpCards = SortTrumpCards(playerHand.Where(c => config.IsTrump(c)).ToList(), config, comparer);
        var nonTrumpCards = playerHand.Where(c => !config.IsTrump(c)).ToList();

        var sortedNonTrump = nonTrumpCards
            .GroupBy(c => c.Suit)
            .OrderBy(g => g.Key)
            .SelectMany(g => g.OrderByDescending(c => c, comparer))
            .ToList();

        playerHand.Clear();
        playerHand.AddRange(trumpCards);
        playerHand.AddRange(sortedNonTrump);
    }

    private static List<Card> SortTrumpCards(List<Card> trumpCards, GameConfig config, CardComparer comparer)
    {
        if (trumpCards.Count == 0)
            return trumpCards;

        var jokers = trumpCards
            .Where(card => card.IsJoker)
            .OrderByDescending(card => card, comparer)
            .ToList();

        var levelCards = trumpCards
            .Where(card => !card.IsJoker && card.Rank == config.LevelRank)
            .GroupBy(card => card.Suit)
            .OrderBy(group => GetLevelSuitBucket(group.Key, config.TrumpSuit))
            .ThenBy(group => group.Key)
            .SelectMany(group => group.OrderByDescending(card => card, comparer))
            .ToList();

        var mainSuitNonLevel = trumpCards
            .Where(card => !card.IsJoker && card.Rank != config.LevelRank &&
                           config.TrumpSuit.HasValue && card.Suit == config.TrumpSuit.Value)
            .OrderByDescending(card => card, comparer)
            .ToList();

        var sorted = new List<Card>(trumpCards.Count);
        sorted.AddRange(jokers);
        sorted.AddRange(levelCards);
        sorted.AddRange(mainSuitNonLevel);
        return sorted;
    }

    private static int GetLevelSuitBucket(Suit suit, Suit? trumpSuit)
    {
        if (trumpSuit.HasValue && suit == trumpSuit.Value)
            return 0;
        return 1;
    }
}
