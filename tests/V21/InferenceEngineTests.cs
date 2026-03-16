using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class InferenceEngineTests
    {
        [Fact]
        public void Build_EstimatesTrumpAndBottomThreat()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var memory = new CardMemory(config);
            memory.RecordTrick(new List<TrickPlay>
            {
                new TrickPlay(0, new List<Card> { new Card(Suit.Spade, Rank.Three) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Heart, Rank.Three) })
            });

            var snapshot = new InferenceEngine(config).Build(
                memory,
                new List<Card> { new Card(Suit.Spade, Rank.Ace), new Card(Suit.Joker, Rank.SmallJoker) },
                myPosition: 0,
                cardsLeftMin: 5,
                visibleBottomCards: new List<Card> { new Card(Suit.Heart, Rank.King), new Card(Suit.Club, Rank.Ten) });

            Assert.True(snapshot.EstimatedTrumpCountByPlayer.ContainsKey(1));
            Assert.True(snapshot.PairPotentialBySystem.ContainsKey("Suit:Spade"));
            Assert.Equal(RiskLevel.High, snapshot.EndgameBottomThreat.Level);
            Assert.True(snapshot.MateHoldConfidence.Probability >= 0.5);
        }
    }
}
