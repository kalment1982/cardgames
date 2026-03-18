using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class LeadPolicy2Tests
    {
        [Fact]
        public void Decide_SelectsTractorWhenStrongStructuredLeadExists()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Nine),
                    new Card(Suit.Spade, Rank.Nine),
                    new Card(Suit.Spade, Rank.Eight),
                    new Card(Suit.Spade, Rank.Eight),
                    new Card(Suit.Spade, Rank.Seven),
                    new Card(Suit.Spade, Rank.Seven),
                    new Card(Suit.Heart, Rank.Ace)
                },
                AIRole.Opponent);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(6, decision.SelectedCards.Count);
            Assert.Equal("LeadPolicy2", decision.Explanation.PhasePolicy);
        }

        [Fact]
        public void Decide_SelectsSafeThrowToCloseOut_AtProtectBottomEndgame()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var memory = new CardMemory(config);
            RecordPlayedCards(memory, BuildPlayedSpadesForSafeEndgame(config));
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Four)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.DealerPartner,
                playerIndex: 2,
                dealerIndex: 0,
                cardsLeftMin: 3,
                defenderScore: 80,
                bottomPoints: 20);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(3, decision.SelectedCards.Count);
            Assert.All(decision.SelectedCards, card => Assert.Equal(Suit.Spade, card.Suit));
            Assert.Equal("endgame_safe_throw_closeout", decision.Explanation.SelectedReason);
        }

        [Fact]
        public void Decide_WhenDealerOpensWithStrongSidePair_DoesNotLeadLowTrumpProbe()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.King)
            };
            var visibleBottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 2,
                dealerIndex: 2,
                visibleBottomCards: visibleBottom,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: 25);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.SelectedCards.Count);
            Assert.All(decision.SelectedCards, card => Assert.Equal(Suit.Club, card.Suit));
            Assert.All(decision.SelectedCards, card => Assert.Equal(Rank.Ace, card.Rank));
        }

        [Fact]
        public void Decide_WhenDealerSecondTrickStillHasStrongSidePair_AvoidsLowTrumpProbe()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.King)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 2,
                dealerIndex: 2,
                trickIndex: 2,
                turnIndex: 5,
                playPosition: 1,
                cardsLeftMin: 24);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.SelectedCards.Count);
            Assert.All(decision.SelectedCards, card => Assert.Equal(Suit.Club, card.Suit));
            Assert.All(decision.SelectedCards, card => Assert.Equal(Rank.Ace, card.Rank));
        }

        [Fact]
        public void Decide_WhenDealerHasSupportedHighSideSingle_PrefersSideSuitRunOverLowTrumpProbe()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Spade, Rank.Three)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 2,
                dealerIndex: 2,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: 13);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Club, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.Ace, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenDealerEarlyAndSideSuitStrong_DoesNotForceTrump()
        {
            var config = new GameConfig { LevelRank = Rank.Four, TrumpSuit = Suit.Diamond };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Four)
            };
            var visibleBottom = new List<Card>
            {
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Spade, Rank.Nine)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 1,
                dealerIndex: 1,
                visibleBottomCards: visibleBottom,
                trickIndex: 2,
                turnIndex: 5,
                playPosition: 1,
                cardsLeftMin: 21);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.NotEqual(Suit.Diamond, decision.SelectedCards[0].Suit);
        }

        [Fact]
        public void Decide_WhenOpponentHasStrongSideSuit_DoesNotForceTrump()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Jack)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 2,
                dealerIndex: 0,
                trickIndex: 8,
                turnIndex: 29,
                playPosition: 1,
                cardsLeftMin: 14,
                defenderScore: 10,
                bottomPoints: 5);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.NotEqual(Suit.Diamond, decision.SelectedCards[0].Suit);
        }

        [Fact]
        public void Decide_WhenOnlyTwoJokersLeft_LeadsSmallJokerFirst()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Joker, Rank.SmallJoker)
            };
            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 1,
                dealerIndex: 0,
                cardsLeftMin: 2,
                trickIndex: 20,
                turnIndex: 77,
                playPosition: 1);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Joker, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.SmallJoker, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenKnownTopSideSuitQueenExists_PrefersQueenOverPointCard()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var memory = new CardMemory(config);
            RecordPlayedCards(memory, new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.King)
            });

            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Nine)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 8,
                turnIndex: 29,
                playPosition: 1,
                cardsLeftMin: 13);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Club, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.Queen, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenSideSuitKingAndTenCompete_PrefersKingLead()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Club, Rank.Eight)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 6,
                turnIndex: 21,
                playPosition: 1,
                cardsLeftMin: 5);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Spade, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.King, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenDealerHasReliableAceSideSuit_PrefersAceLead()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Diamond, Rank.Jack),
                new Card(Suit.Spade, Rank.Seven)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 2,
                dealerIndex: 2,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: 7);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Club, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.Ace, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenEarlyLeadHasAces_DoesNotPreferLowSidePairPressure()
        {
            var config = new GameConfig { LevelRank = Rank.Three, TrumpSuit = Suit.Heart };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Club, Rank.Ten),
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Club, Rank.Five),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.Jack),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Club, Rank.Six)
            };
            var visibleBottom = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Diamond, Rank.Seven),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 2,
                dealerIndex: 2,
                visibleBottomCards: visibleBottom,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: 25);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Rank.Ace, decision.SelectedCards[0].Rank);
        }

        [Fact]
        public void Decide_WhenStrongHighSidePairExists_PrefersItOverSmallPair()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Club, Rank.Three),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Diamond, Rank.Four),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Club, Rank.Six)
            };
            var visibleBottom = new List<Card>
            {
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Heart, Rank.Jack)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Dealer,
                playerIndex: 3,
                dealerIndex: 3,
                visibleBottomCards: visibleBottom,
                trickIndex: 1,
                turnIndex: 1,
                playPosition: 1,
                cardsLeftMin: 25);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.SelectedCards.Count);
            Assert.All(decision.SelectedCards, card => Assert.Equal(Suit.Heart, card.Suit));
            Assert.All(decision.SelectedCards, card => Assert.Equal(Rank.Queen, card.Rank));
        }

        [Fact]
        public void Decide_WhenOnlyTrumpPairRemainsAndOpponentsVoidTrump_LeadsPairToCloseOut()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            MarkAllOpponentsVoidTrump(memory, config);

            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Four)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 3,
                dealerIndex: 0,
                trickIndex: 20,
                turnIndex: 77,
                playPosition: 1,
                cardsLeftMin: 2,
                defenderScore: 70,
                bottomPoints: 10);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(2, decision.SelectedCards.Count);
            Assert.All(decision.SelectedCards, card => Assert.Equal(Suit.Club, card.Suit));
            Assert.All(decision.SelectedCards, card => Assert.Equal(Rank.Four, card.Rank));
        }

        [Fact]
        public void Decide_WhenNextOpponentKnownVoidInSuit_DoesNotLeadHighPointSideSuitIntoCut()
        {
            var config = new GameConfig { LevelRank = Rank.Three, TrumpSuit = Suit.Club };
            var memory = new CardMemory(config);
            RecordPlayedCards(memory, BuildPlayedCardsForImmediateVoidCutRegression());
            RecordHeartVoidBySouth(memory);

            var hand = new List<Card>
            {
                new Card(Suit.Club, Rank.King),
                new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Club, Rank.Nine),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Club, Rank.Queen),
                new Card(Suit.Club, Rank.Four),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Diamond, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Club, Rank.Seven)
            };

            var context = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, memory).BuildLeadContext(
                hand,
                AIRole.Opponent,
                playerIndex: 1,
                dealerIndex: 0,
                trickIndex: 6,
                turnIndex: 21,
                playPosition: 1,
                cardsLeftMin: 13,
                defenderScore: 25,
                bottomPoints: 30);

            var policy = new LeadPolicy2(
                new LeadCandidateGenerator(config, memory),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Suit.Diamond, decision.SelectedCards[0].Suit);
            Assert.Equal(Rank.King, decision.SelectedCards[0].Rank);
        }

        private static void RecordPlayedCards(CardMemory memory, List<Card> cards)
        {
            for (int index = 0; index < cards.Count; index += 4)
            {
                var trick = new List<TrickPlay>();
                var chunk = cards.Skip(index).Take(4).ToList();
                for (int offset = 0; offset < chunk.Count; offset++)
                {
                    trick.Add(new TrickPlay(offset, new List<Card> { chunk[offset] }));
                }

                memory.RecordTrick(trick);
            }
        }

        private static void MarkAllOpponentsVoidTrump(CardMemory memory, GameConfig config)
        {
            memory.RecordTrick(new List<TrickPlay>
            {
                new TrickPlay(3, new List<Card> { new Card(Suit.Club, Rank.Three) }),
                new TrickPlay(0, new List<Card> { new Card(Suit.Heart, Rank.Seven) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Diamond, Rank.Nine) }),
                new TrickPlay(2, new List<Card> { new Card(Suit.Spade, Rank.Jack) })
            });

            Assert.True(config.IsTrump(new Card(Suit.Club, Rank.Three)));
        }

        private static void RecordHeartVoidBySouth(CardMemory memory)
        {
            memory.RecordTrick(new List<TrickPlay>
            {
                new TrickPlay(3, new List<Card> { new Card(Suit.Heart, Rank.Jack) }),
                new TrickPlay(2, new List<Card> { new Card(Suit.Heart, Rank.Three) }),
                new TrickPlay(1, new List<Card> { new Card(Suit.Heart, Rank.Five) }),
                new TrickPlay(0, new List<Card> { new Card(Suit.Club, Rank.Ace) })
            });
        }

        private static List<Card> BuildPlayedCardsForImmediateVoidCutRegression()
        {
            var cards = new List<Card>();

            AddCopies(cards, Suit.Diamond, Rank.Ace, 2);
            AddCopies(cards, Suit.Diamond, Rank.Two, 2);
            AddCopies(cards, Suit.Diamond, Rank.Seven, 2);
            AddCopies(cards, Suit.Diamond, Rank.Ten, 2);
            AddCopies(cards, Suit.Diamond, Rank.King, 1);
            AddCopies(cards, Suit.Diamond, Rank.Six, 1);
            AddCopies(cards, Suit.Diamond, Rank.Eight, 2);
            AddCopies(cards, Suit.Diamond, Rank.Queen, 1);
            AddCopies(cards, Suit.Diamond, Rank.Five, 2);
            AddCopies(cards, Suit.Diamond, Rank.Nine, 1);

            AddCopies(cards, Suit.Spade, Rank.Five, 2);
            AddCopies(cards, Suit.Spade, Rank.Four, 2);
            AddCopies(cards, Suit.Spade, Rank.Eight, 1);
            AddCopies(cards, Suit.Spade, Rank.Queen, 1);
            AddCopies(cards, Suit.Spade, Rank.Ace, 2);
            AddCopies(cards, Suit.Spade, Rank.Two, 1);
            AddCopies(cards, Suit.Spade, Rank.Six, 2);
            AddCopies(cards, Suit.Spade, Rank.Seven, 2);
            AddCopies(cards, Suit.Spade, Rank.Nine, 2);
            AddCopies(cards, Suit.Spade, Rank.Jack, 1);

            AddCopies(cards, Suit.Heart, Rank.Six, 1);
            AddCopies(cards, Suit.Heart, Rank.Seven, 2);
            AddCopies(cards, Suit.Heart, Rank.Ace, 2);
            AddCopies(cards, Suit.Heart, Rank.Queen, 2);
            AddCopies(cards, Suit.Heart, Rank.Ten, 2);
            AddCopies(cards, Suit.Heart, Rank.Two, 2);
            AddCopies(cards, Suit.Heart, Rank.Nine, 1);
            AddCopies(cards, Suit.Heart, Rank.Five, 1);
            AddCopies(cards, Suit.Heart, Rank.Eight, 2);

            AddCopies(cards, Suit.Club, Rank.Two, 1);
            return cards;
        }

        private static void AddCopies(List<Card> cards, Suit suit, Rank rank, int count)
        {
            for (int index = 0; index < count; index++)
                cards.Add(new Card(suit, rank));
        }

        private static List<Card> BuildPlayedSpadesForSafeEndgame(GameConfig config)
        {
            var cards = new List<Card>();
            var ranks = new[]
            {
                Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine,
                Rank.Six, Rank.Five, Rank.Three
            };

            foreach (var rank in ranks)
            {
                cards.Add(new Card(Suit.Spade, rank));
                cards.Add(new Card(Suit.Spade, rank));
            }

            cards.Add(new Card(Suit.Spade, Rank.Eight));
            cards.Add(new Card(Suit.Spade, Rank.Seven));
            cards.Add(new Card(Suit.Spade, Rank.Four));

            Assert.All(cards, card => Assert.False(config.IsTrump(card)));
            return cards;
        }
    }
}
