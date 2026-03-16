using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class BidPolicy2Tests
    {
        [Fact]
        public void Decide_C2MajorityTrump_BidsSingleLevelCard()
        {
            var policy = new BidPolicy2(seed: 7);
            var context = new RuleAIContextBuilder(new GameConfig { LevelRank = Rank.Five }, sessionStyleSeed: 7)
                .BuildBidContext(
                    new List<Card>
                    {
                        new Card(Suit.Spade, Rank.Five),
                        new Card(Suit.Spade, Rank.Ace),
                        new Card(Suit.Spade, Rank.King),
                        new Card(Suit.Spade, Rank.Ten),
                        new Card(Suit.Spade, Rank.Nine),
                        new Card(Suit.Joker, Rank.SmallJoker),
                        new Card(Suit.Heart, Rank.Five),
                        new Card(Suit.Diamond, Rank.Three)
                    },
                    AIRole.Opponent,
                    playerIndex: 1,
                    dealerIndex: 0,
                    roundIndex: 3,
                    currentBidPriority: -1,
                    currentBidPlayer: -1);

            var decision = policy.Decide(context);

            Assert.Single(decision.AttemptCards);
            Assert.Equal(Suit.Spade, decision.AttemptCards[0].Suit);
            Assert.Contains(BidPolicy2.ReasonC2, decision.Reasons);
            Assert.Equal("BidPolicy2", decision.Explanation.PhasePolicy);
        }
    }
}
