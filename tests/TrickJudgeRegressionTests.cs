using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class TrickJudgeRegressionTests
    {
        [Fact]
        public void DetermineWinner_MixedLead_IncompleteTrumpCut_DoesNotBeatLead()
        {
            // 回归样例（用户日志）：
            // 设主：♦，打2。首家出：♣A ♣10 ♣10
            // 东家跟：♣J 大王 ♦2（含主牌）
            // 由于未满足对子结构，主牌不形成有效压制，期望首家继续赢。
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Diamond
            };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Club, Rank.Ten),
                    new Card(Suit.Club, Rank.Ten)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Club, Rank.Jack),
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Diamond, Rank.Two)
                }),
                new TrickPlay(2, new List<Card>
                {
                    new Card(Suit.Club, Rank.Three),
                    new Card(Suit.Club, Rank.Four),
                    new Card(Suit.Club, Rank.Five)
                }),
                new TrickPlay(3, new List<Card>
                {
                    new Card(Suit.Club, Rank.Three),
                    new Card(Suit.Club, Rank.Six),
                    new Card(Suit.Club, Rank.Seven)
                })
            };

            var winner = judge.DetermineWinner(plays);
            Assert.Equal(0, winner);
        }

        [Fact]
        public void DetermineWinner_MixedLead_IncompleteTrumpCut_WithTwoSuitSingles_DoesNotBeatLead()
        {
            // 用户日志回归：
            // 首家：♦A ♦8 ♦8
            // 西家：♦K ♦J 小王
            // 西家缺少对子结构，小王不应单独把整手压过去。
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Ace),
                    new Card(Suit.Diamond, Rank.Eight),
                    new Card(Suit.Diamond, Rank.Eight)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Three),
                    new Card(Suit.Diamond, Rank.Five),
                    new Card(Suit.Diamond, Rank.Nine)
                }),
                new TrickPlay(2, new List<Card>
                {
                    new Card(Suit.Diamond, Rank.Three),
                    new Card(Suit.Diamond, Rank.Four),
                    new Card(Suit.Diamond, Rank.Four)
                }),
                new TrickPlay(3, new List<Card>
                {
                    new Card(Suit.Diamond, Rank.King),
                    new Card(Suit.Diamond, Rank.Jack),
                    new Card(Suit.Joker, Rank.SmallJoker)
                })
            };

            var winner = judge.DetermineWinner(plays);
            Assert.Equal(0, winner);
        }

        [Fact]
        public void DetermineWinner_MixedLead_FullTrumpComponents_CanBeatLead()
        {
            // 首家：♥A ♥8 ♥8（单张 + 对子）
            // 跟牌方：♠4 ♠4 小王（主牌对子 + 主牌单张）
            // 对应结构都满足，主牌压副牌，应由跟牌方获胜。
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Heart, Rank.Eight)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Four),
                    new Card(Suit.Spade, Rank.Four),
                    new Card(Suit.Joker, Rank.SmallJoker)
                })
            };

            var winner = judge.DetermineWinner(plays);
            Assert.Equal(1, winner);
        }

        [Fact]
        public void DetermineWinner_PartialSuitFollowWithTrumpFiller_DoesNotBeatLead()
        {
            // 用户日志回归：
            // 设主：♦，打2。南首发：♠Q ♠J
            // 西跟牌：♠5 ♦4（只跟出1张黑桃，另1张是主牌补缺）
            // 该手属于“部分跟同门 + 主牌垫/毙”，不应压过完整首发。
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Diamond
            };
            var judge = new TrickJudge(config);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Queen),
                    new Card(Suit.Spade, Rank.Jack)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Heart, Rank.Ace)
                }),
                new TrickPlay(2, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Heart, Rank.Five)
                }),
                new TrickPlay(3, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Five),
                    new Card(Suit.Diamond, Rank.Four)
                })
            };

            var winner = judge.DetermineWinner(plays);
            Assert.Equal(0, winner);
        }
    }
}
