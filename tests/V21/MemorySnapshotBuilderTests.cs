using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class MemorySnapshotBuilderTests
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

            var snapshot = new MemorySnapshotBuilder().Build(memory, new List<Card> { new Card(Suit.Club, Rank.Ten) });

            Assert.Contains("♠A", snapshot.PlayedCountByCard.Keys);
            Assert.Contains("Spade", snapshot.VoidSuitsByPlayer[1]);
            Assert.Contains("Suit:Spade", snapshot.NoPairEvidence[1]);
            Assert.Contains("♣10", snapshot.KnownBottomCards);
        }
    }
}
