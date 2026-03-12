using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;

namespace TractorGame.Tests
{
    public class BottomBuryingTests
    {
        [Fact]
        public void BuryCards_Valid8Cards_Success()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Nine)
            };
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Heart, Rank.Jack)
            };

            bool result = burying.BuryCards(hand, bottom);

            Assert.True(result);
            Assert.Equal(8, burying.BuriedCards.Count);
        }

        [Fact]
        public void BuryCards_Wrong_Count_Fails()
        {
            var bottom = new List<Card> { new Card(Suit.Spade, Rank.Two) };
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>();
            var cards = new List<Card> { new Card(Suit.Spade, Rank.Two) };

            bool result = burying.BuryCards(hand, cards);

            Assert.False(result);
        }

        [Fact]
        public void BuryCards_NotInHand_Fails()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Nine)
            };
            var burying = new BottomBurying(bottom);
            var hand = new List<Card>();
            var cards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Four)
            };

            bool result = burying.BuryCards(hand, cards);

            Assert.False(result);
        }
    }
}
