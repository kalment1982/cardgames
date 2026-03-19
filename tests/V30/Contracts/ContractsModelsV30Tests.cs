using System.Text.Json;
using TractorGame.Core.AI.V30.Contracts;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Contracts
{
    public class ContractsModelsV30Tests
    {
        [Fact]
        public void FeatureFlags_DefaultsMatchFrozenBaseline()
        {
            var flags = V30FeatureFlags.Default;

            Assert.True(flags.StrictContractValidation);
            Assert.Equal(0.70, flags.ProbabilityThreshold);
            Assert.Equal(10, flags.DefaultBottomEstimatePoints);
            Assert.True(flags.EnableBottomSignalBoost);
        }

        [Fact]
        public void DecisionExplanationV30_SerializesStableFieldNames()
        {
            var model = new DecisionExplanationV30
            {
                Phase = "lead",
                PrimaryIntent = "TakeLead",
                SecondaryIntent = "PreserveStructure",
                CandidateCount = 3
            };

            string json = JsonSerializer.Serialize(model);
            Assert.Contains("\"phase\"", json);
            Assert.Contains("\"primary_intent\"", json);
            Assert.Contains("\"secondary_intent\"", json);
            Assert.Contains("\"triggered_rules\"", json);
            Assert.Contains("\"candidate_count\"", json);
            Assert.Contains("\"candidate_summary\"", json);
            Assert.Contains("\"rejected_reasons\"", json);
            Assert.Contains("\"selected_action\"", json);
            Assert.Contains("\"selected_reason\"", json);
            Assert.Contains("\"known_facts\"", json);
            Assert.Contains("\"estimated_facts\"", json);
            Assert.Contains("\"win_security\"", json);
            Assert.Contains("\"bottom_mode\"", json);
            Assert.Contains("\"generated_at_utc\"", json);
            Assert.Contains("\"log_context\"", json);
        }

        [Fact]
        public void PhaseDecisionV30_ExposesSelectedReasonFromExplanation()
        {
            var decision = new PhaseDecisionV30
            {
                Phase = PhaseKindV30.Lead,
                SelectedCards = { new Card(Suit.Heart, Rank.Ace) },
                Intent = new ResolvedIntentV30
                {
                    PrimaryIntent = DecisionIntentKindV30.TakeLead
                },
                Explanation = new DecisionExplanationV30
                {
                    SelectedReason = "best_stable_value"
                }
            };

            Assert.Equal("best_stable_value", decision.SelectedReason);
            Assert.Single(decision.SelectedCards);
        }
    }
}

