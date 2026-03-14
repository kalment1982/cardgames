using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Evolution;
using TractorGame.Core.AI.Evolution.GateKeeper;

namespace TractorGame.Core.AI.Evolution.LeagueArena
{
    public sealed class LayeredEvaluationResult
    {
        public IReadOnlyList<CandidateEvaluation> Layer1 { get; set; } = Array.Empty<CandidateEvaluation>();
        public IReadOnlyList<CandidateEvaluation> Layer2 { get; set; } = Array.Empty<CandidateEvaluation>();
        public IReadOnlyList<CandidateEvaluation> Layer3 { get; set; } = Array.Empty<CandidateEvaluation>();
    }

    public sealed class LayeredEvaluator
    {
        private readonly EvolutionConfig _config;
        private readonly SelfPlayEngine _selfPlay;
        private readonly StatisticalTester _stats;

        public LayeredEvaluator(EvolutionConfig config)
        {
            _config = config;
            _selfPlay = new SelfPlayEngine(config);
            _stats = new StatisticalTester(config.GenerationNumber + 1000);
        }

        public LayeredEvaluationResult Evaluate(
            AIStrategyParameters champion,
            IReadOnlyList<CandidateProfile> candidates,
            CancellationToken cancellationToken)
        {
            var layer1 = candidates
                .Select((candidate, idx) => EvaluateCandidate(champion, candidate, _config.Layer1GamesPerCandidate, idx * 1000, "layer1", cancellationToken))
                .ToList();

            var layer1Selected = SelectLayer1(layer1);
            var layer2 = layer1Selected
                .Select((candidate, idx) => EvaluateCandidate(champion, candidate.Candidate, _config.Layer2GamesPerCandidate, 100000 + idx * 1000, "layer2", cancellationToken))
                .ToList();

            var finalists = layer2
                .OrderByDescending(c => c.WinRate)
                .ThenBy(c => c.CandidateP99LatencyMs)
                .Take(Math.Max(1, _config.Layer2TopK))
                .Select(c => c.Candidate)
                .ToList();

            var layer3 = finalists
                .Select((candidate, idx) => EvaluateFinalCandidate(champion, candidate, idx, cancellationToken))
                .ToList();

            return new LayeredEvaluationResult
            {
                Layer1 = layer1,
                Layer2 = layer2,
                Layer3 = layer3
            };
        }

        private List<CandidateEvaluation> SelectLayer1(IReadOnlyList<CandidateEvaluation> layer1)
        {
            var keep = Math.Max(1, _config.Layer1TopK);
            if (_config.Layer1Selection == Layer1SelectionMode.TopK)
            {
                return layer1
                    .OrderByDescending(c => c.WinRate)
                    .ThenBy(c => c.CandidateP99LatencyMs)
                    .Take(keep)
                    .ToList();
            }

            var rows = layer1
                .Select(item => (item.Candidate.CandidateId, _stats.ApproximateBinomialPValue(item.WinRate, item.Games), item.WinRate))
                .ToList();
            var selectedIndexes = _stats.BenjaminiHochbergTopK(rows, keep, _config.Layer1BhFdr);
            return selectedIndexes.Select(idx => layer1[idx]).ToList();
        }

        private CandidateEvaluation EvaluateFinalCandidate(
            AIStrategyParameters champion,
            CandidateProfile candidate,
            int index,
            CancellationToken cancellationToken)
        {
            var merged = new SelfPlayAggregate();
            for (var seedIdx = 0; seedIdx < Math.Max(1, _config.Layer3Seeds); seedIdx++)
            {
                var seedBase = 200000 + index * 10000 + seedIdx * 1000;
                var round = _selfPlay.Evaluate(candidate.Parameters, champion, _config.Layer3GamesPerSeed, seedBase, cancellationToken);
                merged.Outcomes.AddRange(round.Outcomes);
            }

            return BuildEvaluation(candidate, merged, "layer3");
        }

        private CandidateEvaluation EvaluateCandidate(
            AIStrategyParameters champion,
            CandidateProfile candidate,
            int games,
            int seedBase,
            string layer,
            CancellationToken cancellationToken)
        {
            var aggregate = _selfPlay.Evaluate(candidate.Parameters, champion, games, seedBase, cancellationToken);
            return BuildEvaluation(candidate, aggregate, layer);
        }

        private CandidateEvaluation BuildEvaluation(CandidateProfile candidate, SelfPlayAggregate aggregate, string layer)
        {
            var outcomes = aggregate.Outcomes.Select(x => x.CandidateWon ? 1 : 0).ToList();
            var (low, high) = _stats.BootstrapWinRateCI(outcomes, _config.BootstrapIterations, _config.ConfidenceLevel);

            return new CandidateEvaluation
            {
                Candidate = candidate,
                Games = aggregate.Games,
                Wins = aggregate.CandidateWins,
                CandidateDecisions = aggregate.CandidateDecisions,
                CandidateIllegalDecisions = aggregate.CandidateIllegalDecisions,
                OpponentDecisions = aggregate.OpponentDecisions,
                OpponentIllegalDecisions = aggregate.OpponentIllegalDecisions,
                CandidateAvgLatencyMs = aggregate.CandidateAvgLatencyMs,
                CandidateP99LatencyMs = aggregate.CandidateP99LatencyMs,
                OpponentAvgLatencyMs = aggregate.OpponentAvgLatencyMs,
                OpponentP99LatencyMs = aggregate.OpponentP99LatencyMs,
                CandidateDiversity = aggregate.CandidateDiversity,
                OpponentDiversity = aggregate.OpponentDiversity,
                CandidateDealerSideGames = aggregate.CandidateDealerSideGames,
                CandidateDealerSideWins = aggregate.CandidateDealerSideWins,
                CandidateDefenderSideGames = aggregate.CandidateDefenderSideGames,
                CandidateDefenderSideWins = aggregate.CandidateDefenderSideWins,
                WinRateCiLow = low,
                WinRateCiHigh = high,
                Layer = layer
            };
        }
    }
}
