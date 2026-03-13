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
        else
        {
            vm.TrumpSuit = "NT";
            vm.OpponentTrumpSuit = "NT";
        }

        vm.ShowBiddingPanel = game.State.Phase == GamePhase.Bidding;
        vm.ShowBuryButton = game.State.Phase == GamePhase.Burying && game.State.DealerIndex == 0;
        vm.ShowViewBottomButton = game.State.Phase == GamePhase.Burying && game.State.DealerIndex == 0;
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
        var trumpCards = playerHand.Where(c => config.IsTrump(c)).OrderByDescending(c => c, comparer).ToList();
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
}
