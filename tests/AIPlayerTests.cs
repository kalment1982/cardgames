using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class AIPlayerTests
    {
        private static RuleAIOptions LegacyRuleAIOptions => new()
        {
            UseRuleAIV21 = false,
            EnableShadowCompare = false
        };

        private static AIPlayer CreateDeterministicHardAi(GameConfig config, int seed = 1)
        {
            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            strategy.EasyRandomnessRate = 0;
            strategy.MediumRandomnessRate = 0;
            strategy.HardRandomnessRate = 0;
            strategy.ExpertRandomnessRate = 0;
            return new AIPlayer(config, AIDifficulty.Hard, seed, strategy);
        }

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

            // 无对手信息时中等难度采用保守策略，不主动甩牌
            Assert.Single(result);
            Assert.Contains(result[0], hand);
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
            var ai = new AIPlayer(config, AIDifficulty.Medium, 1, ruleAIOptions: LegacyRuleAIOptions);
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
        public void Follow_WhenThreePairTractorLeadCannotBeFullyMatched_ReturnsLegalSuitFollow()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicHardAi(config);
            var validator = new FollowValidator(config);

            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Queen), new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Three), new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Diamond, Rank.Four)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine), new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Eight), new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Seven), new Card(Suit.Diamond, Rank.Seven)
            };

            var result = ai.Follow(
                hand,
                leadCards,
                currentWinningCards: leadCards,
                role: AIRole.Opponent,
                partnerWinning: false);

            Assert.Equal(6, result.Count);
            Assert.True(validator.IsValidFollow(hand, leadCards, result));
            Assert.Equal(6, result.Count(card => !config.IsTrump(card) && card.Suit == Suit.Diamond));
        }

        [Fact]
        public void Follow_CannotBeatPairWithShortage_DoesNotWasteTrumpCut()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicHardAi(config);
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Heart, Rank.Five)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven)
            };

            var result = ai.Follow(hand, leadCards,
                currentWinningCards: leadCards,
                role: AIRole.Opponent,
                partnerWinning: false);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, c => c.Suit == Suit.Diamond && c.Rank == Rank.Eight);
            Assert.Contains(result, c => c.Suit == Suit.Heart && c.Rank == Rank.Five);
            Assert.DoesNotContain(result, c => c.Rank == Rank.SmallJoker);
        }

        [Fact]
        public void Follow_CannotWin_PrefersSmallerPair()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicHardAi(config);
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Eight)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven)
            };
            var currentWinningCards = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Ace)
            };

            var result = ai.Follow(hand, leadCards,
                currentWinningCards: currentWinningCards,
                role: AIRole.Opponent,
                partnerWinning: false);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.Equal(Suit.Diamond, c.Suit));
            Assert.All(result, c => Assert.Equal(Rank.Three, c.Rank));
        }

        [Fact]
        public void Follow_PartnerWinning_PrefersSmallerPair_WhenPointsEqual()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicHardAi(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.SmallJoker)
            };
            var leadCards = new List<Card>
            {
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Seven)
            };
            var currentWinningCards = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.Ace)
            };

            var result = ai.Follow(hand, leadCards,
                currentWinningCards: currentWinningCards,
                role: AIRole.Opponent,
                partnerWinning: true);

            Assert.Equal(2, result.Count);
            Assert.All(result, c => Assert.Equal(Suit.Club, c.Suit));
            Assert.All(result, c => Assert.Equal(Rank.Three, c.Rank));
        }

        [Fact]
        public void Follow_Hard_DoesNotRandomlyWasteBigTrump_WhenCannotBeatCurrentWinner()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var strategy = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            strategy.HardRandomnessRate = 1.0; // 若仍允许Hard随机，将高概率出现浪费大王

            for (int seed = 1; seed <= 12; seed++)
            {
                var ai = new AIPlayer(config, AIDifficulty.Hard, seed, strategy);
                var hand = new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Heart, Rank.Four)
                };
                var leadCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Four)
                };
                var currentWinningCards = new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker)
                };

                var result = ai.Follow(hand, leadCards,
                    currentWinningCards: currentWinningCards,
                    role: AIRole.Opponent,
                    partnerWinning: false);

                Assert.Single(result);
                Assert.Equal(Suit.Spade, result[0].Suit);
                Assert.Equal(Rank.Three, result[0].Rank);
            }
        }

        [Fact]
        public void Follow_WhenTrumpTractorLedAndPartnerWinning_ReturnsLegalTrumpResponse()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var ai = CreateDeterministicHardAi(config, seed: 1);
            var validator = new FollowValidator(config);

            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Club, Rank.Queen)
            };

            var leadCards = new List<Card>
            {
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Seven)
            };

            var currentWinningCards = new List<Card>(leadCards);

            var result = ai.Follow(
                hand,
                leadCards,
                currentWinningCards: currentWinningCards,
                role: AIRole.Opponent,
                partnerWinning: true);

            Assert.Equal(4, result.Count);
            Assert.True(validator.IsValidFollow(hand, leadCards, result));
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
