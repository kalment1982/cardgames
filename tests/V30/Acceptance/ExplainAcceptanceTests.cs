using TractorGame.Tests.V30.Specs;
using TractorGame.Core.AI.V30.Explain;
using Xunit;

namespace TractorGame.Tests.V30.Acceptance
{
    public class ExplainAcceptanceTests
    {
        [Fact]
        public void Explain_RequiredFieldsCatalog_ShouldMatchFrozenExpectation()
        {
            Assert.Contains("phase", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("primary_intent", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("candidate_summary", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("known_facts", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("estimated_facts", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("bottom_mode", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("generated_at_utc", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Contains("log_context", V30TestMatrixCatalog.RequiredExplainFields);
            Assert.Equal(15, V30TestMatrixCatalog.RequiredExplainFields.Count);
        }

        [Fact]
        public void Explain_Output_ShouldContainAllRequiredFields()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "ProtectBottom",
                TriggeredRules = new[] { "Lead-005", "Mate-001" },
                CandidateSummary = new[]
                {
                    new DecisionCandidateV30
                    {
                        Action = new System.Collections.Generic.List<string> { "S_A" },
                        Score = 1.0,
                        ReasonCode = "stable_win"
                    }
                },
                RejectedReasons = new[] { "blind_handoff" },
                SelectedAction = new[] { "S_A" },
                SelectedReason = "stable_win",
                KnownFacts = new System.Collections.Generic.Dictionary<string, string> { ["partner_winning"] = "true" },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "east_trump_count",
                        Value = "5±2",
                        Confidence = 0.72,
                        Evidence = "memory"
                    }
                },
                WinSecurity = "stable_win",
                BottomMode = "protect_bottom"
            }, new AIDecisionLogContextV30 { TraceId = "t_1", RoundId = "r_1" });

            var json = builder.Serialize(bundle, indented: true);

            foreach (var field in V30TestMatrixCatalog.RequiredExplainFields)
            {
                Assert.Contains($"\"{field}\"", json);
            }
        }

        [Fact]
        public void Explain_Output_ShouldRejectUnknownAliasFields()
        {
            var builder = new DecisionBundleBuilderV30();
            var json = builder.Serialize(builder.Build(new DecisionExplainInputV30
            {
                Phase = "Follow",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "PreserveStructure"
            }));

            Assert.DoesNotContain("\"Phase\"", json);
            Assert.DoesNotContain("\"PrimaryIntent\"", json);
            Assert.DoesNotContain("\"SecondaryIntent\"", json);
        }

        [Fact]
        public void Explain_Output_ShouldSplitKnownFactsAndEstimatedFacts()
        {
            var builder = new DecisionBundleBuilderV30();
            var output = builder.Build(new DecisionExplainInputV30
            {
                Phase = "Follow",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "PassToMate",
                KnownFacts = new System.Collections.Generic.Dictionary<string, string>
                {
                    ["partner_winning"] = "true"
                },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "west_trump_count",
                        Value = "4±1",
                        Confidence = 0.8,
                        Evidence = "inference"
                    }
                }
            });

            Assert.True(output.KnownFacts.ContainsKey("partner_winning"));
            Assert.Single(output.EstimatedFacts);
            Assert.Equal("west_trump_count", output.EstimatedFacts[0].Key);
            Assert.False(output.KnownFacts.ContainsKey("west_trump_count"));
        }
    }
}
