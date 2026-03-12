using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class AIPlayerApiCoverageTests
    {
        [Fact]
        public void Lead_ReturnsEmpty_WhenHandEmpty()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, seed: 1);
            var result = ai.Lead(new List<Card>());

            Assert.Empty(result);
        }

        [Fact]
        public void Lead_ReturnsSingleCard_FromHand()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Queen)
            };

            var result = ai.Lead(hand);

            Assert.Single(result);
            Assert.Contains(result[0], hand);
        }

        [Fact]
        public void Follow_ReturnsSameSuit_WhenEnoughCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.Two)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, lead);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.Equal(Suit.Spade, c.Suit));
        }

        [Fact]
        public void Follow_ReturnsFirstCards_WhenNotEnoughSameSuit()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Club, Rank.Queen)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, lead);

            Assert.Equal(2, result.Count);
            Assert.Equal(hand[0], result[0]);
            Assert.Equal(hand[1], result[1]);
        }

        [Fact]
        public void BuryBottom_ReturnsSmallestEightCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Six)
            };

            var result = ai.BuryBottom(hand);

            Assert.Equal(8, result.Count);
            Assert.All(result, c => Assert.Contains(c, hand));

            var comparer = new CardComparer(config);
            var expected = hand.OrderBy(c => c, comparer).Take(8).ToList();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuryBottom_ReturnsEmpty_WhenHandTooSmall()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, seed: 1);
            var hand = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            var result = ai.BuryBottom(hand);

            Assert.Empty(result);
        }
    }

    public class LevelManagerApiCoverageTests
    {
        [Fact]
        public void DetermineLevelChange_DealerWins_WhenDefenderScoreLow()
        {
            var manager = new LevelManager();
            var result = manager.DetermineLevelChange(0, Rank.Two);

            Assert.Equal("庄家", result.Winner);
            Assert.Equal(3, result.LevelChange);
            Assert.Equal("庄家", result.NextDealer);
            Assert.Equal(Rank.Five, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_DefenderWins_WhenScoreInRange()
        {
            var manager = new LevelManager();
            var result = manager.DetermineLevelChange(80, Rank.Five);

            Assert.Equal("闲家", result.Winner);
            Assert.Equal(1, result.LevelChange);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(Rank.Six, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_DefenderWins_BigWin()
        {
            var manager = new LevelManager();
            var result = manager.DetermineLevelChange(160, Rank.Jack);

            Assert.Equal("闲家", result.Winner);
            Assert.Equal(3, result.LevelChange);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(Rank.Ace, result.NextLevel);
        }
    }
}
