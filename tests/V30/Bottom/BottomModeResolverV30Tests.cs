using TractorGame.Core.AI;
using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Bottom
{
    public class BottomModeResolverV30Tests
    {
        [Fact]
        public void ResolveOperationalMode_Dealer_EntersAttention_WhenThresholdMet()
        {
            var resolver = new BottomModeResolverV30();

            var mode = resolver.ResolveOperationalMode(
                AIRole.Dealer,
                defenderScore: 39,
                remainingContestableScore: 20,
                bottomPoints: 15);

            Assert.Equal(BottomOperationalModeV30.ProtectBottomAttention, mode);
        }

        [Fact]
        public void ResolveOperationalMode_Dealer_EntersStrong_WhenSingleBottomRiskHigh()
        {
            var resolver = new BottomModeResolverV30();

            var mode = resolver.ResolveOperationalMode(
                AIRole.Dealer,
                defenderScore: 52,
                remainingContestableScore: 8,
                bottomPoints: 10);

            Assert.Equal(BottomOperationalModeV30.StrongProtectBottom, mode);
        }

        [Fact]
        public void ResolveContestMode_Opponent_Attention_WhenDefenderScoreAtLeast50()
        {
            var resolver = new BottomModeResolverV30();

            var mode = resolver.ResolveContestMode(
                AIRole.Opponent,
                defenderScore: 55,
                estimatedBottomPoints: 10,
                bottomMultiplier: 2);

            Assert.Equal(BottomContestModeV30.ContestBottomAttention, mode);
        }

        [Fact]
        public void ResolveContestMode_Opponent_Strong_WhenFormulaReachesWinline()
        {
            var resolver = new BottomModeResolverV30();

            var mode = resolver.ResolveContestMode(
                AIRole.Opponent,
                defenderScore: 60,
                estimatedBottomPoints: 10,
                bottomMultiplier: 2);

            Assert.Equal(BottomContestModeV30.StrongContestBottom, mode);
        }

        [Theory]
        [InlineData(10, BottomScoreBandV30.Low)]
        [InlineData(11, BottomScoreBandV30.Medium)]
        [InlineData(29, BottomScoreBandV30.Medium)]
        [InlineData(30, BottomScoreBandV30.High)]
        public void ResolveBottomScoreBand_UsesFrozenBands(int points, BottomScoreBandV30 expected)
        {
            var resolver = new BottomModeResolverV30();
            Assert.Equal(expected, resolver.ResolveBottomScoreBand(points));
        }
    }
}
