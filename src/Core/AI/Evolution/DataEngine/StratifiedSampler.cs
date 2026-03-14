using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.Evolution.DataEngine
{
    public sealed class StratifiedSampler
    {
        private readonly Dictionary<string, List<TrainingSample>> _buckets = new();
        private readonly Random _rng;

        public StratifiedSampler(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public void AddSample(TrainingSample sample)
        {
            var key = BuildBucketKey(sample);
            if (!_buckets.TryGetValue(key, out var list))
            {
                list = new List<TrainingSample>();
                _buckets[key] = list;
            }

            list.Add(sample);
        }

        public List<TrainingSample> Sample(int totalCount, double longTailRatio)
        {
            if (totalCount <= 0 || _buckets.Count == 0)
                return new List<TrainingSample>();

            var result = new List<TrainingSample>(totalCount);
            var quotas = CalculateQuotas(totalCount, longTailRatio);

            foreach (var kv in _buckets)
            {
                quotas.TryGetValue(kv.Key, out var quota);
                if (quota <= 0)
                    continue;

                result.AddRange(WeightedSampleWithoutReplacement(kv.Value, quota));
            }

            if (result.Count < totalCount)
            {
                var all = _buckets.Values.SelectMany(v => v).ToList();
                var missing = totalCount - result.Count;
                result.AddRange(WeightedSampleWithoutReplacement(all, missing));
            }

            return result.Take(totalCount).ToList();
        }

        private Dictionary<string, int> CalculateQuotas(int totalCount, double longTailRatio)
        {
            var quotas = _buckets.Keys.ToDictionary(key => key, _ => 0);
            var avgSize = _buckets.Values.Average(v => v.Count);
            var longTailThreshold = avgSize * 0.30;

            var longTailBuckets = _buckets
                .Where(kv => kv.Value.Count <= longTailThreshold)
                .Select(kv => kv.Key)
                .ToList();

            var longTailQuota = (int)Math.Round(totalCount * Math.Clamp(longTailRatio, 0, 0.9));
            if (longTailBuckets.Count == 0)
                longTailQuota = 0;

            if (longTailQuota > 0)
            {
                var baseQuota = longTailQuota / longTailBuckets.Count;
                var remainder = longTailQuota % longTailBuckets.Count;
                foreach (var key in longTailBuckets)
                    quotas[key] = baseQuota;

                for (var i = 0; i < remainder; i++)
                    quotas[longTailBuckets[i % longTailBuckets.Count]]++;
            }

            var remaining = Math.Max(0, totalCount - longTailQuota);
            var normalBuckets = _buckets.Keys.Except(longTailBuckets).ToList();
            if (normalBuckets.Count == 0)
                normalBuckets = _buckets.Keys.ToList();

            var totalSamples = normalBuckets.Sum(k => _buckets[k].Count);
            if (totalSamples <= 0)
                return quotas;

            var assigned = 0;
            foreach (var key in normalBuckets)
            {
                var ratio = (double)_buckets[key].Count / totalSamples;
                var add = (int)Math.Floor(remaining * ratio);
                quotas[key] += add;
                assigned += add;
            }

            var left = totalCount - quotas.Values.Sum();
            if (left > 0)
            {
                foreach (var key in normalBuckets.OrderByDescending(k => _buckets[k].Count))
                {
                    if (left == 0)
                        break;

                    quotas[key]++;
                    left--;
                }
            }

            return quotas;
        }

        private List<TrainingSample> WeightedSampleWithoutReplacement(List<TrainingSample> input, int count)
        {
            if (count <= 0 || input.Count == 0)
                return new List<TrainingSample>();

            if (count >= input.Count)
                return input.OrderByDescending(s => s.FreshnessWeight).ToList();

            var pool = new List<TrainingSample>(input);
            var result = new List<TrainingSample>(count);

            for (var i = 0; i < count && pool.Count > 0; i++)
            {
                var sum = pool.Sum(s => Math.Max(1e-6, s.FreshnessWeight));
                var p = _rng.NextDouble() * sum;
                var cursor = 0.0;
                var selectedIndex = 0;

                for (var j = 0; j < pool.Count; j++)
                {
                    cursor += Math.Max(1e-6, pool[j].FreshnessWeight);
                    if (cursor >= p)
                    {
                        selectedIndex = j;
                        break;
                    }
                }

                result.Add(pool[selectedIndex]);
                pool.RemoveAt(selectedIndex);
            }

            return result;
        }

        private static string BuildBucketKey(TrainingSample sample)
        {
            return string.Join("_", sample.Difficulty, sample.Role, sample.Phase, sample.Pattern);
        }
    }
}
