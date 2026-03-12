using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Tests
{
    public class ScoreCalculatorTests
    {
        private readonly GameConfig _config;
        private readonly ScoreCalculator _calculator;

        public ScoreCalculatorTests()
        {
            _config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            _calculator = new ScoreCalculator(_config);
        }

        [Fact]
        public void CalculateBottomScore_SingleCard_DoublesScore()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ten)
            };
            var lastTrick = new List<Card> { new Card(Suit.Diamond, Rank.Three) };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(30, score); // (5+10) × 2
        }

        [Fact]
        public void CalculateBottomScore_Pair_QuadruplesScore()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.King)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Three)
            };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(80, score); // (10+10) × 4
        }

        [Fact]
        public void CalculateBottomScore_TwoConsecutivePairs_MultipliesByFour()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Diamond, Rank.King)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Four)
            };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(100, score); // (5+10+10) × 4 = 25 × 4
        }

        [Fact]
        public void CalculateBottomScore_ThreeConsecutivePairs_MultipliesByEight()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ten)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Five)
            };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(160, score); // (5+5+10) × 8 = 20 × 8
        }

        [Fact]
        public void CalculateBottomScore_NoScoreCards_ReturnsZero()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Six)
            };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(0, score); // 0 × 4
        }

        [Fact]
        public void CalculateBottomScore_AllScoreCards_MaximumScore()
        {
            var bottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Club, Rank.Five),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.King)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace)
            };

            int score = _calculator.CalculateBottomScore(bottom, lastTrick);

            Assert.Equal(260, score); // (5+10+5+10+5+10+10+10) × 4 = 65 × 4
        }
    }
}
