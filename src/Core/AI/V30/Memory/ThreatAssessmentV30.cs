using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.V30.Memory
{
    public sealed class RemainingPlayerThreatV30
    {
        public int PlayerIndex { get; init; }
        public bool IsTeammate { get; init; }
        public double OvertakeProbability { get; init; }
    }

    public sealed class ThreatAssessmentInputV30
    {
        public bool CandidateCanBeatCurrentWinner { get; init; }
        public IReadOnlyList<RemainingPlayerThreatV30> RemainingPlayers { get; init; } = Array.Empty<RemainingPlayerThreatV30>();
    }

    public sealed class ThreatAssessmentResultV30
    {
        public WinSecurityLevelV30 WinSecurity { get; init; }
        public double OpponentOvertakeRisk { get; init; }
        public int RemainingOpponentCount { get; init; }
        public int RemainingTeammateCount { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public sealed class ThreatAssessmentV30
    {
        public const double StableRiskUpperBound = 0.25;

        public ThreatAssessmentResultV30 Evaluate(ThreatAssessmentInputV30 input)
        {
            if (!input.CandidateCanBeatCurrentWinner)
            {
                return new ThreatAssessmentResultV30
                {
                    WinSecurity = WinSecurityLevelV30.FragileWin,
                    OpponentOvertakeRisk = 1.0,
                    RemainingOpponentCount = 0,
                    RemainingTeammateCount = 0,
                    Reason = "CannotBeatCurrentWinner"
                };
            }

            var remainingPlayers = input.RemainingPlayers ?? Array.Empty<RemainingPlayerThreatV30>();
            var opponents = remainingPlayers.Where(player => !player.IsTeammate).ToList();
            var teammates = remainingPlayers.Where(player => player.IsTeammate).ToList();

            if (opponents.Count == 0)
            {
                return new ThreatAssessmentResultV30
                {
                    WinSecurity = WinSecurityLevelV30.LockWin,
                    OpponentOvertakeRisk = 0.0,
                    RemainingOpponentCount = 0,
                    RemainingTeammateCount = teammates.Count,
                    Reason = "NoRemainingOpponents"
                };
            }

            double opponentRisk = ComputeCombinedRisk(opponents);
            if (opponentRisk <= 0.0001)
            {
                return new ThreatAssessmentResultV30
                {
                    WinSecurity = WinSecurityLevelV30.LockWin,
                    OpponentOvertakeRisk = 0.0,
                    RemainingOpponentCount = opponents.Count,
                    RemainingTeammateCount = teammates.Count,
                    Reason = "OpponentsCannotOvertake"
                };
            }

            if (opponentRisk <= StableRiskUpperBound)
            {
                return new ThreatAssessmentResultV30
                {
                    WinSecurity = WinSecurityLevelV30.StableWin,
                    OpponentOvertakeRisk = opponentRisk,
                    RemainingOpponentCount = opponents.Count,
                    RemainingTeammateCount = teammates.Count,
                    Reason = "OpponentOvertakeRiskLow"
                };
            }

            return new ThreatAssessmentResultV30
            {
                WinSecurity = WinSecurityLevelV30.FragileWin,
                OpponentOvertakeRisk = opponentRisk,
                RemainingOpponentCount = opponents.Count,
                RemainingTeammateCount = teammates.Count,
                Reason = "OpponentOvertakeRiskHigh"
            };
        }

        private static double ComputeCombinedRisk(IReadOnlyList<RemainingPlayerThreatV30> opponents)
        {
            double notOvertaken = 1.0;
            foreach (var opponent in opponents)
            {
                double probability = Math.Max(0.0, Math.Min(1.0, opponent.OvertakeProbability));
                notOvertaken *= (1.0 - probability);
            }

            return 1.0 - notOvertaken;
        }
    }
}
