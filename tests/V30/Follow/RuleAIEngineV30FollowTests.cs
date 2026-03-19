using System.Collections.Generic;
using TractorGame.Core.AI.V30;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Follow
{
    public class RuleAIEngineV30FollowTests
    {
        [Fact]
        public void DecideFollow_DelegatesToFollowPolicyOverlay()
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
                trickScore: 0);

            var engine = new RuleAIEngineV30(config, TractorGame.Core.AI.AIDifficulty.Hard, new TractorGame.Core.AI.CardMemory(config));
            var decision = engine.DecideFollow(context, new List<List<Card>>
            {
                new List<Card> { new Card(Suit.Spade, Rank.Ace) },
                new List<Card> { new Card(Suit.Spade, Rank.Three) }
            });

            Assert.Single(decision.SelectedCards);
            Assert.Equal(Rank.Three, decision.SelectedCards[0].Rank);
            Assert.Contains("pass_to_mate", decision.SelectedReason);
        }
    }
}
