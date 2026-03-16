using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class BuryCandidateGeneratorTests
    {
        [Fact]
        public void Generate_ProducesEightCardSchemes()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var builder = new RuleAIContextBuilder(config, AIDifficulty.Hard, null, new CardMemory(config));
            var hand = new List<Card>();
            for (int i = 0; i < 25; i++)
                hand.Add(new Card(Suit.Heart, Rank.Three));
            for (int i = 0; i < 8; i++)
                hand.Add(new Card(Suit.Spade, Rank.Five));

            var context = builder.BuildBuryContext(hand, AIRole.Dealer);
            var candidates = new BuryCandidateGenerator(config).Generate(context);

            Assert.NotEmpty(candidates);
            Assert.All(candidates, candidate => Assert.Equal(8, candidate.Count));
            Assert.Contains(candidates, candidate => candidate.Any(card => card.Rank == Rank.Three));
        }
    }
}
