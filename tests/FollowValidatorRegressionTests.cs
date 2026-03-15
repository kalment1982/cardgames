using System.Collections.Generic;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class FollowValidatorRegressionTests
    {
        [Fact]
        public void IsValidFollow_MixedLead_RequiresPairWhenHandCanFormPair()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten)
            };

            var result = validator.IsValidFollowEx(hand, lead, invalidFollow);

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.FollowPairRequired, result.ReasonCode);
        }

        [Fact]
        public void IsValidFollow_TractorLead_RequiresPairFragmentsWhenNoTractorAvailable()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Nine)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Nine)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowPairRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }

        [Fact]
        public void IsValidFollow_MixedLead_RequiresTractorBeforeAdditionalPairs()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Seven)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Three)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Three)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Nine)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowTractorRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }
    }
}
