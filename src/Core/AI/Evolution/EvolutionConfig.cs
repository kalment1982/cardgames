using System;
using System.IO;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution
{
    public enum Layer1SelectionMode
    {
        TopK,
        BhFdr
    }

    public sealed class EvolutionConfig
    {
        public int GenerationNumber { get; set; } = 1;
        public int? CandidateCountOverride { get; set; }
        public int Layer1TopK { get; set; } = 6;
        public int Layer2TopK { get; set; } = 2;
        public int Layer1GamesPerCandidate { get; set; } = 12;
        public int Layer2GamesPerCandidate { get; set; } = 24;
        public int Layer3GamesPerSeed { get; set; } = 36;
        public int Layer3Seeds { get; set; } = 3;
        public int MaxTurnsPerGame { get; set; } = 300;
        public int MaxParallelism { get; set; } = Math.Max(1, Environment.ProcessorCount - 2);
        public int BootstrapIterations { get; set; } = 5000;
        public double ConfidenceLevel { get; set; } = 0.95;
        public Layer1SelectionMode Layer1Selection { get; set; } = Layer1SelectionMode.TopK;
        public double Layer1BhFdr { get; set; } = 0.10;
        public bool ForceDefenderBoostCandidate { get; set; }
        public int ForceDefenderBoostGeneration { get; set; } = 24;

        public AIDifficulty EvaluationDifficulty { get; set; } = AIDifficulty.Hard;
        public int EvaluationLevelRankValue { get; set; } = 5;

        public int CoolingHoursAfterRollback { get; set; } = 24;
        public int StagnationThreshold { get; set; } = 8;
        public int PromotionGrayZoneGames { get; set; } = 12000;

        public string RepoRoot { get; set; } = EvolutionPaths.ResolveRepoRoot();

        public string DataRootPath => Path.Combine(RepoRoot, "data", "evolution");
        public string ChampionsPath => Path.Combine(DataRootPath, "champions");
        public string CandidatesPath => Path.Combine(DataRootPath, "candidates");
        public string ReportsPath => Path.Combine(DataRootPath, "reports");
        public string LogsPath => Path.Combine(DataRootPath, "logs");
        public string StateFilePath => Path.Combine(DataRootPath, "evolution_state.json");
        public string RegistryFilePath => Path.Combine(DataRootPath, "policy_registry.jsonl");

        public int ResolveCandidateCount(int stagnationCount)
        {
            if (CandidateCountOverride.HasValue && CandidateCountOverride.Value > 0)
                return CandidateCountOverride.Value;

            if (stagnationCount >= StagnationThreshold)
                return 40;

            if (GenerationNumber <= 3)
                return 32;

            return 24;
        }

        public double ResolveLongTailQuotaRatio(int stagnationCount)
        {
            if (stagnationCount >= StagnationThreshold)
                return 0.40;

            if (GenerationNumber <= 5)
                return 0.30;

            return 0.15;
        }
    }
}
