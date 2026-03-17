using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    public enum DecisionIntentKind
    {
        Unknown = 0,
        TakeScore = 1,
        ProtectBottom = 2,
        SaveControl = 3,
        PassToMate = 4,
        ForceTrump = 5,
        ShapeHand = 6,
        PreserveStructure = 7,
        TakeLead = 8,
        MinimizeLoss = 9,
        PrepareEndgame = 10,
        PrepareThrow = 11,
        AttackLongSuit = 12,
        ProbeWeakSuit = 13
    }

    public enum RiskLevel
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    public enum ScorePressureLevel
    {
        Relaxed = 0,
        Tight = 1,
        Critical = 2
    }

    public enum EndgameLevel
    {
        None = 0,
        Late = 1,
        FinalThree = 2,
        LastTrickRace = 3
    }

    public sealed class RuleProfile
    {
        public string Mode { get; init; } = "Standard80";

        public Suit? TrumpSuit { get; init; }

        public Rank LevelRank { get; init; }

        public int ThrowFailPenalty { get; init; }

        public bool EnableCounterBottom { get; init; }

        public bool AllowBrokenTractor { get; init; }

        public bool StrictFollowStructure { get; init; } = true;

        public bool StrictCutStructure { get; init; } = true;

        public string BottomMultiplierRule { get; init; } = "LastTrickLeadPattern";

        public static RuleProfile FromConfig(GameConfig config)
        {
            return new RuleProfile
            {
                TrumpSuit = config.TrumpSuit,
                LevelRank = config.LevelRank,
                ThrowFailPenalty = config.ThrowFailPenalty,
                EnableCounterBottom = config.EnableCounterBottom
            };
        }
    }

    public sealed class DifficultyProfile
    {
        public AIDifficulty Difficulty { get; init; } = AIDifficulty.Medium;

        public bool MemoryEnabled { get; init; } = true;

        public int InferenceDepth { get; init; } = 2;

        public bool UseNoPairEvidence { get; init; } = true;

        public bool UseThrowSafetyEstimate { get; init; } = true;

        public double TakeScoreThreshold { get; init; } = 10;

        public double ForceTrumpAggression { get; init; } = 0.5;

        public double CheapOvertakeTolerance { get; init; } = 0.5;

        public double EndgameAllInBias { get; init; } = 0.5;

        public double PreserveControlWeight { get; init; } = 1.0;

        public double PreserveStructureWeight { get; init; } = 1.0;

        public double TrumpConsumptionPenalty { get; init; } = 1.0;

        public double HighCardRetentionPenalty { get; init; } = 1.0;

        public double PassToMateBias { get; init; } = 1.0;

        public double ProtectMateLeadBias { get; init; } = 1.0;

        public double AvoidOvertakeMateBias { get; init; } = 1.0;

        public static DifficultyProfile From(AIDifficulty difficulty, AIStrategyParameters? strategy = null)
        {
            strategy ??= AIStrategyParameters.CreatePreset(difficulty);
            return difficulty switch
            {
                AIDifficulty.Easy => new DifficultyProfile
                {
                    Difficulty = difficulty,
                    MemoryEnabled = false,
                    InferenceDepth = 0,
                    UseNoPairEvidence = false,
                    UseThrowSafetyEstimate = false,
                    TakeScoreThreshold = 15,
                    ForceTrumpAggression = 0.25,
                    CheapOvertakeTolerance = 0.25,
                    EndgameAllInBias = 0.20,
                    PreserveControlWeight = 0.65,
                    PreserveStructureWeight = 0.60,
                    TrumpConsumptionPenalty = 0.70,
                    HighCardRetentionPenalty = 0.65,
                    PassToMateBias = 0.90,
                    ProtectMateLeadBias = 0.75,
                    AvoidOvertakeMateBias = 0.85
                },
                AIDifficulty.Medium => new DifficultyProfile
                {
                    Difficulty = difficulty,
                    MemoryEnabled = true,
                    InferenceDepth = 1,
                    UseNoPairEvidence = true,
                    UseThrowSafetyEstimate = true,
                    TakeScoreThreshold = 10,
                    ForceTrumpAggression = 0.45,
                    CheapOvertakeTolerance = 0.55,
                    EndgameAllInBias = 0.45,
                    PreserveControlWeight = 0.90,
                    PreserveStructureWeight = 0.85,
                    TrumpConsumptionPenalty = 0.90,
                    HighCardRetentionPenalty = 0.85,
                    PassToMateBias = strategy.PartnerWinning_GivePointsPriority,
                    ProtectMateLeadBias = strategy.PartnerWinning_AvoidTrumpPriority,
                    AvoidOvertakeMateBias = strategy.PartnerWinning_KeepPairsPriority
                },
                AIDifficulty.Hard => new DifficultyProfile
                {
                    Difficulty = difficulty,
                    MemoryEnabled = true,
                    InferenceDepth = 2,
                    UseNoPairEvidence = true,
                    UseThrowSafetyEstimate = true,
                    TakeScoreThreshold = 8,
                    ForceTrumpAggression = 0.60,
                    CheapOvertakeTolerance = 0.70,
                    EndgameAllInBias = 0.60,
                    PreserveControlWeight = 1.00,
                    PreserveStructureWeight = 1.00,
                    TrumpConsumptionPenalty = 1.00,
                    HighCardRetentionPenalty = 1.00,
                    PassToMateBias = strategy.PartnerWinning_GivePointsPriority,
                    ProtectMateLeadBias = strategy.PartnerWinning_AvoidTrumpPriority,
                    AvoidOvertakeMateBias = strategy.PartnerWinning_KeepPairsPriority
                },
                _ => new DifficultyProfile
                {
                    Difficulty = difficulty,
                    MemoryEnabled = true,
                    InferenceDepth = 3,
                    UseNoPairEvidence = true,
                    UseThrowSafetyEstimate = true,
                    TakeScoreThreshold = 6,
                    ForceTrumpAggression = 0.75,
                    CheapOvertakeTolerance = 0.80,
                    EndgameAllInBias = 0.75,
                    PreserveControlWeight = 1.10,
                    PreserveStructureWeight = 1.05,
                    TrumpConsumptionPenalty = 1.10,
                    HighCardRetentionPenalty = 1.10,
                    PassToMateBias = strategy.PartnerWinning_GivePointsPriority,
                    ProtectMateLeadBias = strategy.PartnerWinning_AvoidTrumpPriority,
                    AvoidOvertakeMateBias = strategy.PartnerWinning_KeepPairsPriority
                }
            };
        }
    }

    public sealed class StyleProfile
    {
        public int SessionStyleSeed { get; init; }

        public double TieBreakRandomness { get; init; }

        public double EarlyBidLuck { get; init; }

        public double ThrowRiskTolerance { get; init; }

        public static StyleProfile Create(int sessionStyleSeed)
        {
            var seed = sessionStyleSeed == 0 ? 73129 : sessionStyleSeed;
            var random = new Random(seed);
            return new StyleProfile
            {
                SessionStyleSeed = seed,
                TieBreakRandomness = 0.02 + random.NextDouble() * 0.08,
                EarlyBidLuck = 0.10 + random.NextDouble() * 0.20,
                ThrowRiskTolerance = 0.15 + random.NextDouble() * 0.35
            };
        }
    }

    public sealed class HandProfile
    {
        public int TrumpCount { get; init; }

        public int HighTrumpCount { get; init; }

        public int JokerCount { get; init; }

        public int LevelCardCount { get; init; }

        public int TrumpPairCount { get; init; }

        public int TrumpTractorCount { get; init; }

        public Dictionary<Suit, int> SuitLengths { get; init; } = new();

        public Suit? StrongestSuit { get; init; }

        public Suit? WeakestSuit { get; init; }

        public List<Suit> PotentialVoidTargets { get; init; } = new();

        public int ScoreCardCount { get; init; }

        public string StructureSummary { get; init; } = string.Empty;
    }

    public sealed class MemorySnapshot
    {
        public Dictionary<string, int> PlayedCountByCard { get; init; } = new();

        public Dictionary<int, List<string>> VoidSuitsByPlayer { get; init; } = new();

        public Dictionary<int, List<string>> NoPairEvidence { get; init; } = new();

        public Dictionary<int, List<string>> NoTractorEvidence { get; init; } = new();

        public List<string> KnownBottomCards { get; init; } = new();
    }

    public sealed class EstimateRange
    {
        public double Estimate { get; init; }

        public double Lower { get; init; }

        public double Upper { get; init; }

        public double Confidence { get; init; }
    }

    public sealed class ProbabilityEstimate
    {
        public double Probability { get; init; }

        public double Confidence { get; init; }
    }

    public sealed class RiskEstimate
    {
        public RiskLevel Level { get; init; } = RiskLevel.None;

        public double Confidence { get; init; }
    }

    public sealed class ThrowSafetyEstimate
    {
        public string Level { get; init; } = "UnsafeRejected";

        public double SuccessProbability { get; init; }

        public bool IsDeterministicallySafe { get; init; }
    }

    public sealed class InferenceSnapshot
    {
        public Dictionary<int, EstimateRange> EstimatedTrumpCountByPlayer { get; init; } = new();

        public Dictionary<int, RiskEstimate> HighTrumpRiskByPlayer { get; init; } = new();

        public Dictionary<string, ProbabilityEstimate> PairPotentialBySystem { get; init; } = new();

        public Dictionary<string, ProbabilityEstimate> TractorPotentialBySystem { get; init; } = new();

        public ThrowSafetyEstimate ThrowSafetyEstimate { get; init; } = new();

        public Dictionary<string, RiskEstimate> LeadCutRiskBySystem { get; init; } = new();

        public ProbabilityEstimate MateHoldConfidence { get; init; } = new();

        public RiskEstimate EndgameBottomThreat { get; init; } = new();
    }

    public sealed class DecisionFrame
    {
        public PhaseKind PhaseKind { get; init; } = PhaseKind.Unknown;

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

        public int PlayedScoreTotal { get; init; }

        public int RemainingScoreTotal { get; init; }

        public int RemainingScoreCards { get; init; }

        public RiskLevel BottomRiskPressure { get; init; } = RiskLevel.None;

        public RiskLevel DealerRetentionRisk { get; init; } = RiskLevel.None;

        public RiskLevel BottomContestPressure { get; init; } = RiskLevel.None;

        public ScorePressureLevel ScorePressure { get; init; } = ScorePressureLevel.Relaxed;

        public EndgameLevel EndgameLevel { get; init; } = EndgameLevel.None;
    }

    public sealed class ResolvedIntent
    {
        public DecisionIntentKind PrimaryIntent { get; init; } = DecisionIntentKind.Unknown;

        public DecisionIntentKind SecondaryIntent { get; init; } = DecisionIntentKind.Unknown;

        public string Mode { get; init; } = string.Empty;

        public double Priority { get; init; }

        public List<string> VetoFlags { get; init; } = new();

        public List<string> RiskFlags { get; init; } = new();

        public double MaxCostBudget { get; init; } = 1.0;
    }

    public sealed class ScoredAction
    {
        public List<Card> Cards { get; init; } = new();

        public double Score { get; init; }

        public string ReasonCode { get; init; } = string.Empty;

        public Dictionary<string, double> Features { get; init; } = new();
    }

    public sealed class PhaseDecision
    {
        public PhaseKind Phase { get; init; } = PhaseKind.Unknown;

        public List<Card> SelectedCards { get; init; } = new();

        public ResolvedIntent Intent { get; init; } = new();

        public List<ScoredAction> ScoredActions { get; init; } = new();

        public DecisionExplanation Explanation { get; init; } = new();

        public string SelectedReason => Explanation.SelectedReason;
    }
}
