using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;

namespace TractorGame.Tests
{
    public class TrumpBiddingTests
    {
        [Fact]
        public void TryBid_SingleLevelCard_Success()
        {
            var bidding = new TrumpBidding();
            var cards = new List<Card> { new Card(Suit.Spade, Rank.Two) };

            bool result = bidding.TryBid(0, Rank.Two, cards);

            Assert.True(result);
            Assert.Equal(Suit.Spade, bidding.TrumpSuit);
            Assert.Equal(0, bidding.TrumpPlayer);
        }

        [Fact]
        public void TryBid_PairLevelCard_Success()
        {
            var bidding = new TrumpBidding();
            var cards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two)
            };

            bool result = bidding.TryBid(1, Rank.Two, cards);

            Assert.True(result);
            Assert.Equal(Suit.Heart, bidding.TrumpSuit);
        }

        [Fact]
        public void TryBid_CounterBid_HigherLevel_Success()
        {
            var bidding = new TrumpBidding();
            bidding.TryBid(0, Rank.Two, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            var cards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two)
            };
            bool result = bidding.TryBid(1, Rank.Two, cards);

            Assert.True(result);
            Assert.Equal(Suit.Heart, bidding.TrumpSuit);
        }

        [Fact]
        public void TryBid_CounterBid_SameLevel_Fails()
        {
            var bidding = new TrumpBidding();
            bidding.TryBid(0, Rank.Two, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            var cards = new List<Card> { new Card(Suit.Heart, Rank.Two) };
            bool result = bidding.TryBid(1, Rank.Two, cards);

            Assert.False(result);
            Assert.Equal(Suit.Spade, bidding.TrumpSuit);
        }

        [Fact]
        public void TryBid_NonLevelCard_Fails()
        {
            var bidding = new TrumpBidding();
            var cards = new List<Card> { new Card(Suit.Spade, Rank.Three) };

            bool result = bidding.TryBid(0, Rank.Two, cards);

            Assert.False(result);
        }

        [Fact]
        public void SelfProtect_SetsMaxLevel()
        {
            var bidding = new TrumpBidding();
            bidding.SelfProtect(Suit.Diamond);

            Assert.Equal(Suit.Diamond, bidding.TrumpSuit);
        }
    }
}
