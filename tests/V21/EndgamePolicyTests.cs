using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
using Xunit;

namespace TractorGame.Tests.V21
{
    public class EndgamePolicyTests
    {
        [Theory]
        [InlineData(80, ScorePressureLevel.Critical)]
        [InlineData(50, ScorePressureLevel.Tight)]
        [InlineData(20, ScorePressureLevel.Relaxed)]
        public void ResolveScorePressure_ReturnsExpectedLevel(int defenderScore, ScorePressureLevel expected)
        {
            var policy = new EndgamePolicy();
            Assert.Equal(expected, policy.ResolveScorePressure(defenderScore));
        }

        [Fact]
        public void ResolveBottomRisk_DealerHighBottomInLateGame_IsHigh()
        {
            var policy = new EndgamePolicy();
            var risk = policy.ResolveBottomRisk(AIRole.Dealer, bottomPoints: 20, cardsLeftMin: 5, ScorePressureLevel.Tight);
            Assert.Equal(RiskLevel.High, risk);
        }
    }
}
