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

        [Fact]
        public void Decide_WhenPairSmallJokersCanOvertakePair_BidsNoTrump()
        {
            var policy = new BidPolicy2(seed: 13);
            var context = new RuleAIContextBuilder(new GameConfig { LevelRank = Rank.Seven }, sessionStyleSeed: 13)
                .BuildBidContext(
                    new List<Card>
                    {
                        new Card(Suit.Joker, Rank.SmallJoker),
                        new Card(Suit.Joker, Rank.SmallJoker),
                        new Card(Suit.Heart, Rank.Seven),
                        new Card(Suit.Spade, Rank.Seven),
                        new Card(Suit.Heart, Rank.Ace),
                        new Card(Suit.Club, Rank.King)
                    },
                    AIRole.Opponent,
                    playerIndex: 0,
                    dealerIndex: 1,
                    roundIndex: 12,
                    currentBidPriority: 1,
                    currentBidPlayer: 2);

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.AttemptCards.Count);
            Assert.All(decision.AttemptCards, card =>
            {
                Assert.True(card.IsJoker);
                Assert.Equal(Rank.SmallJoker, card.Rank);
            });
            Assert.Equal("Joker", decision.CandidateSuit);
            Assert.True(decision.CandidatePriority >= 2);
            Assert.Contains(BidPolicy2.ReasonC1, decision.Reasons);
        }

        [Fact]
        public void Decide_WhenBigJokerPairFacesSmallJokerNoTrump_UsesBigJokerPair()
        {
            var policy = new BidPolicy2(seed: 17);
            var context = new RuleAIContextBuilder(new GameConfig { LevelRank = Rank.Nine }, sessionStyleSeed: 17)
                .BuildBidContext(
                    new List<Card>
                    {
                        new Card(Suit.Joker, Rank.BigJoker),
                        new Card(Suit.Joker, Rank.BigJoker),
                        new Card(Suit.Spade, Rank.Nine),
                        new Card(Suit.Heart, Rank.Nine),
                        new Card(Suit.Club, Rank.Ace)
                    },
                    AIRole.DealerPartner,
                    playerIndex: 2,
                    dealerIndex: 0,
                    roundIndex: 18,
                    currentBidPriority: 2,
                    currentBidPlayer: 1);

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.AttemptCards.Count);
            Assert.All(decision.AttemptCards, card =>
            {
                Assert.True(card.IsJoker);
                Assert.Equal(Rank.BigJoker, card.Rank);
            });
            Assert.Equal(3, decision.CandidatePriority);
        }
    }
}
