using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Bottom
{
    public class BottomPlanSelectorV30Tests
    {
        [Fact]
        public void Select_PrefersSingle_WhenSingleAlreadyWins()
        {
            var selector = new BottomPlanSelectorV30();
            var decision = selector.Select(new BottomPlanInputV30
            {
                DefenderScore = 72,
                SingleBottomGainPoints = 10,
                DoubleBottomGainPoints = 20,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Fragile
            });

            Assert.Equal(BottomPlanGoalV30.SingleBottomPreferred, decision.Goal);
            Assert.True(decision.CanWinWithSingleBottom);
            Assert.True(decision.CanWinWithDoubleBottom);
            Assert.False(decision.ShouldPreservePairsAndTractors);
        }

        [Fact]
        public void Select_PrefersDouble_WhenSingleNotEnoughButDoubleEnough()
        {
            var selector = new BottomPlanSelectorV30();
            var decision = selector.Select(new BottomPlanInputV30
            {
                DefenderScore = 52,
                SingleBottomGainPoints = 20,
                DoubleBottomGainPoints = 30,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Stable
            });

            Assert.Equal(BottomPlanGoalV30.DoubleBottomPreferred, decision.Goal);
            Assert.False(decision.CanWinWithSingleBottom);
            Assert.True(decision.CanWinWithDoubleBottom);
            Assert.True(decision.ShouldPreservePairsAndTractors);
        }

        [Fact]
        public void Select_AllowsDoubleOnlyWhenEquallyStable_IfSingleAlreadyWins()
        {
            var selector = new BottomPlanSelectorV30();
            var decision = selector.Select(new BottomPlanInputV30
            {
                DefenderScore = 65,
                SingleBottomGainPoints = 15,
                DoubleBottomGainPoints = 30,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Stable
            });

            Assert.Equal(BottomPlanGoalV30.DoubleBottomPreferred, decision.Goal);
            Assert.True(decision.CanWinWithSingleBottom);
            Assert.True(decision.CanWinWithDoubleBottom);
        }

        [Fact]
        public void Select_NoBottomLine_WhenNeitherSingleNorDoubleCanWin()
        {
            var selector = new BottomPlanSelectorV30();
            var decision = selector.Select(new BottomPlanInputV30
            {
                DefenderScore = 30,
                SingleBottomGainPoints = 10,
                DoubleBottomGainPoints = 20,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Stable
            });

            Assert.Equal(BottomPlanGoalV30.NoBottomLine, decision.Goal);
            Assert.False(decision.CanWinWithSingleBottom);
            Assert.False(decision.CanWinWithDoubleBottom);
        }
    }
}
