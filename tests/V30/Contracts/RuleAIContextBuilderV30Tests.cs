using System;
using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Contracts
{
    public class RuleAIContextBuilderV30Tests
    {
        [Fact]
        public void BuildLeadContext_MinimalFields_Succeeds()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var builder = new RuleAIContextBuilderV30(config, AIDifficulty.Hard, memory);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Club, Rank.Ten)
            };

            var context = builder.BuildLeadContext(
                hand: hand,
                role: AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                visibleBottomCards: new List<Card> { new Card(Suit.Diamond, Rank.King) },
                trickIndex: 1,
                turnIndex: 0,
                defenderScore: 45);

            Assert.Equal(PhaseKindV30.Lead, context.Phase);
            Assert.Equal(AIRole.Dealer, context.Role);
            Assert.Equal(10, context.DecisionFrame.BottomPoints);
            Assert.Equal(0.70, context.DecisionFrame.ProbabilityThreshold);
            Assert.True(context.HandProfile.TrumpCount >= 2);
            Assert.NotNull(context.MemorySnapshot);
        }

        [Fact]
        public void BuildFollowContext_MissingLeadCards_WithStrictValidation_Throws()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilderV30(config);

            Assert.Throws<ArgumentException>(() =>
                builder.BuildFollowContext(
                    hand: new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                    leadCards: null,
                    currentWinningCards: null,
                    role: AIRole.Opponent,
                    partnerWinning: false));
        }

        [Fact]
        public void BuildLeadContext_NullHand_WhenStrictDisabled_DoesNotThrow()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilderV30(
                config,
                featureFlags: new V30FeatureFlags
                {
                    StrictContractValidation = false,
                    ProbabilityThreshold = 0.70
                });

            var context = builder.BuildLeadContext(
                hand: null,
                role: AIRole.Opponent,
                defenderScore: 60);

            Assert.NotNull(context);
            Assert.Empty(context.MyHand);
            Assert.Equal(0.70, context.DecisionFrame.ProbabilityThreshold);
        }

        [Fact]
        public void BuildLeadContext_OpponentAtSixty_EntersBottomContestPressure()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Spade };
            var builder = new RuleAIContextBuilderV30(config);

            var context = builder.BuildLeadContext(
                hand: new List<Card> { new Card(Suit.Heart, Rank.Ace) },
                role: AIRole.Opponent,
                defenderScore: 60);

            Assert.True(context.DecisionFrame.BottomContestPressure >= RiskLevelV30.Medium);
        }

        [Fact]
        public void BuildLeadContext_DealerBottomHighRisk_UsesStrongProtectThreshold()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Spade };
            var builder = new RuleAIContextBuilderV30(config);

            var context = builder.BuildLeadContext(
                hand: new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                role: AIRole.Dealer,
                defenderScore: 60,
                bottomPoints: 10);

            Assert.Equal(RiskLevelV30.High, context.DecisionFrame.BottomRiskPressure);
        }
    }
}

