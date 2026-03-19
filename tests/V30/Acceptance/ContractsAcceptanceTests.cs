using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.AI.V30.Memory;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Acceptance
{
    public class ContractsAcceptanceTests
    {
        [Fact]
        public void Contracts_ContextBuilder_ShouldBuildMinimalContext()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilderV30(config);
            var context = builder.BuildLeadContext(
                hand: new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Club, Rank.Ten)
                },
                role: AIRole.Dealer,
                playerIndex: 0,
                dealerIndex: 0,
                defenderScore: 45);

            Assert.Equal(PhaseKindV30.Lead, context.Phase);
            Assert.Equal(AIRole.Dealer, context.Role);
            Assert.Equal(0, context.PlayerIndex);
            Assert.Equal(0, context.DealerIndex);
            Assert.Equal(3, context.MyHand.Count);
            Assert.NotNull(context.HandProfile);
            Assert.NotNull(context.DecisionFrame);
        }

        [Fact]
        public void Contracts_ContextBuilder_ShouldRejectMissingRequiredFacts()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var builder = new RuleAIContextBuilderV30(config);

            Assert.Throws<System.ArgumentException>(() =>
                builder.BuildFollowContext(
                    hand: new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                    leadCards: null,
                    currentWinningCards: null,
                    role: AIRole.Opponent,
                    partnerWinning: false));
        }

        [Fact]
        public void Contracts_ConfidenceBoundary_70Percent_ShouldBeProbabilisticOnly()
        {
            var inference = new InferenceEngineV30();
            var knowledge = inference.BuildSuitKnowledge(
                playerIndex: 1,
                suit: Suit.Heart,
                confirmedVoid: false,
                probabilityHasSuit: 0.70);

            Assert.Equal(SuitKnowledgeStateV30.ProbablyHasSuit, knowledge.State);
            Assert.False(knowledge.ConfirmedVoid);
        }
    }
}
