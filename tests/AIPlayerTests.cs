using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.AI;

namespace TractorGame.Tests
{
    public class AIPlayerTests
    {
        [Fact]
        public void Lead_ReturnsOneCard()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.Five)
            };

            var result = ai.Lead(hand);

            Assert.Single(result);
            Assert.Contains(result[0], hand);
        }

        [Fact]
        public void Follow_ReturnsSameSuit()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.Five)
            };
            var leadCards = new List<Card> { new Card(Suit.Heart, Rank.Six) };

            var result = ai.Follow(hand, leadCards);

            Assert.Single(result);
            Assert.Equal(Suit.Heart, result[0].Suit);
        }

        [Fact]
        public void BuryBottom_Returns8Cards()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, 1);
            var hand = new List<Card>();
            for (int i = 0; i < 33; i++)
            {
                hand.Add(new Card(Suit.Heart, Rank.Three));
            }

            var result = ai.BuryBottom(hand);

            Assert.Equal(8, result.Count);
        }
    }
}
