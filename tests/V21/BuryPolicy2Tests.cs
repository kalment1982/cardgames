using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class BuryPolicy2Tests
    {
        [Fact]
        public void Decide_DealerHighBottomRisk_AvoidsHighPointBury()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var hand = new List<Card>();
            for (int i = 0; i < 25; i++)
                hand.Add(new Card(Suit.Heart, Rank.Three));
            for (int i = 0; i < 4; i++)
            {
                hand.Add(new Card(Suit.Club, Rank.King));
                hand.Add(new Card(Suit.Diamond, Rank.Ten));
            }

            var context = new RuleAIContextBuilder(config, AIDifficulty.Expert, null, new CardMemory(config)).BuildBuryContext(
                hand,
                AIRole.Dealer,
                visibleBottomCards: new List<Card> { new Card(Suit.Club, Rank.King), new Card(Suit.Diamond, Rank.Ten) },
                defenderScore: 72,
                cardsLeftMin: 5);

            var policy = new BuryPolicy2(
                new BuryCandidateGenerator(config),
                new IntentResolver(config),
                new ActionScorer(config),
                new DecisionExplainer());

            var decision = policy.Decide(context);

            Assert.Equal(8, decision.SelectedCards.Count);
            Assert.DoesNotContain(decision.SelectedCards, card => card.Score > 0);
            Assert.Equal("BuryPolicy2", decision.Explanation.PhasePolicy);
        }
    }
}
