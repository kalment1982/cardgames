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
