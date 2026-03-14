using System;
using System.Collections.Generic;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution
{
    public sealed class CandidateProfile
    {
        public string CandidateId { get; set; } = string.Empty;
        public int Generation { get; set; }
        public string ParentHash { get; set; } = string.Empty;
        public string GenomeHash { get; set; } = string.Empty;
        public AIStrategyParameters Parameters { get; set; } = AIStrategyParameters.CreateDefault();
    }

    public sealed class CandidateEvaluation
    {
        public CandidateProfile Candidate { get; set; } = new();
        public int Games { get; set; }
        public int Wins { get; set; }
        public int Losses => Games - Wins;
        public double WinRate => Games == 0 ? 0 : (double)Wins / Games;

        public int CandidateIllegalDecisions { get; set; }
        public int CandidateDecisions { get; set; }
        public int OpponentIllegalDecisions { get; set; }
        public int OpponentDecisions { get; set; }

        public double CandidateIllegalRate => CandidateDecisions == 0 ? 0 : (double)CandidateIllegalDecisions / CandidateDecisions;
        public double OpponentIllegalRate => OpponentDecisions == 0 ? 0 : (double)OpponentIllegalDecisions / OpponentDecisions;

        public double CandidateAvgLatencyMs { get; set; }
        public double CandidateP99LatencyMs { get; set; }
        public double OpponentAvgLatencyMs { get; set; }
        public double OpponentP99LatencyMs { get; set; }

        public double CandidateDiversity { get; set; }
        public double OpponentDiversity { get; set; }

        public int CandidateDealerSideGames { get; set; }
        public int CandidateDealerSideWins { get; set; }
        public int CandidateDefenderSideGames { get; set; }
        public int CandidateDefenderSideWins { get; set; }

        public double CandidateDealerWinRate =>
            CandidateDealerSideGames == 0 ? 0 : (double)CandidateDealerSideWins / CandidateDealerSideGames;
        public double CandidateDefenderWinRate =>
            CandidateDefenderSideGames == 0 ? 0 : (double)CandidateDefenderSideWins / CandidateDefenderSideGames;

        public double WinRateCiLow { get; set; }
        public double WinRateCiHigh { get; set; }

        public string Layer { get; set; } = string.Empty;
    }

    public sealed class EvolutionState
    {
        public int Generation { get; set; }
        public int ConsecutiveNoPromotion { get; set; }
        public string CurrentChampionHash { get; set; } = string.Empty;
        public DateTime? CooldownUntilUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
    }

    public sealed class ChampionSnapshot
    {
        public string ChampionId { get; set; } = string.Empty;
        public string GenomeHash { get; set; } = string.Empty;
        public int Generation { get; set; }
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public AIStrategyParameters Parameters { get; set; } = AIStrategyParameters.CreateDefault();
    }

    public sealed class EvolutionRunResult
    {
        public int Generation { get; set; }
        public bool Promoted { get; set; }
        public string PromotionReason { get; set; } = string.Empty;
        public string ChampionBeforeHash { get; set; } = string.Empty;
        public string ChampionAfterHash { get; set; } = string.Empty;
        public string? ReportPath { get; set; }
        public DateTime StartedAtUtc { get; set; }
        public DateTime FinishedAtUtc { get; set; }
        public int CandidateCount { get; set; }
        public CandidateEvaluation? BestCandidate { get; set; }
        public IReadOnlyList<CandidateEvaluation> FinalEvaluations { get; set; } = Array.Empty<CandidateEvaluation>();
        public DataEngine.DataQualityReport? DataQualityReport { get; set; }
    }
}
