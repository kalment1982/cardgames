using System.Collections.Generic;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class HandProfileBuilderTests
    {
        [Fact]
        public void Build_RecognizesTrumpPairsAndVoidTargets()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new HandProfileBuilder(config);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Club, Rank.Three)
            };

            var profile = builder.Build(hand);

            Assert.Equal(4, profile.TrumpCount);
            Assert.Equal(2, profile.JokerCount + profile.LevelCardCount - 1);
            Assert.True(profile.TrumpPairCount >= 1);
            Assert.Contains(Suit.Club, profile.PotentialVoidTargets);
            Assert.Equal(Suit.Spade, profile.StrongestSuit);
        }
    }
}
