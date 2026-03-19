using TractorGame.Core.AI.V30.Memory;
using Xunit;

namespace TractorGame.Tests.V30.Memory
{
    public class ThreatAssessmentV30Tests
    {
        private readonly ThreatAssessmentV30 _assessment = new();

        [Fact]
        public void Evaluate_WhenCannotBeatCurrentWinner_IsFragileWin()
        {
            var result = _assessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = false
            });

            Assert.Equal(WinSecurityLevelV30.FragileWin, result.WinSecurity);
            Assert.Equal("CannotBeatCurrentWinner", result.Reason);
        }

        [Fact]
        public void Evaluate_WhenNoRemainingOpponents_IsLockWin()
        {
            var result = _assessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 0.8 }
                }
            });

            Assert.Equal(WinSecurityLevelV30.LockWin, result.WinSecurity);
            Assert.Equal("NoRemainingOpponents", result.Reason);
        }

        [Fact]
        public void Evaluate_WhenOpponentRiskLow_IsStableWin()
        {
            var result = _assessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.10 },
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 0.90 }
                }
            });

            Assert.Equal(WinSecurityLevelV30.StableWin, result.WinSecurity);
            Assert.Equal("OpponentOvertakeRiskLow", result.Reason);
            Assert.True(result.OpponentOvertakeRisk <= ThreatAssessmentV30.StableRiskUpperBound);
        }

        [Fact]
        public void Evaluate_WhenOpponentRiskHigh_IsFragileWin()
        {
            var result = _assessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.60 },
                    new RemainingPlayerThreatV30 { PlayerIndex = 3, IsTeammate = false, OvertakeProbability = 0.50 }
                }
            });

            Assert.Equal(WinSecurityLevelV30.FragileWin, result.WinSecurity);
            Assert.Equal("OpponentOvertakeRiskHigh", result.Reason);
            Assert.True(result.OpponentOvertakeRisk > ThreatAssessmentV30.StableRiskUpperBound);
        }

        [Fact]
        public void Evaluate_WhenOpponentsCannotOvertake_IsLockWin()
        {
            var result = _assessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.0 },
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 1.0 }
                }
            });

            Assert.Equal(WinSecurityLevelV30.LockWin, result.WinSecurity);
            Assert.Equal("OpponentsCannotOvertake", result.Reason);
        }
    }
}
