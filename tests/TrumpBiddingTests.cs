using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;

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

        [Fact]
        public void TryBid_PairSmallJokers_SetsNoTrumpBid()
        {
            var bidding = new TrumpBidding();
            var cards = new List<Card>
            {
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.SmallJoker)
            };

            var result = bidding.TryBidEx(0, Rank.Seven, cards);

            Assert.True(result.Success);
            Assert.Equal(Suit.Joker, bidding.TrumpSuit);
            Assert.Equal(0, bidding.TrumpPlayer);
        }

        [Fact]
        public void CanBidEx_PairSmallJokers_CanOvertakePairLevelBid()
        {
            var bidding = new TrumpBidding();
            Assert.True(bidding.TryBid(1, Rank.Seven, new List<Card>
            {
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Seven)
            }));

            var result = bidding.CanBidEx(0, Rank.Seven, new List<Card>
            {
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.SmallJoker)
            });

            Assert.True(result.Success);
        }

        [Fact]
        public void CanBidEx_DoesNotMutateState()
        {
            var bidding = new TrumpBidding();
            var result = bidding.CanBidEx(0, Rank.Two, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            Assert.True(result.Success);
            Assert.Null(bidding.TrumpSuit);
            Assert.Equal(-1, bidding.TrumpPlayer);
        }

        [Fact]
        public void CanBidEx_ReturnsPriorityTooLow_WhenExistingBidCannotBeOvertaken()
        {
            var bidding = new TrumpBidding();
            Assert.True(bidding.TryBid(0, Rank.Two, new List<Card> { new Card(Suit.Spade, Rank.Two) }));

            var result = bidding.CanBidEx(1, Rank.Two, new List<Card> { new Card(Suit.Heart, Rank.Two) });

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.BidPriorityTooLow, result.ReasonCode);
        }
    }
}
