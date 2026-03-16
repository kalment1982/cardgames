using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class ActionScorerTests
    {
        [Fact]
        public void Score_FollowTakeScore_PrefersWinningCandidate()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Club };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config), 11);
            var context = builder.BuildFollowContext(
                new List<Card>
                {
                    new Card(Suit.Joker, Rank.BigJoker),
                    new Card(Suit.Diamond, Rank.Three)
                },
                new List<Card> { new Card(Suit.Spade, Rank.Four) },
                new List<Card> { new Card(Suit.Spade, Rank.Four) },
                AIRole.Opponent,
                partnerWinning: false,
                trickScore: 20);

            var intent = new ResolvedIntent { PrimaryIntent = DecisionIntentKind.TakeScore };
            var scored = new ActionScorer(config).Score(context, intent, new[]
            {
                new List<Card> { new Card(Suit.Joker, Rank.BigJoker) },
                new List<Card> { new Card(Suit.Diamond, Rank.Three) }
            });

            Assert.Equal(Rank.BigJoker, scored[0].Cards[0].Rank);
            Assert.Equal("cheap_overtake_with_acceptable_structure_loss", scored[0].ReasonCode);
        }
    }
}
