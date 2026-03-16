using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class RuleAIProfileTests
    {
        [Fact]
        public void RuleProfile_FromConfig_CopiesFrozenRuleFields()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Five,
                TrumpSuit = Suit.Heart,
                ThrowFailPenalty = 20,
                EnableCounterBottom = true
            };

            var profile = RuleProfile.FromConfig(config);

            Assert.Equal(Suit.Heart, profile.TrumpSuit);
            Assert.Equal(Rank.Five, profile.LevelRank);
            Assert.Equal(20, profile.ThrowFailPenalty);
            Assert.True(profile.EnableCounterBottom);
            Assert.True(profile.StrictFollowStructure);
        }

        [Fact]
        public void DifficultyProfile_From_EasyDisablesDeepInference()
        {
            var profile = DifficultyProfile.From(AIDifficulty.Easy);

            Assert.False(profile.MemoryEnabled);
            Assert.Equal(0, profile.InferenceDepth);
            Assert.False(profile.UseThrowSafetyEstimate);
        }

        [Fact]
        public void StyleProfile_Create_IsStableForSameSeed()
        {
            var left = StyleProfile.Create(99);
            var right = StyleProfile.Create(99);

            Assert.Equal(left.SessionStyleSeed, right.SessionStyleSeed);
            Assert.Equal(left.TieBreakRandomness, right.TieBreakRandomness);
            Assert.Equal(left.EarlyBidLuck, right.EarlyBidLuck);
            Assert.Equal(left.ThrowRiskTolerance, right.ThrowRiskTolerance);
        }

        [Fact]
        public void ModelDefaults_AreInitialized()
        {
            var frame = new DecisionFrame();
            var intent = new ResolvedIntent();
            var decision = new PhaseDecision();

            Assert.Equal(PhaseKind.Unknown, frame.PhaseKind);
            Assert.Equal(DecisionIntentKind.Unknown, intent.PrimaryIntent);
            Assert.NotNull(intent.RiskFlags);
            Assert.NotNull(decision.SelectedCards);
            Assert.NotNull(decision.Explanation);
        }
    }
}
