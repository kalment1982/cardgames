using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V30.Follow;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Follow
{
    public class FollowPolicyV30Tests
    {
        [Fact]
        public void Decide_UsesExplicitLegalCandidatesFirst()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Three)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: true,
                trickScore: 0,
                legalActions: new List<List<Card>>
                {
                    new List<Card> { new Card(Suit.Spade, Rank.Ace) }
                });

            var explicitCandidates = new List<List<Card>>
            {
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            };

            var policy = new FollowPolicyV30();
            var decision = policy.Decide(context, explicitCandidates);

            Assert.Equal(Rank.Three, decision.SelectedCards[0].Rank);
            Assert.Single(decision.RankedCandidates);
        }

        [Fact]
        public void Decide_FallsBackToContextLegalActions_WhenNoExplicitInput()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Three)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: true,
                trickScore: 0,
                legalActions: new List<List<Card>>
                {
                    new List<Card> { new Card(Suit.Spade, Rank.Three) }
                });

            var policy = new FollowPolicyV30();
            var decision = policy.Decide(context);

            Assert.Equal(Rank.Three, decision.SelectedCards[0].Rank);
            Assert.NotEmpty(decision.RankedCandidates);
        }

        [Fact]
        public void Decide_UsesV21Generator_WhenNoLegalCandidatesProvided()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.Three),
                    new Card(Suit.Club, Rank.King),
                    new Card(Suit.Heart, Rank.Three)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.Jack) },
                partnerWinning: false,
                trickScore: 10);

            var policy = new FollowPolicyV30();
            var decision = policy.Decide(context);

            Assert.NotEmpty(decision.RankedCandidates);
            Assert.True(decision.SelectedCards.Count == 1);
        }

        [Fact]
        public void Decide_ReturnsMinimizeLossWhenCandidateListEmpty()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                new List<Card> { new Card(Suit.Spade, Rank.Three) },
                new List<Card> { new Card(Suit.Spade, Rank.Nine) },
                new List<Card> { new Card(Suit.Spade, Rank.King) },
                partnerWinning: false,
                trickScore: 0);

            var policy = new FollowPolicyV30();
            var decision = policy.Decide(context, new List<List<Card>>());

            Assert.Equal(FollowOverlayIntentV30.MinimizeLoss, decision.Intent);
            Assert.Empty(decision.SelectedCards);
            Assert.Empty(decision.RankedCandidates);
        }

        [Fact]
        public void Decide_ExpandsContextLegalActions_ForLowerPointMinimizeLossCombo()
        {
            var config = FollowOverlayTestHelper.CreateConfig();
            var context = FollowOverlayTestHelper.BuildFollowContext(
                config,
                new List<Card>
                {
                    new Card(Suit.Club, Rank.Five),
                    new Card(Suit.Club, Rank.Nine),
                    new Card(Suit.Club, Rank.Queen),
                    new Card(Suit.Club, Rank.Two)
                },
                new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Club, Rank.King)
                },
                new List<Card>
                {
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Club, Rank.Ace),
                    new Card(Suit.Club, Rank.King)
                },
                partnerWinning: false,
                trickScore: 25,
                legalActions: new List<List<Card>>
                {
                    new List<Card>
                    {
                        new Card(Suit.Club, Rank.Five),
                        new Card(Suit.Club, Rank.Nine),
                        new Card(Suit.Club, Rank.Queen)
                    }
                });

            var policy = new FollowPolicyV30();
            var decision = policy.Decide(context);

            Assert.Equal(3, decision.SelectedCards.Count);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Rank == Rank.Five);
            Assert.Contains(decision.SelectedCards, card => card.Rank == Rank.Two);
            Assert.Equal(FollowOverlayIntentV30.MinimizeLoss, decision.Intent);
        }
    }
}
