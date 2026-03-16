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

        [Fact]
        public void IsValidFollow_MixedLead_AllowsShortSuitExhaustionWithFiller()
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
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Seven)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Club, Rank.Three)
            };
            var follow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Club, Rank.Three)
            };

            var result = validator.IsValidFollowEx(hand, lead, follow);

            Assert.True(result.Success);
        }

        [Fact]
        public void IsValidFollow_TrumpMixedLead_AllowsTrumpExhaustionWithSideFiller()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Seven)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Three)
            };
            var follow = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Three)
            };

            var result = validator.IsValidFollowEx(hand, lead, follow);

            Assert.True(result.Success);
        }

        [Fact]
        public void IsValidFollow_MixedLead_RequiresSecondTractorWhenHandCanSupplyTwoTractors()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace), new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King), new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Seven), new Card(Suit.Heart, Rank.Seven)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Seven)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Seven), new Card(Suit.Heart, Rank.Seven)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowTractorRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }

        [Fact]
        public void IsValidFollow_MixedLead_RequiresPairAfterMatchingTractor()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace), new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King), new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Seven)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Heart, Rank.Five)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Six)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowPairRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }

        [Fact]
        public void IsValidFollow_MixedLead_RequiresTractorEvenWhenAlternativeHasMorePairs()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace), new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King), new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten), new Card(Suit.Heart, Rank.Ten)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowTractorRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }

        [Fact]
        public void IsValidFollow_TrumpMixedLead_RequiresTractorBeforeExtraTrumpPairs()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace), new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King), new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Nine), new Card(Suit.Spade, Rank.Nine)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen), new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack), new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Eight), new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Six)
            };
            var invalidFollow = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Eight), new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Six)
            };
            var validFollow = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen), new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack), new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten), new Card(Suit.Spade, Rank.Ten)
            };

            var invalidResult = validator.IsValidFollowEx(hand, lead, invalidFollow);
            var validResult = validator.IsValidFollowEx(hand, lead, validFollow);

            Assert.False(invalidResult.Success);
            Assert.Equal(ReasonCodes.FollowTractorRequired, invalidResult.ReasonCode);
            Assert.True(validResult.Success);
        }

        [Fact]
        public void IsValidFollow_TractorLead_AllowsBestAvailablePairsPlusSingles_WhenThreePairTractorUnavailable()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Club
            };
            var validator = new FollowValidator(config);

            var lead = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine), new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Eight), new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Seven), new Card(Suit.Diamond, Rank.Seven)
            };
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Queen), new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Three), new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Two)
            };
            var follow = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Queen), new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Three), new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Ten)
            };

            var result = validator.IsValidFollowEx(hand, lead, follow);

            Assert.True(result.Success);
        }
    }
}
