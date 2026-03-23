using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace WebUI.Application;

public sealed class GameSessionService
{
    public (Game Game, int Seed) StartNewGame(
        int? forcedSeed,
        int dealerIndex = 0,
        Rank levelRank = Rank.Two,
        bool bidWinnerBecomesDealer = false)
    {
        int seed = forcedSeed ?? DateTime.Now.Millisecond;
        var game = new Game(seed, bidWinnerBecomesDealer: bidWinnerBecomesDealer);
        game.State.DealerIndex = ((dealerIndex % 4) + 4) % 4;
        game.State.LevelRank = levelRank;
        game.StartGame();
        return (game, seed);
    }

    public void UpdateViewModel(Game game, GamePageViewModel vm)
    {
        vm.PlayerHand = new List<Card>(game.State.PlayerHands[0]);
        SortHand(vm.PlayerHand, BuildDisplayConfig(game));
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
        return BuildCurrentConfigForAudit(game);
    }

    public static GameConfig BuildCurrentConfigForAudit(Game game)
    {
        return new GameConfig
        {
            LevelRank = game.State.LevelRank,
            TrumpSuit = game.State.TrumpSuit
        };
    }

    private static GameConfig BuildDisplayConfig(Game game)
    {
        return new GameConfig
        {
            LevelRank = game.State.LevelRank,
            // 发牌/竞叫阶段尚未定主时，使用当前亮主花色排序手牌，
            // 这样亮主后主牌会立即移动到最左侧显示。
            TrumpSuit = game.State.TrumpSuit ?? game.CurrentBidSuit
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

    public int GetCurrentWinningPlayer(Game game)
    {
        if (game.CurrentTrick.Count == 0)
            return -1;

        var judge = new TrickJudge(BuildCurrentConfig(game));
        return judge.DetermineWinner(game.CurrentTrick);
    }

    public bool IsPartnerWinning(Game game, int playerIndex)
    {
        if (game.CurrentTrick.Count == 0)
            return false;

        var judge = new TrickJudge(BuildCurrentConfig(game));
        int winner = judge.DetermineWinner(game.CurrentTrick);
        return winner != playerIndex && winner % 2 == playerIndex % 2;
    }

    public AIDecisionLogContext BuildAIDecisionLogContext(Game game, int playerIndex, string? actor = null)
    {
        return new AIDecisionLogContext
        {
            SessionId = game.SessionId,
            GameId = game.GameId,
            RoundId = game.RoundId,
            TrickId = game.CurrentTrickId,
            TurnId = game.CurrentTurnId,
            PlayerIndex = playerIndex,
            Actor = string.IsNullOrWhiteSpace(actor) ? $"player_{playerIndex}" : actor,
            TrickIndex = game.CurrentTrickNo,
            TurnIndex = game.CurrentTurnNo,
            PlayPosition = game.CurrentTrick.Count + 1,
            DealerIndex = game.State.DealerIndex,
            CurrentWinningPlayer = GetCurrentWinningPlayer(game),
            DefenderScore = game.State.DefenderScore,
            BottomPoints = game.BottomCardsSnapshot.Sum(card => card.Score),
            TruthSnapshot = BuildAIDebugTruthSnapshot(game)
        };
    }

    public Dictionary<string, object?> BuildAIDebugTruthSnapshot(Game game)
    {
        var config = BuildCurrentConfigForAudit(game);
        return new Dictionary<string, object?>
        {
            ["current_player"] = game.State.CurrentPlayer,
            ["defender_score"] = game.State.DefenderScore,
            ["hands_by_player"] = Enumerable.Range(0, 4)
                .Select(index => new Dictionary<string, object?>
                {
                    ["player_index"] = index,
                    ["cards"] = SerializeCardsForAudit(game.State.PlayerHands[index], config)
                })
                .ToList(),
            ["current_trick"] = game.CurrentTrick
                .Select(play => new Dictionary<string, object?>
                {
                    ["player_index"] = play.PlayerIndex,
                    ["cards"] = SerializeCardsForAudit(play.Cards, config)
                })
                .ToList(),
            ["bottom_cards"] = SerializeCardsForAudit(game.BottomCardsSnapshot, config)
        };
    }

    public static object[] SerializeHandsSnapshotForAudit(Game game)
    {
        var config = new GameConfig
        {
            LevelRank = game.State.LevelRank,
            TrumpSuit = game.State.TrumpSuit ?? game.CurrentBidSuit
        };

        return Enumerable.Range(0, 4)
            .Select(index =>
            {
                var hand = new List<Card>(game.State.PlayerHands[index]);
                SortHand(hand, config);
                return (object)new Dictionary<string, object?>
                {
                    ["player_index"] = index,
                    ["hand_count"] = hand.Count,
                    ["cards"] = SerializeCardsForAudit(hand, config)
                };
            })
            .ToArray();
    }

    public static object[] SerializePlaysForAudit(IEnumerable<TrickPlay> plays, GameConfig? config = null)
    {
        return plays.Select(play => (object)new Dictionary<string, object?>
        {
            ["player_index"] = play.PlayerIndex,
            ["cards"] = SerializeCardsForAudit(play.Cards, config)
        }).ToArray();
    }

    public static Dictionary<string, object?> BuildAuditFactSnapshot(Game game, bool includeHands = false, bool includeBottomCards = false)
    {
        var config = BuildCurrentConfigForAudit(game);
        int currentWinner = -1;
        List<Card> currentWinningCards = new();
        if (game.CurrentTrick.Count > 0)
        {
            var judge = new TrickJudge(config);
            currentWinner = judge.DetermineWinner(game.CurrentTrick);
            currentWinningCards = game.CurrentTrick
                .FirstOrDefault(play => play.PlayerIndex == currentWinner)?
                .Cards
                .ToList() ?? new List<Card>();
        }

        var payload = new Dictionary<string, object?>
        {
            ["dealer_index"] = game.State.DealerIndex,
            ["level_rank"] = game.State.LevelRank.ToString(),
            ["trump_suit"] = game.State.TrumpSuit?.ToString() ?? "NoTrump",
            ["is_no_trump"] = !game.State.TrumpSuit.HasValue,
            ["defender_score"] = game.State.DefenderScore,
            ["current_player"] = game.State.CurrentPlayer,
            ["trick_no"] = game.CurrentTrickNo,
            ["turn_no"] = game.CurrentTurnNo,
            ["trick_id"] = game.CurrentTrickId,
            ["turn_id"] = game.CurrentTurnId,
            ["current_trick"] = SerializePlaysForAudit(game.CurrentTrick, config),
            ["current_trick_score"] = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score)),
            ["current_winner"] = currentWinner,
            ["current_winning_cards"] = SerializeCardsForAudit(currentWinningCards, config)
        };

        if (includeHands)
            payload["hands_by_player"] = SerializeHandsSnapshotForAudit(game);

        if (includeBottomCards)
            payload["bottom_cards"] = SerializeCardsForAudit(game.BottomCardsSnapshot, config);

        return payload;
    }

    public static string GetSuitSymbol(Suit suit)
    {
        return suit switch
        {
            Suit.Joker => "无主",
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

    public static List<Dictionary<string, object?>> SerializeCardsForAudit(IEnumerable<Card> cards, GameConfig? config = null)
    {
        return AuditCardSerializer.SerializeCards(cards, config);
    }

    private static List<Dictionary<string, object?>> SerializeCards(IEnumerable<Card> cards)
    {
        return SerializeCardsForAudit(cards);
    }
}
