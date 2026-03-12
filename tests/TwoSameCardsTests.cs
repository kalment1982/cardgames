using System.Collections.Generic;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    /// <summary>
    /// 测试相同牌的比较规则（先出为大）
    /// </summary>
    public class TwoSameCardsTests
    {
        [Fact]
        public void TwoBigJokers_FirstPlayerWins()
        {
            // 玩家0先出大王，玩家2后出大王
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card> { new Card(Suit.Joker, Rank.BigJoker) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Spade, Rank.Three) }),
                new TrickPlay(2, new List<Card> { new Card(Suit.Joker, Rank.BigJoker) }),
                new TrickPlay(3, new List<Card> { new Card(Suit.Heart, Rank.Ace) })
            };

            int winner = judge.DetermineWinner(plays);

            Assert.Equal(0, winner); // 玩家0应该赢
        }

        [Fact]
        public void TwoSmallJokers_FirstPlayerWins()
        {
            // 玩家1先出小王，玩家3后出小王
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(1, new List<Card> { new Card(Suit.Joker, Rank.SmallJoker) }),
                new TrickPlay(2, new List<Card> { new Card(Suit.Heart, Rank.King) }),
                new TrickPlay(3, new List<Card> { new Card(Suit.Joker, Rank.SmallJoker) }),
                new TrickPlay(0, new List<Card> { new Card(Suit.Club, Rank.Queen) })
            };

            int winner = judge.DetermineWinner(plays);

            Assert.Equal(1, winner); // 玩家1应该赢
        }

        [Fact]
        public void TwoSameLevelCards_FirstPlayerWins()
        {
            // 打2，玩家0先出♠2，玩家2后出♠2
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card> { new Card(Suit.Spade, Rank.Two) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Spade, Rank.Three) }),
                new TrickPlay(2, new List<Card> { new Card(Suit.Spade, Rank.Two) }),
                new TrickPlay(3, new List<Card> { new Card(Suit.Spade, Rank.Four) })
            };

            int winner = judge.DetermineWinner(plays);

            Assert.Equal(0, winner); // 玩家0应该赢
        }

        [Fact]
        public void CardComparer_TwoBigJokers_ShouldBeEqual()
        {
            // 测试 CardComparer 对两张大王的比较
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var comparer = new CardComparer(config);

            var bigJoker1 = new Card(Suit.Joker, Rank.BigJoker);
            var bigJoker2 = new Card(Suit.Joker, Rank.BigJoker);

            int result = comparer.Compare(bigJoker1, bigJoker2);

            // 两张大王应该相等（返回0）
            Assert.Equal(0, result);
        }
    }
}
