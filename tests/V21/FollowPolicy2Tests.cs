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
        public void Decide_WhenPartnerWinningAndNoPointDifference_PrefersLowerSloughOverAce()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Heart, Rank.Two)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten)
                },
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Spade, Rank.Ten)
                },
                AIRole.DealerPartner,
                partnerWinning: true,
                trickScore: 20,
                playerIndex: 2,
                dealerIndex: 0,
                trickIndex: 3,
                turnIndex: 11,
                playPosition: 3,
                currentWinningPlayer: 0,
                defenderScore: 0,
                bottomPoints: 10);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("PassToMate", decision.Explanation.PrimaryIntent);
            Assert.Contains(decision.SelectedCards, card => card.Suit == Suit.Spade && card.Rank == Rank.Six);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Suit == Suit.Spade && card.Rank == Rank.Ace);
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

        [Fact]
        public void Decide_WhenMateLeadsLowTrumpAndOpponentBehind_ReinforcesControlWithJoker()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Spade, Rank.Queen)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card> { new Card(Suit.Diamond, Rank.Three) },
                new List<Card> { new Card(Suit.Diamond, Rank.Three) },
                AIRole.DealerPartner,
                partnerWinning: true,
                trickScore: 0,
                playerIndex: 3,
                dealerIndex: 1,
                trickIndex: 2,
                turnIndex: 7,
                playPosition: 3,
                currentWinningPlayer: 1,
                defenderScore: 0,
                bottomPoints: 10);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("TakeLead", decision.Explanation.PrimaryIntent);
            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Joker, decision.SelectedCards[0].Suit);
            Assert.Contains(decision.SelectedReason, new[] { "balanced_select", "cheap_overtake_with_acceptable_structure_loss" });
        }

        [Fact]
        public void Decide_WhenScoringTrumpSingleHasOpponentBehind_UsesCheapestStableWin()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Diamond, Rank.Seven)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card> { new Card(Suit.Club, Rank.Five) },
                new List<Card> { new Card(Suit.Club, Rank.Seven) },
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 5,
                playerIndex: 3,
                dealerIndex: 0,
                trickIndex: 3,
                turnIndex: 11,
                playPosition: 3,
                currentWinningPlayer: 2);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("TakeScore", decision.Explanation.PrimaryIntent);
            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Club, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.King, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenShortageFollow_MinimizeLoss_AvoidsBurningHighSideCards()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Diamond, Rank.Jack),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Five)
            };

            var lead = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.King)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                lead,
                lead,
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 50,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 2,
                turnIndex: 8,
                playPosition: 4,
                currentWinningPlayer: 0);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("MinimizeLoss", decision.Explanation.PrimaryIntent);
            Assert.Contains(decision.SelectedCards, card => card.Suit == Suit.Diamond && card.Rank == Rank.Jack);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Suit == Suit.Heart && card.Rank == Rank.Ace);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Suit == Suit.Heart && card.Rank == Rank.Queen);
        }

        [Fact]
        public void Decide_WhenPartnerWinningButHighScoreIsExposed_ReinforcesWithHighestWinningHeart()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Ace)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                new List<Card> { new Card(Suit.Heart, Rank.Ten) },
                AIRole.Opponent,
                partnerWinning: true,
                trickScore: 10,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 15,
                turnIndex: 59,
                playPosition: 3,
                currentWinningPlayer: 3,
                cardsLeftMin: 8);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("TakeScore", decision.Explanation.PrimaryIntent);
            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Heart, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.Ace, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenPrepareEndgameOnLowScoreTrick_UsesCheapestSecureTrumpWin()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Diamond, Rank.Two)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config)).BuildFollowContext(
                hand,
                new List<Card> { new Card(Suit.Spade, Rank.Five) },
                new List<Card> { new Card(Suit.Spade, Rank.Five) },
                AIRole.Dealer,
                partnerWinning: false,
                trickScore: 5,
                cardsLeftMin: 4,
                playerIndex: 2,
                dealerIndex: 2,
                trickIndex: 15,
                turnIndex: 58,
                playPosition: 2,
                currentWinningPlayer: 3,
                defenderScore: 60,
                bottomPoints: 20);

            var policy = new FollowPolicy2(
                new FollowCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal("PrepareEndgame", decision.Explanation.PrimaryIntent);
            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Diamond, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.Two, decision.SelectedCards[0].Rank);
        }
    }
}
