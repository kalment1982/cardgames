using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 当前决策帧（局面状态 + 压力信号）。
    /// </summary>
    public sealed class DecisionFrameV30
    {
        public PhaseKindV30 PhaseKind { get; init; } = PhaseKindV30.Unknown;

        public int TrickIndex { get; init; }

        public int TurnIndex { get; init; }

        public int PlayPosition { get; init; }

        public int CardsLeftMin { get; init; } = -1;

        public int CurrentWinningPlayer { get; init; } = -1;

        public bool PartnerWinning { get; init; }

        public List<Card> LeadCards { get; init; } = new();

        public List<Card> CurrentWinningCards { get; init; } = new();

        public int CurrentTrickScore { get; init; }

        public int DefenderScore { get; init; }

        public int BottomPoints { get; init; }

        public int EstimatedBottomPoints { get; init; }

        public int PlayedScoreTotal { get; init; }

        public int RemainingScoreTotal { get; init; }

        public int RemainingScoreCards { get; init; }

        public int RemainingContestableScore { get; init; }

        public RiskLevelV30 BottomRiskPressure { get; init; } = RiskLevelV30.None;

        public RiskLevelV30 DealerRetentionRisk { get; init; } = RiskLevelV30.None;

        public RiskLevelV30 BottomContestPressure { get; init; } = RiskLevelV30.None;

        public ScorePressureLevelV30 ScorePressure { get; init; } = ScorePressureLevelV30.Relaxed;

        public EndgameLevelV30 EndgameLevel { get; init; } = EndgameLevelV30.None;

        /// <summary>
        /// 合同层统一概率阈值（当前冻结口径为 0.70）。
        /// </summary>
        public double ProbabilityThreshold { get; init; } = 0.70;
    }
}

