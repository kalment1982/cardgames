using Xunit;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;

namespace TractorGame.Tests
{
    public class DeckTests
    {
        [Fact]
        public void Deck_Initialize_Has108Cards()
        {
            var deck = new Deck();
            Assert.Equal(108, deck.RemainingCards);
        }

        [Fact]
        public void Deck_DrawCard_ReducesCount()
        {
            var deck = new Deck();
            deck.DrawCard();
            Assert.Equal(107, deck.RemainingCards);
        }

        [Fact]
        public void Deck_Shuffle_DifferentSeeds()
        {
            var deck1 = new Deck(1);
            var deck2 = new Deck(2);

            deck1.Shuffle();
            deck2.Shuffle();

            var cards1 = new List<Card>();
            var cards2 = new List<Card>();
            for (int i = 0; i < 10; i++)
            {
                cards1.Add(deck1.DrawCard());
                cards2.Add(deck2.DrawCard());
            }

            Assert.NotEqual(cards1, cards2);
        }
    }

    public class DealingPhaseTests
    {
        [Fact]
        public void Deal_GivesEachPlayer25Cards()
        {
            var deck = new Deck(1);
            var dealing = new DealingPhase(deck);
            dealing.Deal();

            for (int i = 0; i < 4; i++)
            {
                Assert.Equal(25, dealing.GetPlayerHand(i).Count);
            }
        }

        [Fact]
        public void Deal_Gives8BottomCards()
        {
            var deck = new Deck(1);
            var dealing = new DealingPhase(deck);
            dealing.Deal();

            Assert.Equal(8, dealing.GetBottomCards().Count);
        }

        [Fact]
        public void Deal_AllCardsDistributed()
        {
            var deck = new Deck(1);
            var dealing = new DealingPhase(deck);
            dealing.Deal();

            var allCards = new List<Card>();
            for (int i = 0; i < 4; i++)
            {
                allCards.AddRange(dealing.GetPlayerHand(i));
            }
            allCards.AddRange(dealing.GetBottomCards());

            Assert.Equal(108, allCards.Count);
            Assert.Equal(54, allCards.Distinct().Count()); // 54种牌，每种2张
        }
    }
}
