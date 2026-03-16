using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class FollowPolicy2Tests
    {
        [Fact]
        public void Decide_WhenPartnerWinning_PreservesPartnerLead()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Ten)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 20);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("PassToMate", decision.Explanation.PrimaryIntent);
            Assert.Single(decision.SelectedCards);
            Assert.Contains(decision.SelectedReason, new[] { "pass_to_mate_send_points", "pass_to_mate_keep_power" });
        }

        [Fact]
        public void Decide_WhenPartnerWinningAndCanSendPoints_PrefersLowPointSendWithoutBurningAce()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Two)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.King)
                },
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.King)
                },
                AIRole.DealerPartner,
                partnerWinning: true,
                trickScore: 20,
                playerIndex: 2,
                dealerIndex: 0);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("PassToMate", decision.Explanation.PrimaryIntent);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Suit == Suit.Spade && card.Rank == Rank.Ace);
            Assert.Contains(decision.SelectedCards, card => card.Suit == Suit.Spade && card.Rank == Rank.Ten);
            Assert.Equal(
                new[] { "♠10", "♠6", "♠7" }.OrderBy(text => text),
                decision.SelectedCards.Select(card => card.ToString()).OrderBy(text => text));
            Assert.Equal("pass_to_mate_send_points", decision.SelectedReason);
        }

        [Fact]
        public void Decide_WhenHighScoreTrickCanOvercut_DoesNotSloughOffsuit()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Spade, Rank.Two)
            };
            var lead = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Five)
            };
            var currentWinning = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Ace)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                lead,
                currentWinning,
                AIRole.DealerPartner,
                partnerWinning: false,
                trickScore: 15,
                cardsLeftMin: 7,
                playerIndex: 2,
                dealerIndex: 0);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("TakeScore", decision.Explanation.PrimaryIntent);
            Assert.True(RuleAIUtility.CanBeatCards(config, currentWinning, decision.SelectedCards));
            Assert.All(decision.SelectedCards, card => Assert.True(config.IsTrump(card)));
            Assert.NotEqual(
                "♥9,♥J",
                string.Join(",", decision.SelectedCards.Select(card => card.ToString()).OrderBy(text => text)));
        }
    }
}
