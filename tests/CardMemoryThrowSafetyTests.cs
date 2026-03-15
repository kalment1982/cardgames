using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class CardMemoryThrowSafetyTests
    {
        [Fact]
        public void EvaluateThrowSafety_SingleBlockedBySameSystemBiggerCard_IsNotDeterministicallySafe()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Diamond, Rank.Three)
            };
            var throwCards = new List<Card> { new Card(Suit.Heart, Rank.Ten) };

            var assessment = memory.EvaluateThrowSafety(
                throwCards,
                hand,
                myPosition: 0,
                opponentPositions: new List<int> { 1, 2, 3 });

            Assert.False(assessment.IsDeterministicallySafe);
            Assert.True(assessment.SuccessProbability < 1.0);
        }

        [Fact]
        public void EvaluateThrowSafety_PairUsesNoPairEvidence_AllFollowersNoPair_IsDeterministicallySafe()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);

            // 首发红桃对子，三家都未跟对子 -> 记录为无红桃对子能力
            var trick = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Heart, Rank.Five)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Diamond, Rank.Three)
                }),
                new TrickPlay(2, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Spade, Rank.Four)
                }),
                new TrickPlay(3, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Club, Rank.Three)
                })
            };
            memory.RecordTrick(trick);

            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Diamond, Rank.King)
            };
            var throwCards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten)
            };

            var assessment = memory.EvaluateThrowSafety(
                throwCards,
                hand,
                myPosition: 0,
                opponentPositions: new List<int> { 1, 2, 3 });

            Assert.True(assessment.IsDeterministicallySafe);
            Assert.True(assessment.SuccessProbability >= 0.99);
        }

        [Fact]
        public void EvaluateThrowSafety_PairChecksAnyOfThreeFollowers_ThirdFollowerCanBlock_ThenUnsafe()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);

            // 仅玩家1和3被推断为无红桃对子；玩家2显式跟过红桃对子。
            var trick = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Heart, Rank.Five)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Diamond, Rank.Three)
                }),
                new TrickPlay(2, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Ace)
                }),
                new TrickPlay(3, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Spade, Rank.Four)
                })
            };
            memory.RecordTrick(trick);

            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Diamond, Rank.King)
            };
            var throwCards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten)
            };

            var twoFollowers = memory.EvaluateThrowSafety(
                throwCards,
                hand,
                myPosition: 0,
                opponentPositions: new List<int> { 1, 3 });
            var threeFollowers = memory.EvaluateThrowSafety(
                throwCards,
                hand,
                myPosition: 0,
                opponentPositions: new List<int> { 1, 2, 3 });

            Assert.True(twoFollowers.IsDeterministicallySafe);
            Assert.False(threeFollowers.IsDeterministicallySafe);
            Assert.True(threeFollowers.SuccessProbability < twoFollowers.SuccessProbability);
        }
    }
}
