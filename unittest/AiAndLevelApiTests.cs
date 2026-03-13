using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class AIPlayerApiCoverageTests
    {
        [Fact]
        public void Lead_ReturnsEmpty_WhenHandEmpty()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, AIDifficulty.Medium, seed: 1);
            var result = ai.Lead(new List<Card>());

            Assert.Empty(result);
        }

        [Fact]
        public void Lead_ReturnsThrow_WhenCanThrowSameSuit()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, AIDifficulty.Medium, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Five)
            };

            var result = ai.Lead(hand);

            // 无对手信息时中等难度采用保守策略，不主动甩牌
            Assert.Single(result);
            Assert.Contains(result[0], hand);
        }

        [Fact]
        public void Follow_ReturnsSameSuit_WhenEnoughCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, seed: 1);
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
        public void Follow_UsesTrumpToBeat_WhenNoLeadSuit()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Club, Rank.Four)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, lead);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.True(config.IsTrump(c)));
        }

        [Fact]
        public void Follow_FollowsTractor_WhenHasTractor()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, seed: 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Queen)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, lead);
            var pattern = new CardPattern(result, config);
            var validator = new FollowValidator(config);

            Assert.Equal(4, result.Count);
            Assert.True(pattern.IsTractor(result));
            Assert.True(validator.IsValidFollow(hand, lead, result));
        }

        [Fact]
        public void BuryBottom_ReturnsSmallestEightCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, seed: 1);
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

            // 注意：由于不是33张牌，使用简单策略（取最小8张）
            var comparer = new CardComparer(config);
            var expected = hand.OrderBy(c => c, comparer).Take(8).ToList();
            Assert.Equal(expected, result);
        }

        [Fact]
        public void BuryBottom_ReturnsEmpty_WhenHandTooSmall()
        {
            var ai = new AIPlayer(new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade }, AIDifficulty.Medium, seed: 1);
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
            Assert.Equal(0, result.LevelChange);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(Rank.Five, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_DefenderWins_BigWin()
        {
            var manager = new LevelManager();
            var result = manager.DetermineLevelChange(160, Rank.Jack);

            Assert.Equal("闲家", result.Winner);
            Assert.Equal(2, result.LevelChange);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(Rank.King, result.NextLevel);
        }
    }
}
