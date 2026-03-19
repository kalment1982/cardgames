using System;
using System.Collections.Generic;
using System.IO;
using TractorGame.Core.AI.V30.Explain;
using Xunit;

namespace TractorGame.Tests.V30.Fixtures
{
    public class DecisionBundleFixtureV30Tests
    {
        [Fact]
        public void WriteFixture_CreatesBundleFileAndCanReadBack()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(new DecisionExplainInputV30
            {
                Phase = "Lead",
                PrimaryIntent = "TakeScore",
                SecondaryIntent = "PassToMate",
                TriggeredRules = new[] { "Lead-006" },
                CandidateSummary = new[]
                {
                    new DecisionCandidateV30
                    {
                        Action = new List<string> { "H_A", "H_K" },
                        Score = 6.4,
                        ReasonCode = "stable_run_score"
                    }
                },
                SelectedAction = new[] { "H_A", "H_K" },
                SelectedReason = "stable_run_score",
                KnownFacts = new Dictionary<string, string> { ["partner_void_heart"] = "false" },
                EstimatedFacts = new[]
                {
                    new EstimatedFactV30
                    {
                        Key = "east_heart_count",
                        Value = "2±1",
                        Confidence = 0.64,
                        Evidence = "memory_snapshot"
                    }
                },
                WinSecurity = "stable_win",
                BottomMode = "normal",
                GeneratedAtUtc = DateTimeOffset.Parse("2026-03-19T00:00:00+00:00")
            });

            var fixtureDir = Path.Combine(Path.GetTempPath(), "tractor_v30_fixture_tests", Guid.NewGuid().ToString("N"));
            var filePath = builder.WriteFixture(fixtureDir, "lead_fixture", bundle);
            var json = File.ReadAllText(filePath);
            var restored = builder.Deserialize(json);

            Assert.True(File.Exists(filePath));
            Assert.Equal("Lead", restored.Phase);
            Assert.Equal("TakeScore", restored.PrimaryIntent);
            Assert.Equal("stable_run_score", restored.SelectedReason);
            Assert.Equal("2026-03-19T00:00:00.0000000+00:00", restored.GeneratedAtUtc);
        }

        [Fact]
        public void WriteFixture_RespectsOverwriteFlag()
        {
            var builder = new DecisionBundleBuilderV30();
            var bundle = builder.Build(new DecisionExplainInputV30
            {
                Phase = "Follow",
                PrimaryIntent = "TakeLead",
                SecondaryIntent = "PreserveStructure",
                SelectedAction = new[] { "C_9" },
                SelectedReason = "cheap_win",
                WinSecurity = "fragile_win",
                BottomMode = "contest_bottom"
            });

            var fixtureDir = Path.Combine(Path.GetTempPath(), "tractor_v30_fixture_tests", Guid.NewGuid().ToString("N"));
            var filePath = builder.WriteFixture(fixtureDir, "follow_fixture", bundle, overwrite: true);

            Assert.True(File.Exists(filePath));
            Assert.Throws<IOException>(() => builder.WriteFixture(fixtureDir, "follow_fixture", bundle, overwrite: false));
        }
    }
}
