using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace PpoEngineHost;

public static class StateSnapshotBuilder
{
    /// <summary>
    /// Build a state snapshot visible to the given PPO player seat.
    /// Returns an anonymous object ready for JSON serialization.
    /// </summary>
    public static object Build(Game game, int mySeat)
    {
        var state = game.State;
        var config = new GameConfig
        {
            LevelRank = state.LevelRank,
            TrumpSuit = state.TrumpSuit
        };

        var phase = NormalizePhase(state.Phase);
        var dealer = state.DealerIndex;
        var isNoTrump = !state.TrumpSuit.HasValue;
        var trumpSuitStr = state.TrumpSuit?.ToString() ?? "NoTrump";

        // my_role
        var myRole = ResolveRole(mySeat, dealer);

        // my_hand — only the PPO player's own hand
        var myHand = state.PlayerHands[mySeat]
            .Select(SerializeCard)
            .ToArray();

        // current_trick
        var currentTrick = game.CurrentTrick;
        var currentTrickPlays = currentTrick
            .Select(p => new
            {
                player_index = p.PlayerIndex,
                cards = p.Cards.Select(SerializeCard).ToArray()
            })
            .ToArray();

        // lead_cards
        object[] leadCards = currentTrick.Count > 0
            ? currentTrick[0].Cards.Select(SerializeCard).ToArray()
            : Array.Empty<object>();

        // current winning player & cards
        int currentWinningPlayer = -1;
        object[] currentWinningCards = Array.Empty<object>();
        int currentTrickScore = 0;

        if (currentTrick.Count > 0)
        {
            var judge = new TrickJudge(config);
            currentWinningPlayer = judge.DetermineWinner(currentTrick);
            var winnerPlay = currentTrick.LastOrDefault(p => p.PlayerIndex == currentWinningPlayer);
            if (winnerPlay != null)
                currentWinningCards = winnerPlay.Cards.Select(SerializeCard).ToArray();
            currentTrickScore = currentTrick.Sum(p => p.Cards.Sum(c => c.Score));
        }

        // play_position: index within current trick (0=lead, 1=second, etc.)
        var playPosition = currentTrick.Count;

        // cards_left_by_player
        var cardsLeftByPlayer = new int[4];
        for (int i = 0; i < 4; i++)
            cardsLeftByPlayer[i] = state.PlayerHands[i].Count;

        // trick_index (0-based): CurrentTrickNo is 1-based during play
        var trickIndex = game.CurrentTrickNo > 0 ? game.CurrentTrickNo - 1 : 0;

        // played_trick_count: completed tricks = trickIndex (since trickIndex is 0-based current)
        // If trick just completed and new trick started, CurrentTrickNo already advanced.
        // LastCompletedTrickNo tracks the last finished trick number.
        var playedTrickCount = game.LastCompletedTrickNo;

        var terminal = state.Phase == GamePhase.Finished;

        return new
        {
            phase,
            dealer,
            current_player = state.CurrentPlayer,
            trump_suit = trumpSuitStr,
            is_no_trump = isNoTrump,
            level_rank = state.LevelRank.ToString(),
            my_seat = mySeat,
            my_role = myRole,
            my_hand = myHand,
            current_trick = currentTrickPlays,
            lead_cards = leadCards,
            current_winning_cards = currentWinningCards,
            current_winning_player = currentWinningPlayer,
            current_trick_score = currentTrickScore,
            defender_score = state.DefenderScore,
            trick_index = trickIndex,
            play_position = playPosition,
            cards_left_by_player = cardsLeftByPlayer,
            played_trick_count = playedTrickCount,
            terminal
        };
    }

    private static object SerializeCard(Card card)
    {
        var suit = card.IsJoker ? "Joker" : card.Suit.ToString();
        return new
        {
            suit,
            rank = card.Rank.ToString(),
            score = card.Score,
            text = card.ToString()
        };
    }

    private static string ResolveRole(int mySeat, int dealerIndex)
    {
        if (mySeat == dealerIndex) return "dealer";
        // seats 0&2 are partners, 1&3 are partners
        if (mySeat % 2 == dealerIndex % 2) return "dealer_partner";
        return "defender";
    }

    private static string NormalizePhase(GamePhase phase)
    {
        return phase switch
        {
            GamePhase.Dealing => "Dealing",
            GamePhase.Bidding => "CallTrump",
            GamePhase.Burying => "BuryBottom",
            GamePhase.Playing => "PlayTricks",
            GamePhase.Finished => "Finished",
            _ => phase.ToString()
        };
    }
}
