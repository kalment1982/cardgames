using System.Collections.Generic;
using TractorGame.Core.GameFlow;
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

        [Fact]
        public void SelectBidAttempt_WhenPairSmallJokersCanOvertakePair_ReturnsNoTrumpPair()
        {
            var policy = new BidPolicy(seed: 13);
            var context = new BidPolicy.DecisionContext
            {
                PlayerIndex = 0,
                DealerIndex = 1,
                LevelRank = Rank.Seven,
                RoundIndex = 12,
                CurrentBidPriority = 1,
                CurrentBidPlayer = 2,
                VisibleCards = new List<Card>
                {
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Joker, Rank.SmallJoker),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Spade, Rank.Seven),
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Club, Rank.King)
                }
            };

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.AttemptCards.Count);
            Assert.All(decision.AttemptCards, card =>
            {
                Assert.True(card.IsJoker);
                Assert.Equal(Rank.SmallJoker, card.Rank);
            });
            Assert.Equal("Joker", decision.CandidateSuit);
            Assert.True(decision.CandidatePriority >= 2);
        }

        [Fact]
        public void AutoBiddingFlow_FindsSeedWhereAiBidsNoTrump()
        {
            for (var seed = 1; seed <= 2000; seed++)
            {
                var game = new Game(seed);
                game.StartGame();
                RunAutoBidding(game, seed);
                if (game.CurrentBidSuit != Suit.Joker)
                    continue;

                var finalize = game.FinalizeTrumpEx();
                Assert.True(finalize.Success);
                Assert.Null(game.State.TrumpSuit);
                Assert.InRange(game.State.DealerIndex, 0, 3);
                return;
            }

            Assert.True(false, "No seed in [1,2000] produced an AI no-trump bid.");
        }

        private static void RunAutoBidding(Game game, int seed)
        {
            var bidPolicy = new BidPolicy(seed + 5003);
            var visibleHands = new[] { new List<Card>(), new List<Card>(), new List<Card>(), new List<Card>() };

            while (!game.IsDealingComplete)
            {
                var dealResult = game.DealNextCardEx();
                if (!dealResult.Success)
                    break;

                var step = game.LastDealStep;
                if (step == null || step.IsBottomCard)
                    continue;

                var player = step.PlayerIndex;
                if (player < 0 || player >= 4)
                    continue;

                visibleHands[player].Add(step.Card);
                var decision = bidPolicy.Decide(new BidPolicy.DecisionContext
                {
                    PlayerIndex = player,
                    DealerIndex = game.State.DealerIndex,
                    LevelRank = game.State.LevelRank,
                    VisibleCards = new List<Card>(visibleHands[player]),
                    RoundIndex = step.PlayerCardCount - 1,
                    CurrentBidPriority = game.CurrentBidPriority,
                    CurrentBidPlayer = game.CurrentBidPlayer
                });

                var bidCards = decision.AttemptCards;
                if (bidCards.Count == 0)
                    continue;

                var detail = decision.ToLogDetail();
                var bidResult = game.BidTrumpEx(player, bidCards, detail);
                if (!bidResult.Success && bidCards.Count > 1)
                {
                    var single = new List<Card> { bidCards[0] };
                    if (game.CanBidTrumpEx(player, single).Success)
                        game.BidTrumpEx(player, single, detail);
                }
            }
        }
    }
}
