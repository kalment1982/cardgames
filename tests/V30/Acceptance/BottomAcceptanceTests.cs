using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Acceptance
{
    public class BottomAcceptanceTests
    {
        private readonly BottomModeResolverV30 _modeResolver = new BottomModeResolverV30();
        private readonly BottomPlanSelectorV30 _planSelector = new BottomPlanSelectorV30();
        private readonly EndgameControlPolicyV30 _endgamePolicy = new EndgameControlPolicyV30();
        private readonly BottomScoreEstimatorV30 _scoreEstimator = new BottomScoreEstimatorV30();

        [Fact]
        public void Bottom001_DealerShouldEnterProtectModeEarly_WhenBottomRiskIsHigh()
        {
            var decision = _modeResolver.Resolve(new BottomModeInputV30
            {
                Role = AIRole.Dealer,
                DefenderScore = 39,
                RemainingContestableScore = 20,
                BottomPoints = 15,
                EstimatedBottomPoints = 10,
                BottomMultiplier = 2
            });

            Assert.Equal(BottomOperationalModeV30.ProtectBottomAttention, decision.OperationalMode);
        }

        [Fact]
        public void Bottom003_ShouldPreferSingleBottom_WhenSingleBottomAlreadyWins()
        {
            var decision = _planSelector.Select(new BottomPlanInputV30
            {
                DefenderScore = 72,
                SingleBottomGainPoints = 10,
                DoubleBottomGainPoints = 20,
                SinglePlanStability = PlanStabilityV30.Stable,
                DoublePlanStability = PlanStabilityV30.Fragile
            });

            Assert.Equal(BottomPlanGoalV30.SingleBottomPreferred, decision.Goal);
            Assert.False(decision.ShouldPreservePairsAndTractors);
        }

        [Fact]
        public void Bottom006_DefaultOrderSmallThenBigJoker_WithThreatException()
        {
            var defaultOrder = _endgamePolicy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = false,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.StableWin
            });

            var threatOrder = _endgamePolicy.DecideJokerOrder(new JokerControlInputV30
            {
                BigJokerUnplayedLikelyInRearOpponent = true,
                RearOpponentLikelyHasStrongerTrumpStructure = false,
                SmallJokerSecurity = WinSecurityTierV30.StableWin
            });

            Assert.True(defaultOrder.ShouldPlaySmallJokerFirst);
            Assert.False(threatOrder.ShouldPlaySmallJokerFirst);
        }

        [Fact]
        public void Bottom008_DefenderBottomContest_IsDynamicAcrossPhases()
        {
            var normal = _modeResolver.ResolveContestMode(
                AIRole.Opponent,
                defenderScore: 45,
                estimatedBottomPoints: 10,
                bottomMultiplier: 2);

            var attention = _modeResolver.ResolveContestMode(
                AIRole.Opponent,
                defenderScore: 55,
                estimatedBottomPoints: 10,
                bottomMultiplier: 2);

            var strong = _modeResolver.ResolveContestMode(
                AIRole.Opponent,
                defenderScore: 60,
                estimatedBottomPoints: 10,
                bottomMultiplier: 2);

            Assert.Equal(BottomContestModeV30.NormalContest, normal);
            Assert.Equal(BottomContestModeV30.ContestBottomAttention, attention);
            Assert.Equal(BottomContestModeV30.StrongContestBottom, strong);
        }

        [Fact]
        public void Bottom009_BottomScoreEstimate_ShouldIncreaseOnUnseenScoreSignals()
        {
            var baseline = _scoreEstimator.EstimateBottomPoints(knownBottomPoints: null);
            var boosted = _scoreEstimator.EstimateBottomPoints(
                knownBottomPoints: null,
                signals: new[]
                {
                    new BottomScoreSignalV30
                    {
                        SignalType = BottomScoreSignalTypeV30.SuitExhaustedScoreUnseen,
                        Confidence = 0.8
                    },
                    new BottomScoreSignalV30
                    {
                        SignalType = BottomScoreSignalTypeV30.MultiSuitExhaustedScoreUnseen,
                        Confidence = 0.9
                    }
                });

            Assert.Equal(10, baseline);
            Assert.True(boosted > baseline);
        }

        [Fact]
        public void Bottom011_DealerProtectBottomMode_ShouldSwitchDynamically()
        {
            var strongMode = _modeResolver.ResolveOperationalMode(
                AIRole.Dealer,
                defenderScore: 52,
                remainingContestableScore: 8,
                bottomPoints: 10);

            var control = _endgamePolicy.ResolveTrumpControl(new EndgameControlInputV30
            {
                OperationalMode = strongMode,
                CurrentTrickPoints = 10
            });

            Assert.Equal(BottomOperationalModeV30.StrongProtectBottom, strongMode);
            Assert.True(control.FreezeTrumpResources);
            Assert.True(control.AllowConcedeLowPointTrick);
        }
    }
}
