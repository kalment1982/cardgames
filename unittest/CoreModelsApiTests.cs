using System.Collections.Generic;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class CardApiCoverageTests
    {
        [Fact]
        public void Card_Constructor_SetsSuitAndRank()
        {
            var card = new Card(Suit.Spade, Rank.Ace);

            Assert.Equal(Suit.Spade, card.Suit);
            Assert.Equal(Rank.Ace, card.Rank);
        }

        [Fact]
        public void Card_IsJoker_ReturnsTrue_ForJokers()
        {
            var small = new Card(Suit.Joker, Rank.SmallJoker);
            var big = new Card(Suit.Joker, Rank.BigJoker);

            Assert.True(small.IsJoker);
            Assert.True(big.IsJoker);
        }

        [Fact]
        public void Card_IsScoreCard_AndScore_ReturnExpected()
        {
            var five = new Card(Suit.Spade, Rank.Five);
            var ten = new Card(Suit.Heart, Rank.Ten);
            var king = new Card(Suit.Club, Rank.King);
            var ace = new Card(Suit.Diamond, Rank.Ace);

            Assert.True(five.IsScoreCard);
            Assert.True(ten.IsScoreCard);
            Assert.True(king.IsScoreCard);
            Assert.False(ace.IsScoreCard);

            Assert.Equal(5, five.Score);
            Assert.Equal(10, ten.Score);
            Assert.Equal(10, king.Score);
            Assert.Equal(0, ace.Score);
        }

        [Fact]
        public void Card_Equals_AndGetHashCode_WorkForEqualCards()
        {
            var a = new Card(Suit.Heart, Rank.Ten);
            var b = new Card(Suit.Heart, Rank.Ten);

            Assert.True(a.Equals(b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        [Fact]
        public void Card_ToString_ReturnsExpected()
        {
            var joker = new Card(Suit.Joker, Rank.BigJoker);
            var normal = new Card(Suit.Spade, Rank.Ace);

            Assert.Equal("大王", joker.ToString());
            Assert.Equal("♠A", normal.ToString());
        }
    }

    public class GameConfigApiCoverageTests
    {
        [Fact]
        public void GameConfig_IsTrump_AndGetCardCategory_Work()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

            var joker = new Card(Suit.Joker, Rank.SmallJoker);
            var level = new Card(Suit.Spade, Rank.Five);
            var trumpSuit = new Card(Suit.Heart, Rank.Ace);
            var suit = new Card(Suit.Club, Rank.Ace);

            Assert.True(config.IsTrump(joker));
            Assert.True(config.IsTrump(level));
            Assert.True(config.IsTrump(trumpSuit));
            Assert.False(config.IsTrump(suit));

            Assert.Equal(CardCategory.Trump, config.GetCardCategory(trumpSuit));
            Assert.Equal(CardCategory.Suit, config.GetCardCategory(suit));
        }
    }

    public class CardComparerApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void Compare_TrumpBeatsSuit()
        {
            var comparer = new CardComparer(_config);
            var trump = new Card(Suit.Heart, Rank.Ace);
            var suit = new Card(Suit.Spade, Rank.Ace);

            Assert.True(comparer.Compare(trump, suit) > 0);
            Assert.True(comparer.Compare(suit, trump) < 0);
        }

        [Fact]
        public void Compare_BigJokerBeatsSmallJoker()
        {
            var comparer = new CardComparer(_config);
            var big = new Card(Suit.Joker, Rank.BigJoker);
            var small = new Card(Suit.Joker, Rank.SmallJoker);

            Assert.True(comparer.Compare(big, small) > 0);
        }

        [Fact]
        public void Compare_MainLevelBeatsOtherLevel()
        {
            var comparer = new CardComparer(_config);
            var mainLevel = new Card(Suit.Heart, Rank.Five);
            var sideLevel = new Card(Suit.Spade, Rank.Five);

            Assert.True(comparer.Compare(mainLevel, sideLevel) > 0);
        }

        [Fact]
        public void Compare_SameSuit_ByRank()
        {
            var comparer = new CardComparer(_config);
            var ten = new Card(Suit.Spade, Rank.Ten);
            var king = new Card(Suit.Spade, Rank.King);

            Assert.True(comparer.Compare(ten, king) < 0);
        }

        [Fact]
        public void Compare_DifferentSuitNonTrump_ReturnsZero()
        {
            var comparer = new CardComparer(_config);
            var spade = new Card(Suit.Spade, Rank.Ten);
            var diamond = new Card(Suit.Diamond, Rank.Ten);

            Assert.Equal(0, comparer.Compare(spade, diamond));
        }
    }

    public class CardPatternApiCoverageTests
    {
        private readonly GameConfig _config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };

        [Fact]
        public void IsPair_ReturnsExpected()
        {
            var pair = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.King)
            };
            var notPair = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Heart, Rank.King)
            };

            Assert.True(CardPattern.IsPair(pair));
            Assert.False(CardPattern.IsPair(notPair));
        }

        [Fact]
        public void Type_ReturnsExpected_ForSinglePairTractorMixed()
        {
            var single = new CardPattern(new List<Card> { new Card(Suit.Spade, Rank.Ace) }, _config);
            var pair = new CardPattern(new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Queen)
            }, _config);
            var tractor = new CardPattern(new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight)
            }, _config);
            var mixed = new CardPattern(new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen)
            }, _config);

            Assert.Equal(PatternType.Single, single.Type);
            Assert.Equal(PatternType.Pair, pair.Type);
            Assert.Equal(PatternType.Tractor, tractor.Type);
            Assert.Equal(PatternType.Mixed, mixed.Type);
        }

        [Fact]
        public void IsTractor_ReturnsTrue_ForGapTractorWithLevel()
        {
            var cards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Six)
            };

            var pattern = new CardPattern(cards, _config);
            Assert.True(pattern.IsTractor(cards));
        }
    }
}
