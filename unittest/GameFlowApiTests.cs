using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class DeckApiCoverageTests
    {
        [Fact]
        public void Deck_Constructors_Initialize108Cards()
        {
            var deck1 = new Deck();
            var deck2 = new Deck(1);

            Assert.Equal(108, deck1.RemainingCards);
            Assert.Equal(108, deck2.RemainingCards);
        }

        [Fact]
        public void Deck_Shuffle_DrawAll_HasTwoOfEachCard()
        {
            var deck = new Deck(42);
            deck.Shuffle();

            var counts = new Dictionary<(Suit Suit, Rank Rank), int>();
            for (int i = 0; i < 108; i++)
            {
                var card = deck.DrawCard();
                var key = (card.Suit, card.Rank);
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }

            Assert.Equal(0, deck.RemainingCards);
            Assert.Equal(54, counts.Count);
            foreach (var kvp in counts)
            {
                Assert.Equal(2, kvp.Value);
            }
        }

        [Fact]
        public void Deck_DrawFromEmpty_Throws()
        {
            var deck = new Deck(1);
            for (int i = 0; i < 108; i++)
            {
                deck.DrawCard();
            }

            Assert.Throws<InvalidOperationException>(() => deck.DrawCard());
        }
    }

    public class DealingPhaseApiCoverageTests
    {
        [Fact]
        public void Deal_DistributesCorrectCounts()
        {
            var dealing = new DealingPhase(new Deck(7));
            while (!dealing.IsComplete)
            {
                dealing.DealNext();
            }

            int total = 0;
            for (int i = 0; i < 4; i++)
            {
                var hand = dealing.GetPlayerHand(i);
                Assert.Equal(25, hand.Count);
                total += hand.Count;
            }

            var bottom = dealing.GetBottomCards();
            Assert.Equal(8, bottom.Count);
            total += bottom.Count;

            Assert.Equal(108, total);
        }

        [Fact]
        public void GetPlayerHand_InvalidIndex_Throws()
        {
            var dealing = new DealingPhase(new Deck(1));

            Assert.Throws<ArgumentOutOfRangeException>(() => dealing.GetPlayerHand(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => dealing.GetPlayerHand(4));
        }
    }

    public class BottomBuryingApiCoverageTests
    {
        [Fact]
        public void BuryCards_ReturnsFalse_WhenCountNotEight()
        {
            var bottom = BuildBottom();
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>();

            Assert.False(burying.BuryCards(hand, bottom.Take(7).ToList()));
        }

        [Fact]
        public void BuryCards_ReturnsFalse_WhenCardMissing()
        {
            var bottom = BuildBottom();
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>();

            var cardsToBury = new List<Card>(bottom)
            {
                new Card(Suit.Spade, Rank.Ace)
            };
            cardsToBury.RemoveAt(0);

            Assert.False(burying.BuryCards(hand, cardsToBury));
        }

        [Fact]
        public void BuryCards_ReturnsTrue_AndStoresBuriedCards()
        {
            var bottom = BuildBottom();
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>();

            Assert.True(burying.BuryCards(hand, bottom));
            Assert.Equal(8, burying.BuriedCards.Count);
            foreach (var card in bottom)
            {
                Assert.Contains(card, burying.BuriedCards);
            }
        }

        [Fact]
        public void BottomCards_ReturnsCopy()
        {
            var bottom = BuildBottom();
            var burying = new BottomBurying(bottom);

            var copy = burying.BottomCards;
            copy.Clear();

            Assert.Equal(8, burying.BottomCards.Count);
        }

        private static List<Card> BuildBottom()
        {
            return new List<Card>
            {
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Diamond, Rank.Nine)
            };
        }
    }

    public class TrumpBiddingApiCoverageTests
    {
        [Fact]
        public void TryBid_ValidatesCards()
        {
            var bidding = new TrumpBidding();

            Assert.False(bidding.TryBid(0, Rank.Five, new List<Card>()));
            Assert.False(bidding.TryBid(0, Rank.Five, new List<Card> { new Card(Suit.Spade, Rank.Four) }));
            Assert.False(bidding.TryBid(0, Rank.Five, new List<Card>
            {
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Heart, Rank.Five)
            }));
        }

        [Fact]
        public void TryBid_AllowsHigherBidLevel()
        {
            var bidding = new TrumpBidding();

            Assert.True(bidding.TryBid(1, Rank.Five, new List<Card> { new Card(Suit.Heart, Rank.Five) }));
            Assert.Equal(Suit.Heart, bidding.TrumpSuit);
            Assert.Equal(1, bidding.TrumpPlayer);

            Assert.False(bidding.TryBid(2, Rank.Five, new List<Card> { new Card(Suit.Spade, Rank.Five) }));

            Assert.True(bidding.TryBid(2, Rank.Five, new List<Card>
            {
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Five)
            }));
            Assert.Equal(Suit.Spade, bidding.TrumpSuit);
            Assert.Equal(2, bidding.TrumpPlayer);
        }

        [Fact]
        public void SelfProtect_BlocksFurtherBids()
        {
            var bidding = new TrumpBidding();
            bidding.SelfProtect(Suit.Club);

            Assert.Equal(Suit.Club, bidding.TrumpSuit);
            Assert.False(bidding.TryBid(0, Rank.Five, new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Five)
            }));
        }
    }

    public class GameStateApiCoverageTests
    {
        [Fact]
        public void Constructor_InitializesCollections()
        {
            var state = new GameState();

            Assert.Equal(4, state.PlayerHands.Length);
            for (int i = 0; i < 4; i++)
            {
                Assert.NotNull(state.PlayerHands[i]);
            }
            Assert.NotNull(state.BuriedCards);
        }
    }

    public class GameApiCoverageTests
    {
        [Fact]
        public void StartGame_SetsPhaseAndHands()
        {
            var game = new Game(7);

            game.StartGame();

            Assert.Equal(GamePhase.Bidding, game.State.Phase);
            Assert.False(game.IsDealingComplete);
            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(0, game.State.PlayerHands[i].Count);
            }
        }

        [Fact]
        public void BidTrump_ReturnsFalse_WhenNotBidding()
        {
            var game = new Game();

            var result = game.BidTrump(0, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            Assert.False(result);
        }

        [Fact]
        public void BidTrump_ReturnsTrue_WhenBiddingAndValid()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);

            var result = game.BidTrump(0, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            Assert.True(result);
        }

        [Fact]
        public void FinalizeTrump_UsesProvidedSuit()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);

            game.FinalizeTrump(Suit.Heart);

            Assert.Equal(Suit.Heart, game.State.TrumpSuit);
            Assert.Equal(GamePhase.Burying, game.State.Phase);
        }

        [Fact]
        public void FinalizeTrump_UsesDefaultWhenNoBidding()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);

            game.FinalizeTrump();

            Assert.Equal(Suit.Spade, game.State.TrumpSuit);
            Assert.Equal(GamePhase.Burying, game.State.Phase);
        }

        [Fact]
        public void BuryBottom_Succeeds_WithDealerCards()
        {
            var game = new Game(3);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Club);

            var cardsToBury = game.State.PlayerHands[0].Take(8).ToList();

            Assert.True(game.BuryBottom(cardsToBury));
            Assert.Equal(GamePhase.Playing, game.State.Phase);
            Assert.Equal(0, game.State.CurrentPlayer);
            Assert.Equal(25, game.State.PlayerHands[0].Count);
        }

        [Fact]
        public void BuryBottom_ReturnsFalse_WhenNotBurying()
        {
            var game = new Game(3);

            var cardsToBury = new List<Card>
            {
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Nine)
            };

            Assert.False(game.BuryBottom(cardsToBury));
        }

        [Fact]
        public void PlayCards_ReturnsFalse_WhenNotPlayersTurn()
        {
            var game = new Game();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 1;
            game.State.PlayerHands[0] = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var play = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.False(game.PlayCards(0, play));
        }

        [Fact]
        public void PlayCards_ReturnsFalse_WhenPhaseNotPlaying()
        {
            var game = new Game();
            game.State.Phase = GamePhase.Dealing;
            game.State.CurrentPlayer = 0;
            game.State.PlayerHands[0] = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var play = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.False(game.PlayCards(0, play));
        }

        private static void DealToEnd(Game game)
        {
            while (!game.IsDealingComplete)
            {
                var step = game.DealNextCardEx();
                Assert.True(step.Success);
            }
        }

        [Fact]
        public void PlayCards_ReturnsTrue_WhenHandEqualsPlayedCards()
        {
            var game = new Game();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;
            game.State.PlayerHands[0] = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var play = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.True(game.PlayCards(0, play));
            Assert.Empty(game.State.PlayerHands[0]);
            Assert.Equal(1, game.State.CurrentPlayer);
        }

        [Fact]
        public void PlayCards_FollowPath_ReturnsTrue_WhenCardsAlignWithCurrentBehavior()
        {
            var game = new Game();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;
            game.State.PlayerHands[0] = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            game.State.PlayerHands[1] = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var lead = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var follow = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.True(game.PlayCards(0, lead));
            Assert.True(game.PlayCards(1, follow));
            Assert.Empty(game.State.PlayerHands[1]);
            Assert.Equal(2, game.State.CurrentPlayer);
        }
    }
}
