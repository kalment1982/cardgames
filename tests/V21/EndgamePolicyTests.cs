using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class EndgamePolicyTests
    {
        [Fact]
        public void ResolveBottomContestPressure_Opponent_WhenCanWinWithRemainingScore_IsMedium()
        {
            var policy = new EndgamePolicy();
            var level = policy.ResolveBottomContestPressure(
                AIRole.Opponent,
                defenderScore: 60,
                remainingScoreTotal: 25,
                bottomPoints: 0,
                cardsLeftMin: 4,
                remainingScoreCards: 6);

            Assert.Equal(RiskLevel.Medium, level);
        }

        [Fact]
        public void ResolveBottomContestPressure_Opponent_WhenNeedsDoubleBottom_IsHigh()
        {
            var policy = new EndgamePolicy();
            var level = policy.ResolveBottomContestPressure(
                AIRole.Opponent,
                defenderScore: 40,
                remainingScoreTotal: 30,
                bottomPoints: 10,
                cardsLeftMin: 4,
                remainingScoreCards: 6);

            Assert.Equal(RiskLevel.High, level);
        }

        [Fact]
        public void ResolveBottomRisk_Dealer_WhenOpponentsCanReachWinline_IsHigh()
        {
            var policy = new EndgamePolicy();
            var level = policy.ResolveBottomRisk(
                AIRole.Dealer,
                bottomPoints: 10,
                cardsLeftMin: 4,
                scorePressure: ScorePressureLevel.Relaxed,
                defenderScore: 70,
                remainingScoreTotal: 15);

            Assert.Equal(RiskLevel.High, level);
        }
    }
}
