using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class PlayValidatorApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void IsValidPlay_ReturnsTrue_ForSingleCardInHand()
        {
            var validator = new PlayValidator(_config);
            var hand = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var play = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.True(validator.IsValidPlay(hand, play));
        }

        [Fact]
        public void IsValidPlay_ReturnsFalse_WhenCardNotInHand()
        {
            var validator = new PlayValidator(_config);
            var hand = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var play = new List<Card> { new Card(Suit.Heart, Rank.Ace) };

            Assert.False(validator.IsValidPlay(hand, play));
        }

        [Fact]
        public void ValidatePattern_RejectsMixedDifferentSuit()
        {
            var validator = new PlayValidator(_config);
            var cards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Spade, Rank.Queen)
            };

            Assert.False(validator.ValidatePattern(cards));
        }
    }

    public class FollowValidatorApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void IsValidFollow_MustFollowSuit_WhenHasSuit()
        {
            var validator = new FollowValidator(_config);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var invalid = new List<Card> { new Card(Suit.Heart, Rank.Ten) };
            var valid = new List<Card> { new Card(Suit.Spade, Rank.Queen) };

            Assert.False(validator.IsValidFollow(hand, lead, invalid));
            Assert.True(validator.IsValidFollow(hand, lead, valid));
        }

        [Fact]
        public void IsValidFollow_MustFollowPair_WhenHasPair()
        {
            var validator = new FollowValidator(_config);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Two)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten)
            };

            var invalid = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Two)
            };
            var valid = new List<Card>
            {
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight)
            };

            Assert.False(validator.IsValidFollow(hand, lead, invalid));
            Assert.True(validator.IsValidFollow(hand, lead, valid));
        }

        [Fact]
        public void IsValidFollow_AllowsAny_WhenNoLeadSuitInHand()
        {
            var validator = new FollowValidator(_config);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Diamond, Rank.King)
            };
            var lead = new List<Card> { new Card(Suit.Spade, Rank.Ten) };
            var follow = new List<Card> { new Card(Suit.Heart, Rank.Ace) };

            Assert.True(validator.IsValidFollow(hand, lead, follow));
        }
    }

    public class ThrowValidatorApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void IsThrowSuccessful_ReturnsTrue_WhenNoFollowerCanBeat()
        {
            var validator = new ThrowValidator(_config);
            var throwCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                new List<Card> { new Card(Suit.Diamond, Rank.Ace) }
            };

            Assert.True(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsFalse_WhenFollowerHasHigherPair()
        {
            var validator = new ThrowValidator(_config);
            var throwCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.King)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Nine)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void IsThrowSuccessful_ReturnsFalse_WhenFollowerCanBeatSingleSubComponent()
        {
            var validator = new ThrowValidator(_config);
            var throwCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Ten)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void GetSmallestCard_ReturnsNull_WhenEmpty()
        {
            var validator = new ThrowValidator(_config);
            List<Card> none = null!;

            Assert.Null(validator.GetSmallestCard(none));
            Assert.Null(validator.GetSmallestCard(new List<Card>()));
        }
    }

    public class TrickJudgeApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void DetermineWinner_TrumpBeatsSuit()
        {
            var judge = new TrickJudge(_config);
            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card> { new Card(Suit.Spade, Rank.Ace) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Heart, Rank.Two) })
            };

            Assert.Equal(1, judge.DetermineWinner(plays));
        }

        [Fact]
        public void TrickPlay_Constructor_SetsProperties()
        {
            var cards = new List<Card> { new Card(Suit.Spade, Rank.Ace) };
            var play = new TrickPlay(2, cards);

            Assert.Equal(2, play.PlayerIndex);
            Assert.Equal(cards, play.Cards);
        }
    }
}
