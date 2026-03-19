using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Bottom
{
    public class BottomModelsV30Tests
    {
        [Fact]
        public void BottomModeInput_DefaultsAreStable()
        {
            var input = new BottomModeInputV30();
            Assert.Equal(AIRole.Opponent, input.Role);
            Assert.Equal(10, input.EstimatedBottomPoints);
            Assert.Equal(2, input.BottomMultiplier);
        }

        [Fact]
        public void BottomModeDecision_CanBeConstructed()
        {
            var decision = new BottomModeDecisionV30
            {
                BottomScoreBand = BottomScoreBandV30.Medium,
                OperationalMode = BottomOperationalModeV30.ProtectBottomAttention,
                ContestMode = BottomContestModeV30.ContestBottomAttention
            };

            Assert.Equal(BottomScoreBandV30.Medium, decision.BottomScoreBand);
            Assert.Equal(BottomOperationalModeV30.ProtectBottomAttention, decision.OperationalMode);
            Assert.Equal(BottomContestModeV30.ContestBottomAttention, decision.ContestMode);
        }

        [Fact]
        public void BottomPlanInput_CanBeConstructed()
        {
            var input = new BottomPlanInputV30
            {
                DefenderScore = 60,
                SingleBottomGainPoints = 10,
                DoubleBottomGainPoints = 20
            };

            Assert.Equal(60, input.DefenderScore);
        }

        [Fact]
        public void JokerControlModels_CanBeConstructed()
        {
            var input = new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = true,
                SmallJokerSecurity = WinSecurityTierV30.FragileWin
            };
            var decision = new JokerControlDecisionV30
            {
                ShouldPlaySmallJokerFirst = false,
                Reason = "test"
            };

            Assert.True(input.BigJokerUnplayedLikelyInRearOpponent);
            Assert.False(decision.ShouldPlaySmallJokerFirst);
        }

        [Fact]
        public void EndgameControlModels_CanBeConstructed()
        {
            var input = new EndgameControlInputV30
            {
                OperationalMode = BottomOperationalModeV30.StrongProtectBottom,
                CurrentTrickPoints = 10
            };
            var decision = new EndgameControlDecisionV30
            {
                FreezeTrumpResources = true,
                AllowConcedeLowPointTrick = true
            };

            Assert.Equal(BottomOperationalModeV30.StrongProtectBottom, input.OperationalMode);
            Assert.True(decision.FreezeTrumpResources);
        }
    }
}
