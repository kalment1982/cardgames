using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TractorGame.Core.AI.Evolution.DataEngine;
using TractorGame.Core.AI.Evolution.GateKeeper;
using TractorGame.Core.AI.Evolution.LeagueArena;
using TractorGame.Core.AI.Evolution.PolicyFactory;
using TractorGame.Core.AI.Evolution.ReleaseManager;
using TractorGame.Core.AI.Evolution.Reporting;

namespace TractorGame.Core.AI.Evolution
{
    public sealed class EvolutionRunner
    {
        private readonly ChampionRegistry _championRegistry = new();
        private readonly EvolutionStateStore _stateStore = new();
        private readonly CooldownManager _cooldownManager = new();
        private readonly PromotionContract _promotion = new();
        private readonly CandidateFactory _candidateFactory;
        private readonly ReportWriter _reportWriter = new();
        private readonly DataQualityChecker _qualityChecker = new();
        private readonly LogReader _logReader = new();

        public EvolutionRunner(int seed = 0)
        {
            _candidateFactory = new CandidateFactory(seed);
        }

        public EvolutionRunResult RunOneGeneration(EvolutionConfig config, CancellationToken cancellationToken = default)
        {
            using var mutex = CreateProcessMutex(config);
            var lockAcquired = mutex.WaitOne(TimeSpan.FromSeconds(30));
            if (!lockAcquired)
                throw new TimeoutException("Evolution runner is busy with another process.");

            try
            {
                EvolutionPaths.EnsureEvolutionDirectories(config);

                var startedAt = DateTime.UtcNow;
                var champion = _championRegistry.LoadOrCreateSeed(config);
                var state = _stateStore.Load(config, champion.GenomeHash);
                var generation = Math.Max(config.GenerationNumber, state.Generation + 1);
                config.GenerationNumber = generation;

                var candidateCount = config.ResolveCandidateCount(state.ConsecutiveNoPromotion);
                var candidates = _candidateFactory.Generate(
                    champion.Parameters,
                    champion.GenomeHash,
                    generation,
                    candidateCount,
                    config);

                _reportWriter.WriteCandidateParameters(config, candidates);

                var evaluator = new LayeredEvaluator(config);
                var layered = evaluator.Evaluate(champion.Parameters, candidates, cancellationToken);

                var inCooldown = _cooldownManager.IsInCooldown(state);
                var decision = _promotion.Decide(layered.Layer3, generation, inCooldown);

                var result = new EvolutionRunResult
                {
                    Generation = generation,
                    StartedAtUtc = startedAt,
                    FinishedAtUtc = DateTime.UtcNow,
                    CandidateCount = candidates.Count,
                    ChampionBeforeHash = champion.GenomeHash,
                    Promoted = decision.Promote,
                    PromotionReason = decision.Reason,
                    BestCandidate = decision.Winner,
                    FinalEvaluations = layered.Layer3
                };

                if (decision.Promote && decision.Winner != null)
                {
                    champion = _championRegistry.Promote(config, decision.Winner);
                    state.ConsecutiveNoPromotion = 0;
                    state.CurrentChampionHash = champion.GenomeHash;
                    _cooldownManager.ClearCooldown(state);
                }
                else
                {
                    state.ConsecutiveNoPromotion++;
                }

                state.Generation = generation;
                _stateStore.Save(config, state);

                result.ChampionAfterHash = champion.GenomeHash;
                result.DataQualityReport = BuildDataQualityReport(config, generation);
                result.FinishedAtUtc = DateTime.UtcNow;
                result.ReportPath = _reportWriter.WriteGenerationReport(
                    config,
                    result,
                    layered.Layer1,
                    layered.Layer2,
                    layered.Layer3);

                _championRegistry.AppendRegistryEntry(config, new
                {
                    ts_utc = DateTime.UtcNow,
                    action = "generation_complete",
                    generation = generation,
                    promoted = result.Promoted,
                    champion_before = result.ChampionBeforeHash,
                    champion_after = result.ChampionAfterHash,
                    reason = result.PromotionReason,
                    report = result.ReportPath
                });

                return result;
            }
            finally
            {
                if (lockAcquired)
                    mutex.ReleaseMutex();
            }
        }

        public IReadOnlyList<EvolutionRunResult> RunContinuous(
            EvolutionConfig config,
            int rounds,
            CancellationToken cancellationToken = default)
        {
            var count = Math.Max(1, rounds);
            var results = new List<EvolutionRunResult>(count);
            for (var i = 0; i < count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                results.Add(RunOneGeneration(config, cancellationToken));
            }

            return results;
        }

        private DataQualityReport BuildDataQualityReport(EvolutionConfig config, int generation)
        {
            var eventDirs = new List<string> { config.LogsPath };

            var events = eventDirs
                .Where(System.IO.Directory.Exists)
                .SelectMany(dir => _logReader.ReadEventsFromDirectory(dir))
                .Where(evt => BelongsToGeneration(evt, generation))
                .ToList();

            var report = _qualityChecker.Check(events);
            report.Generation = generation;
            return report;
        }

        private static bool BelongsToGeneration(GameEvent evt, int generation)
        {
            var marker = $"_g{generation}_";
            if (!string.IsNullOrWhiteSpace(evt.GameId) && evt.GameId.Contains(marker, StringComparison.Ordinal))
                return true;

            if (!string.IsNullOrWhiteSpace(evt.RoundId) && evt.RoundId.Contains(marker, StringComparison.Ordinal))
                return true;

            return false;
        }

        private static Mutex CreateProcessMutex(EvolutionConfig config)
        {
            var key = Math.Abs((config.RepoRoot ?? string.Empty).GetHashCode());
            return new Mutex(false, $"tractor_evolution_runner_{key}");
        }
    }
}
