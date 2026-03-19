using System.Collections.Generic;
using TractorGame.Core.AI.V30.Explain;
using Xunit;

namespace TractorGame.Tests.V30.Explain
{
    public class DecisionBundleBuilderV30Tests
    {
        [Fact]
        public void Serialize_ContainsRequiredSnakeCaseFields()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(
                BuildInput(),
                new AIDecisionLogContextV30
                {
                    TraceId = "trace_001",
                    RoundId = "round_001",
                    TrickIndex = 3,
                    TurnIndex = 1,
                    PlayerIndex = 0,
                    SeatTag = "North"
                });

            var json = builder.Serialize(bundle, indented: true);

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
            Assert.Contains("\"log_context\"", json);
            Assert.DoesNotContain("\"Phase\"", json);
            Assert.DoesNotContain("\"PrimaryIntent\"", json);
        }

        [Fact]
        public void SerializeAndDeserialize_RoundTripsCoreFields()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(BuildInput());

            var json = builder.Serialize(bundle);
            var restored = builder.Deserialize(json);

            Assert.Equal("Lead", restored.Phase);
            Assert.Equal("TakeScore", restored.PrimaryIntent);
            Assert.Equal("PassToMate", restored.SecondaryIntent);
            Assert.Equal("stable_win", restored.WinSecurity);
            Assert.Equal("protect_bottom", restored.BottomMode);
            Assert.Single(restored.CandidateSummary);
            Assert.Single(restored.EstimatedFacts);
        }

        private static DecisionExplainInputV30 BuildInput()
        {
            return new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "PassToMate",
                TriggeredRules = new[] { "Lead-005", "Mate-001" },
                CandidateSummary = new[]
                {
                    new DecisionCandidateV30
                    {
                        Action = new List<string> { "♠A", "♠K" },
                        Score = 12.8,
                        ReasonCode = "stable_win_low_cost",
                        Features = new Dictionary<string, double> { ["WinSecurityValue"] = 1.0 }
                    }
                },
                RejectedReasons = new[] { "unsafe_throw", "blind_handoff" },
                SelectedAction = new[] { "♠A", "♠K" },
                SelectedReason = "stable_win_low_cost",
                KnownFacts = new Dictionary<string, string> { ["partner_void_spade"] = "true" },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "east_trump_count",
                        Value = "5±2",
                        Confidence = 0.72,
                        Evidence = "memory_snapshot"
                    }
                },
                WinSecurity = "stable_win",
                BottomMode = "protect_bottom"
            };
        }
    }
}
