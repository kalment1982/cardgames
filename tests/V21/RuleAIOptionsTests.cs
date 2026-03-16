using TractorGame.Core.AI.V21;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class RuleAIOptionsTests
    {
        [Fact]
        public void FromEnvironment_ParsesBooleanAndRate()
        {
            const string useName = "TRACTOR_RULE_AI_V21_USE_NEW_PATH";
            const string shadowName = "TRACTOR_RULE_AI_V21_SHADOW_MODE";
            const string rateName = "TRACTOR_RULE_AI_V21_SHADOW_RATE";
            const string traceName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_ENABLED";
            const string truthName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_INCLUDE_TRUTH";
            const string candidateLimitName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_MAX_CANDIDATES";

            var oldUse = System.Environment.GetEnvironmentVariable(useName);
            var oldShadow = System.Environment.GetEnvironmentVariable(shadowName);
            var oldRate = System.Environment.GetEnvironmentVariable(rateName);
            var oldTrace = System.Environment.GetEnvironmentVariable(traceName);
            var oldTruth = System.Environment.GetEnvironmentVariable(truthName);
            var oldCandidateLimit = System.Environment.GetEnvironmentVariable(candidateLimitName);
            try
            {
                System.Environment.SetEnvironmentVariable(useName, "true");
                System.Environment.SetEnvironmentVariable(shadowName, "false");
                System.Environment.SetEnvironmentVariable(rateName, "0.25");
                System.Environment.SetEnvironmentVariable(traceName, "true");
                System.Environment.SetEnvironmentVariable(truthName, "false");
                System.Environment.SetEnvironmentVariable(candidateLimitName, "12");

                var options = RuleAIOptions.FromEnvironment();

                Assert.True(options.UseRuleAIV21);
                Assert.False(options.EnableShadowCompare);
                Assert.Equal(0.25, options.ShadowSampleRate);
                Assert.True(options.DecisionTraceEnabled);
                Assert.False(options.DecisionTraceIncludeTruthSnapshot);
                Assert.Equal(12, options.DecisionTraceMaxCandidates);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(useName, oldUse);
                System.Environment.SetEnvironmentVariable(shadowName, oldShadow);
                System.Environment.SetEnvironmentVariable(rateName, oldRate);
                System.Environment.SetEnvironmentVariable(traceName, oldTrace);
                System.Environment.SetEnvironmentVariable(truthName, oldTruth);
                System.Environment.SetEnvironmentVariable(candidateLimitName, oldCandidateLimit);
            }
        }

        [Fact]
        public void FromEnvironment_DefaultsToV21WhenUnset()
        {
            const string useName = "TRACTOR_RULE_AI_V21_USE_NEW_PATH";
            const string shadowName = "TRACTOR_RULE_AI_V21_SHADOW_MODE";
            const string rateName = "TRACTOR_RULE_AI_V21_SHADOW_RATE";
            const string traceName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_ENABLED";
            const string truthName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_INCLUDE_TRUTH";
            const string candidateLimitName = "TRACTOR_RULE_AI_V21_DECISION_TRACE_MAX_CANDIDATES";

            var oldUse = System.Environment.GetEnvironmentVariable(useName);
            var oldShadow = System.Environment.GetEnvironmentVariable(shadowName);
            var oldRate = System.Environment.GetEnvironmentVariable(rateName);
            var oldTrace = System.Environment.GetEnvironmentVariable(traceName);
            var oldTruth = System.Environment.GetEnvironmentVariable(truthName);
            var oldCandidateLimit = System.Environment.GetEnvironmentVariable(candidateLimitName);
            try
            {
                System.Environment.SetEnvironmentVariable(useName, null);
                System.Environment.SetEnvironmentVariable(shadowName, null);
                System.Environment.SetEnvironmentVariable(rateName, null);
                System.Environment.SetEnvironmentVariable(traceName, null);
                System.Environment.SetEnvironmentVariable(truthName, null);
                System.Environment.SetEnvironmentVariable(candidateLimitName, null);

                var options = RuleAIOptions.FromEnvironment();

                Assert.True(options.UseRuleAIV21);
                Assert.True(options.EnableShadowCompare);
                Assert.Equal(1.0, options.ShadowSampleRate);
                Assert.True(options.DecisionTraceEnabled);
                Assert.True(options.DecisionTraceIncludeTruthSnapshot);
                Assert.Equal(0, options.DecisionTraceMaxCandidates);
            }
            finally
            {
                System.Environment.SetEnvironmentVariable(useName, oldUse);
                System.Environment.SetEnvironmentVariable(shadowName, oldShadow);
                System.Environment.SetEnvironmentVariable(rateName, oldRate);
                System.Environment.SetEnvironmentVariable(traceName, oldTrace);
                System.Environment.SetEnvironmentVariable(truthName, oldTruth);
                System.Environment.SetEnvironmentVariable(candidateLimitName, oldCandidateLimit);
            }
        }

        [Fact]
        public void Create_OverridesFallbackAndClampsRate()
        {
            var fallback = new RuleAIOptions
            {
                UseRuleAIV21 = true,
                EnableShadowCompare = true,
                ShadowSampleRate = 0.6,
                DecisionTraceEnabled = false,
                DecisionTraceIncludeTruthSnapshot = false,
                DecisionTraceMaxCandidates = 4
            };

            var options = RuleAIOptions.Create(
                useRuleAIV21: false,
                enableShadowCompare: false,
                shadowSampleRate: 2.5,
                decisionTraceEnabled: true,
                decisionTraceIncludeTruthSnapshot: true,
                decisionTraceMaxCandidates: -9,
                fallback: fallback);

            Assert.False(options.UseRuleAIV21);
            Assert.False(options.EnableShadowCompare);
            Assert.Equal(1.0, options.ShadowSampleRate);
            Assert.True(options.DecisionTraceEnabled);
            Assert.True(options.DecisionTraceIncludeTruthSnapshot);
            Assert.Equal(0, options.DecisionTraceMaxCandidates);
        }
    }
}
