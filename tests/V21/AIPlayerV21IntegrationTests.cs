using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class AIPlayerV21IntegrationTests
    {
        [Fact]
        public void Lead_UseRuleAIV21_EmitsLeadPolicyDecisionLog()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var sink = new InMemoryLogSink();
            var ai = new AIPlayer(
                config,
                AIDifficulty.Hard,
                seed: 5,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Hard),
                decisionLogger: new CoreLogger(sink),
                ruleAIOptions: new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false });

            var result = ai.Lead(new List<Card>
            {
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Seven)
            });

            Assert.Equal(6, result.Count);
            var decisionEntry = Assert.Single(sink.Entries.Where(entry => entry.Event == "ai.decision"));
            var bundleEntry = Assert.Single(sink.Entries.Where(entry => entry.Event == "ai.bundle"));

            Assert.Equal("LeadPolicy2", decisionEntry.Payload["phase_policy"]);
            Assert.Equal(decisionEntry.Payload["decision_trace_id"], bundleEntry.Payload["decision_trace_id"]);
            Assert.Equal(decisionEntry.CorrelationId, bundleEntry.CorrelationId);

            var bundlePayload = Assert.IsType<Dictionary<string, object?>>(bundleEntry.Payload["bundle"]);
            Assert.True(bundlePayload.ContainsKey("context_snapshot"));
            Assert.True(bundlePayload.ContainsKey("candidate_details"));
        }

        [Fact]
        public void BuryBottom_UseRuleAIV21_ReturnsEightCards()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Spade };
            var ai = new AIPlayer(
                config,
                AIDifficulty.Expert,
                seed: 9,
                strategyParameters: AIStrategyParameters.CreatePreset(AIDifficulty.Expert),
                decisionLogger: new CoreLogger(new InMemoryLogSink()),
                ruleAIOptions: new RuleAIOptions { UseRuleAIV21 = true, EnableShadowCompare = false });

            var hand = new List<Card>();
            for (int i = 0; i < 25; i++)
                hand.Add(new Card(Suit.Heart, Rank.Three));
            for (int i = 0; i < 8; i++)
                hand.Add(new Card(Suit.Spade, Rank.Five));

            var result = ai.BuryBottom(hand, AIRole.Dealer, new List<Card> { new Card(Suit.Club, Rank.Ten) });

            Assert.Equal(8, result.Count);
        }
    }
}
