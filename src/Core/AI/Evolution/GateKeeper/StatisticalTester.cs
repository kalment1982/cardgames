using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.Evolution.GateKeeper
{
    public sealed class StatisticalTester
    {
        private readonly Random _rng;

        public StatisticalTester(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public (double low, double high) BootstrapWinRateCI(
            IReadOnlyList<int> outcomes,
            int iterations,
            double confidenceLevel)
        {
            if (outcomes == null || outcomes.Count == 0)
                return (0, 0);

            iterations = Math.Max(100, iterations);
            confidenceLevel = Math.Clamp(confidenceLevel, 0.5, 0.999);

            var samples = new double[iterations];
            for (var i = 0; i < iterations; i++)
            {
                var win = 0;
                for (var j = 0; j < outcomes.Count; j++)
                {
                    var idx = _rng.Next(outcomes.Count);
                    win += outcomes[idx];
                }

                samples[i] = (double)win / outcomes.Count;
            }

            Array.Sort(samples);
            var alpha = 1.0 - confidenceLevel;
            var lowIndex = (int)Math.Floor((alpha / 2) * (samples.Length - 1));
            var highIndex = (int)Math.Ceiling((1 - alpha / 2) * (samples.Length - 1));
            lowIndex = Math.Clamp(lowIndex, 0, samples.Length - 1);
            highIndex = Math.Clamp(highIndex, 0, samples.Length - 1);
            return (samples[lowIndex], samples[highIndex]);
        }

        public (double mean, double low, double high) BootstrapDifferenceCI(
            IReadOnlyList<int> candidateOutcomes,
            IReadOnlyList<int> championOutcomes,
            int iterations,
            double confidenceLevel)
        {
            if (candidateOutcomes == null || championOutcomes == null)
                return (0, 0, 0);

            var count = Math.Min(candidateOutcomes.Count, championOutcomes.Count);
            if (count <= 0)
                return (0, 0, 0);

            iterations = Math.Max(100, iterations);
            confidenceLevel = Math.Clamp(confidenceLevel, 0.5, 0.999);

            var diffs = new double[count];
            for (var i = 0; i < count; i++)
                diffs[i] = candidateOutcomes[i] - championOutcomes[i];

            var samples = new double[iterations];
            for (var i = 0; i < iterations; i++)
            {
                var sum = 0.0;
                for (var j = 0; j < count; j++)
                {
                    var idx = _rng.Next(count);
                    sum += diffs[idx];
                }

                samples[i] = sum / count;
            }

            Array.Sort(samples);
            var mean = samples.Average();
            var alpha = 1.0 - confidenceLevel;
            var lowIndex = (int)Math.Floor((alpha / 2) * (samples.Length - 1));
            var highIndex = (int)Math.Ceiling((1 - alpha / 2) * (samples.Length - 1));
            lowIndex = Math.Clamp(lowIndex, 0, samples.Length - 1);
            highIndex = Math.Clamp(highIndex, 0, samples.Length - 1);
            return (mean, samples[lowIndex], samples[highIndex]);
        }

        public IReadOnlyList<int> ToWinOutcomes(int wins, int games)
        {
            wins = Math.Clamp(wins, 0, games);
            var outcomes = Enumerable.Repeat(1, wins)
                .Concat(Enumerable.Repeat(0, Math.Max(0, games - wins)))
                .ToList();
            return outcomes;
        }

        public IReadOnlyList<int> BenjaminiHochbergTopK(
            IReadOnlyList<(string id, double pValue, double score)> rows,
            int maxKeep,
            double fdr)
        {
            if (rows == null || rows.Count == 0)
                return Array.Empty<int>();

            var ranked = rows
                .Select((r, idx) => new { idx, r.id, r.pValue, r.score })
                .OrderBy(x => x.pValue)
                .ToList();

            var thresholdIndex = -1;
            for (var i = 0; i < ranked.Count; i++)
            {
                var critical = ((i + 1) / (double)ranked.Count) * fdr;
                if (ranked[i].pValue <= critical)
                    thresholdIndex = i;
            }

            var keep = new List<int>();
            if (thresholdIndex >= 0)
            {
                keep.AddRange(ranked.Take(thresholdIndex + 1).Select(x => x.idx));
            }

            if (keep.Count < maxKeep)
            {
                var extra = rows
                    .Select((r, idx) => new { idx, r.score })
                    .OrderByDescending(x => x.score)
                    .Select(x => x.idx)
                    .Where(idx => !keep.Contains(idx))
                    .Take(maxKeep - keep.Count);
                keep.AddRange(extra);
            }

            return keep.Take(maxKeep).ToList();
        }

        public double ApproximateBinomialPValue(double winRate, int games)
        {
            if (games <= 0)
                return 1.0;

            // Normal approximation for H0: p=0.5.
            var mean = 0.5;
            var std = Math.Sqrt(mean * (1 - mean) / games);
            if (std <= 0)
                return 1.0;

            var z = Math.Abs(winRate - mean) / std;
            return 2 * (1 - NormalCdf(z));
        }

        private static double NormalCdf(double z)
        {
            // Abramowitz-Stegun approximation.
            var t = 1.0 / (1.0 + 0.2316419 * z);
            var d = 0.3989423 * Math.Exp(-z * z / 2.0);
            var prob = d * t * (0.3193815 + t * (-0.3565638 + t * (1.781478 + t * (-1.821256 + t * 1.330274))));
            return 1 - prob;
        }
    }
}
