using System.Collections.Generic;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class DecisionExplainerTests
    {
        [Fact]
        public void Build_FormatsTopCandidatesAndFeatures()
        {
            var context = new RuleAIContext { Phase = PhaseKind.Follow };
            var intent = new ResolvedIntent
            {
                PrimaryIntent = DecisionIntentKind.TakeScore,
                SecondaryIntent = DecisionIntentKind.PreserveStructure,
                RiskFlags = new List<string> { "critical_score_pressure" }
            };
            var scored = new List<ScoredAction>
            {
                new ScoredAction
                {
                    Cards = new List<Card> { new Card(Suit.Joker, Rank.BigJoker) },
                    Score = 9.5,
                    ReasonCode = "cheap_overtake_with_acceptable_structure_loss",
                    Features = new Dictionary<string, double> { ["TrickWinValue"] = 2.5 }
                }
            };

            var explanation = new DecisionExplainer().Build(context, intent, scored, "FollowPolicy2");

            Assert.Equal("FollowPolicy2", explanation.PhasePolicy);
            Assert.Equal("TakeScore", explanation.PrimaryIntent);
            Assert.Equal("PreserveStructure", explanation.SecondaryIntent);
            Assert.Contains("critical_score_pressure", explanation.RiskFlags);
            Assert.Single(explanation.TopCandidates);
            Assert.True(explanation.SelectedActionFeatures.ContainsKey("TrickWinValue"));
        }
    }
}
