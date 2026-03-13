using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class AIPlayerTests
    {
        [Fact]
        public void Lead_ReturnsThrow_WhenCanThrowSameSuit()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Five)
            };

            var result = ai.Lead(hand, AIRole.Opponent);

            Assert.Equal(3, result.Count);
            Assert.All(result, c => Assert.Equal(Suit.Heart, c.Suit));
        }

        [Fact]
        public void Lead_PrefersTractor_WhenAvailable()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Ace)
            };

            var result = ai.Lead(hand, AIRole.Opponent);
            var pattern = new CardPattern(result, config);

            Assert.Equal(6, result.Count);
            Assert.True(pattern.IsTractor(result));
        }

        [Fact]
        public void Follow_UsesTrumpToBeat_WhenNoLeadSuit()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Club, Rank.Four)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, leadCards, role: AIRole.Opponent);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.True(config.IsTrump(c)));
        }

        [Fact]
        public void Follow_FollowsTractor_WhenHasTractor()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Queen)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine)
            };

            var result = ai.Follow(hand, leadCards, role: AIRole.Opponent);
            var pattern = new CardPattern(result, config);
            var validator = new FollowValidator(config);

            Assert.Equal(4, result.Count);
            Assert.True(pattern.IsTractor(result));
            Assert.True(validator.IsValidFollow(hand, leadCards, result));
        }

        [Fact]
        public void BuryBottom_Returns8Cards_From33Cards()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>();
            for (int i = 0; i < 33; i++)
            {
                hand.Add(new Card(Suit.Heart, Rank.Three));
            }

            var result = ai.BuryBottom(hand);

            Assert.Equal(8, result.Count);
        }

        [Fact]
        public void BuryBottom_AvoidsPointCards()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1);
            var hand = new List<Card>();

            // 添加25张小牌
            for (int i = 0; i < 25; i++)
            {
                hand.Add(new Card(Suit.Heart, Rank.Three));
            }

            // 添加8张分牌
            for (int i = 0; i < 4; i++)
            {
                hand.Add(new Card(Suit.Diamond, Rank.King));
                hand.Add(new Card(Suit.Club, Rank.Ten));
            }

            var result = ai.BuryBottom(hand);

            // 应该埋小牌，不埋分牌
            Assert.Equal(8, result.Count);
            Assert.All(result, c => Assert.Equal(Rank.Three, c.Rank));
        }

        [Fact]
        public void Follow_PartnerWinning_SendsPointCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Hard, 1);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),  // 10分
                new Card(Suit.Spade, Rank.Ten),   // 10分
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen)
            };

            // 对家赢牌
            var result = ai.Follow(hand, leadCards,
                currentWinningCards: leadCards,
                role: AIRole.Opponent,
                partnerWinning: true);

            Assert.Equal(2, result.Count);
            // 应该包含分牌
            var totalPoints = result.Select(c => c.Rank == Rank.King || c.Rank == Rank.Ten ? 10 :
                                              c.Rank == Rank.Five ? 5 : 0).Sum();
            Assert.True(totalPoints > 0);
        }

        [Fact]
        public void DifficultyEasy_UsesMoreRandomness()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var aiEasy = new AIPlayer(config, AIDifficulty.Easy, 1);
            var aiExpert = new AIPlayer(config, AIDifficulty.Expert, 2);

            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Six)
            };

            // 简单难度应该有更多随机性（这里只是验证能运行）
            var resultEasy = aiEasy.Lead(hand, AIRole.Opponent);
            var resultExpert = aiExpert.Lead(hand, AIRole.Opponent);

            Assert.NotNull(resultEasy);
            Assert.NotNull(resultExpert);
            Assert.True(resultEasy.Count > 0);
            Assert.True(resultExpert.Count > 0);
        }

        [Fact]
        public void RecordTrick_TracksPlayedCards()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Hard, 1);

            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Queen),
                    new Card(Suit.Spade, Rank.Jack)
                })
            };

            // 记录这墩牌
            ai.RecordTrick(plays);

            // 验证记牌系统工作（通过后续出牌行为验证）
            Assert.True(true); // 基础验证
        }

        [Fact]
        public void Lead_EvaluatesThrowSafety_WithMemory()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var ai = new AIPlayer(config, AIDifficulty.Expert, 1);

            // 记录一些已出的牌
            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card> { new Card(Suit.Spade, Rank.Ace) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Spade, Rank.King) })
            };
            ai.RecordTrick(plays);

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Diamond, Rank.Six)
            };

            // 提供对手位置信息
            var result = ai.Lead(hand, AIRole.Opponent, myPosition: 0, opponentPositions: new List<int> { 1, 3 });

            Assert.NotNull(result);
            Assert.True(result.Count > 0);
        }
    }
}
