using System.Collections.Generic;
using TractorGame.Core.AI.V30.Explain;
using Xunit;

namespace TractorGame.Tests.V30.Explain
{
    public class DecisionExplainerV30Tests
    {
        [Fact]
        public void Build_SeparatesKnownFactsAndEstimatedFacts()
        {
            var explainer = new DecisionExplainerV30();
            var bundle = explainer.Build(new DecisionExplainInputV30
            {
                Phase = "Follow",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "PreserveStructure",
                KnownFacts = new Dictionary<string, string>
                {
                    ["partner_is_winning"] = "true",
                    ["lead_suit"] = "spade"
                },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "west_trump_count",
                        Value = "4±1",
                        Confidence = 0.83,
                        Evidence = "inference_snapshot"
                    }
                },
                WinSecurity = "fragile_win",
                BottomMode = "normal"
            });

            Assert.Equal("true", bundle.KnownFacts["partner_is_winning"]);
            Assert.Single(bundle.EstimatedFacts);
            Assert.Equal("west_trump_count", bundle.EstimatedFacts[0].Key);
            Assert.Equal(0.83, bundle.EstimatedFacts[0].Confidence, 3);
            Assert.False(bundle.KnownFacts.ContainsKey("west_trump_count"));
        }

        [Fact]
        public void Build_FallbacksToTopCandidateWhenSelectedActionIsMissing()
        {
            var explainer = new DecisionExplainerV30();
            var bundle = explainer.Build(new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = "TakeLead",
                SecondaryIntent = "PreserveStructure",
                CandidateSummary = new[]
                {
                    new DecisionCandidateV30
                    {
                        Action = new List<string> { "S_A" },
                        Score = 3.5,
                        ReasonCode = "safe_high_card"
                    }
                },
                SelectedAction = null,
                SelectedReason = ""
            });

            Assert.Single(bundle.SelectedAction);
            Assert.Equal("S_A", bundle.SelectedAction[0]);
            Assert.Equal("safe_high_card", bundle.SelectedReason);
        }
    }
}
