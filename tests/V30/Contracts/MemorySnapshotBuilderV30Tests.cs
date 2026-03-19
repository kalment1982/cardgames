using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V30.Contracts
{
    public class MemorySnapshotBuilderV30Tests
    {
        [Fact]
        public void Build_CopiesPlayedVoidAndEvidenceSnapshots()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            memory.RecordTrick(new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Ace)
                }),
                new TrickPlay(1, new List<Card>
                {
                    new Card(Suit.Heart, Rank.Three),
                    new Card(Suit.Heart, Rank.Four)
                })
            });

            var snapshot = new MemorySnapshotBuilderV30().Build(memory, new List<Card> { new Card(Suit.Club, Rank.Ten) });

            Assert.Contains("♠A", snapshot.PlayedCountByCard.Keys);
            Assert.Contains("Spade", snapshot.VoidSuitsByPlayer[1]);
            Assert.Contains("Suit:Spade", snapshot.NoPairEvidence[1]);
            Assert.Contains("♣10", snapshot.KnownBottomCards);
            Assert.True(snapshot.PlayedScoreTotal >= 0);
            Assert.True(snapshot.PlayedScoreCardCount >= 0);
        }

        [Fact]
        public void Build_NullMemory_ReturnsMinimalSnapshot()
        {
            var snapshot = new MemorySnapshotBuilderV30().Build(
                memory: null,
                knownBottomCards: new List<Card> { new Card(Suit.Diamond, Rank.Five) });

            Assert.Empty(snapshot.PlayedCountByCard);
            Assert.Single(snapshot.KnownBottomCards);
            Assert.Equal("♦5", snapshot.KnownBottomCards[0]);
            Assert.Equal(0, snapshot.PlayedScoreTotal);
        }
    }
}

