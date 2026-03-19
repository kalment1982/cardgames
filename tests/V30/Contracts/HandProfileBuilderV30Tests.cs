using System;
using System.Collections.Generic;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Contracts
{
    public class HandProfileBuilderV30Tests
    {
        [Fact]
        public void Build_RecognizesTrumpPairsAndVoidTargets()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new HandProfileBuilderV30(config);
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
            Assert.True(profile.HighTrumpCount >= 3);
            Assert.True(profile.TrumpPairCount >= 1);
            Assert.Contains(Suit.Club, profile.PotentialVoidTargets);
            Assert.Equal(Suit.Spade, profile.StrongestSuit);
            Assert.True(profile.HasControlTrump);
        }

        [Fact]
        public void Build_NullHand_Throws()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new HandProfileBuilderV30(config);

            Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
        }
    }
}

