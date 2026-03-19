using System.Collections.Generic;
using TractorGame.Core.AI.V30.Memory;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Memory
{
    public class InferenceEngineV30Tests
    {
        private readonly InferenceEngineV30 _engine = new();

        [Fact]
        public void BuildSuitKnowledge_WhenConfirmedVoid_IsConfirmedVoidState()
        {
            var knowledge = _engine.BuildSuitKnowledge(
                playerIndex: 1,
                suit: Suit.Heart,
                confirmedVoid: true,
                probabilityHasSuit: 0.9);

            Assert.Equal(SuitKnowledgeStateV30.ConfirmedVoid, knowledge.State);
            Assert.True(knowledge.ConfirmedVoid);
        }

        [Fact]
        public void BuildSuitKnowledge_WhenProbabilityAtLeast70Percent_IsProbablyHasSuit()
        {
            var knowledge = _engine.BuildSuitKnowledge(
                playerIndex: 2,
                suit: Suit.Spade,
                confirmedVoid: false,
                probabilityHasSuit: 0.75);

            Assert.Equal(SuitKnowledgeStateV30.ProbablyHasSuit, knowledge.State);
        }

        [Fact]
        public void BuildSuitKnowledge_WhenProbabilityLow_IsProbablyVoid()
        {
            var knowledge = _engine.BuildSuitKnowledge(
                playerIndex: 3,
                suit: Suit.Club,
                confirmedVoid: false,
                probabilityHasSuit: 0.20);

            Assert.Equal(SuitKnowledgeStateV30.ProbablyVoid, knowledge.State);
        }

        [Fact]
        public void ObserveFollowAction_WhenPlayerFailsToFollowLedSuit_ConfirmsVoid()
        {
            var playedCards = new List<Card>
            {
                new(Suit.Diamond, Rank.Five)
            };

            var knowledge = _engine.ObserveFollowAction(
                playerIndex: 1,
                ledSuit: Suit.Heart,
                playedCards: playedCards);

            Assert.True(knowledge.ConfirmedVoid);
            Assert.Equal(SuitKnowledgeStateV30.ConfirmedVoid, knowledge.State);
        }

        [Fact]
        public void BuildSnapshot_CanQueryProbablyHasSuitAndConfirmedVoid()
        {
            var snapshot = _engine.BuildSnapshot(new[]
            {
                _engine.BuildSuitKnowledge(1, Suit.Heart, confirmedVoid: false, probabilityHasSuit: 0.80),
                _engine.BuildSuitKnowledge(2, Suit.Heart, confirmedVoid: true, probabilityHasSuit: 0.00)
            });

            Assert.True(snapshot.IsProbablyHasSuit(1, Suit.Heart));
            Assert.True(snapshot.IsConfirmedVoid(2, Suit.Heart));
        }
    }
}
