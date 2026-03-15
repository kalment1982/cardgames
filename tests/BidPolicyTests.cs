using System.Collections.Generic;
using TractorGame.Core.AI.Bidding;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class BidPolicyTests
    {
        [Fact]
        public void RoundLuckProbability_IsWithinConfiguredRange()
        {
            var policy = new BidPolicy(seed: 12345);
            Assert.InRange(policy.RoundLuckProbability, 0.1, 0.3);
        }

        [Fact]
        public void SelectBidAttempt_C2MajorityTrump_BidsInEarlyStage()
        {
            var policy = new BidPolicy(seed: 7);
            var context = new BidPolicy.DecisionContext
            {
                PlayerIndex = 1,
                DealerIndex = 0,
                LevelRank = Rank.Five,
                RoundIndex = 3,
                CurrentBidPriority = -1,
                CurrentBidPlayer = -1,
                VisibleCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Five),
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Nine),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Diamond, Rank.Three)
                }
            };

            var decision = policy.Decide(context);
            var attempt = decision.AttemptCards;

            Assert.Single(attempt);
            Assert.Equal(Rank.Five, attempt[0].Rank);
            Assert.Equal(Suit.Spade, attempt[0].Suit);
            Assert.Equal(BidPolicy.ReasonC2, decision.PrimaryReason);
            Assert.Contains(BidPolicy.ReasonC2, decision.Reasons);
            Assert.False(decision.UsedLuck);
        }

        [Fact]
        public void SelectBidAttempt_WhenPairCanOvertakeSingle_ReturnsPair()
        {
            var policy = new BidPolicy(seed: 9);
            var context = new BidPolicy.DecisionContext
            {
                PlayerIndex = 2,
                DealerIndex = 0,
                LevelRank = Rank.Two,
                RoundIndex = 10,
                CurrentBidPriority = 0, // 场上已有单张亮主
                CurrentBidPlayer = 1,
                VisibleCards = new List<Card>
                {
                    new Card(Suit.Heart, Rank.Two),
                    new Card(Suit.Heart, Rank.Two),
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Club, Rank.Seven),
                    new Card(Suit.Diamond, Rank.Seven)
                }
            };

            var decision = policy.Decide(context);
            var attempt = decision.AttemptCards;

            Assert.Equal(2, attempt.Count);
            Assert.All(attempt, card =>
            {
                Assert.Equal(Rank.Two, card.Rank);
                Assert.Equal(Suit.Heart, card.Suit);
            });
            Assert.True(decision.CandidateScore > 0);
            Assert.NotNull(decision.CandidateSuit);
        }

        [Fact]
        public void SelectBidAttempt_WhenOnlySingleCannotOvertakePair_ReturnsEmpty()
        {
            var policy = new BidPolicy(seed: 11);
            var context = new BidPolicy.DecisionContext
            {
                PlayerIndex = 3,
                DealerIndex = 1,
                LevelRank = Rank.Three,
                RoundIndex = 16,
                CurrentBidPriority = 1, // 场上已有对子亮主
                CurrentBidPlayer = 0,
                VisibleCards = new List<Card>
                {
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Club, Rank.Jack),
                    new Card(Suit.Diamond, Rank.Nine)
                }
            };

            var attempt = policy.SelectBidAttempt(context);

            Assert.Empty(attempt);
        }
    }
}
