using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class ScoreCalculatorApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void CalculateBottomScore_SingleMultiplier()
        {
            var calc = new ScoreCalculator(_config);
            var bottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Diamond, Rank.Ten)
            };
            var lastTrick = new List<Card> { new Card(Suit.Spade, Rank.Ace) };

            Assert.Equal(80, calc.CalculateBottomScore(bottom, lastTrick));
        }

        [Fact]
        public void CalculateBottomScore_PairMultiplier()
        {
            var calc = new ScoreCalculator(_config);
            var bottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Diamond, Rank.Ten)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace)
            };

            Assert.Equal(160, calc.CalculateBottomScore(bottom, lastTrick));
        }

        [Fact]
        public void CalculateBottomScore_TractorMultiplier_TwoPairs()
        {
            var calc = new ScoreCalculator(_config);
            var bottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Diamond, Rank.Ten)
            };
            var lastTrick = new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight)
            };

            Assert.Equal(160, calc.CalculateBottomScore(bottom, lastTrick));
        }
    }
}
