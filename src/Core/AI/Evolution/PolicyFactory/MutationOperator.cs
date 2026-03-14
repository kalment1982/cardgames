using System;
using System.Collections.Generic;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class MutationOperator
    {
        private static readonly HashSet<string> RandomnessProperties = new(StringComparer.Ordinal)
        {
            nameof(AIStrategyParameters.EasyRandomnessRate),
            nameof(AIStrategyParameters.MediumRandomnessRate),
            nameof(AIStrategyParameters.HardRandomnessRate),
            nameof(AIStrategyParameters.ExpertRandomnessRate)
        };

        private readonly Random _rng;

        public MutationOperator(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public AIStrategyParameters Mutate(AIStrategyParameters parent, bool exploratory)
        {
            var child = parent.Clone();
            var props = typeof(AIStrategyParameters).GetProperties();

            var mutateProb = exploratory ? 0.35 : 0.25;
            var strategySigma = exploratory ? 0.07 : 0.05;
            var randomnessSigma = exploratory ? 0.05 : 0.03;

            foreach (var prop in props)
            {
                if (prop.PropertyType == typeof(double))
                {
                    if (_rng.NextDouble() > mutateProb)
                        continue;

                    var current = (double)(prop.GetValue(child) ?? 0.0);
                    var sigma = RandomnessProperties.Contains(prop.Name) ? randomnessSigma : strategySigma;
                    var mutated = current + SampleGaussian(0, sigma);
                    prop.SetValue(child, mutated);
                }
                else if (prop.PropertyType == typeof(int) && prop.Name == nameof(AIStrategyParameters.LeadThrowMinAdvantage))
                {
                    if (_rng.NextDouble() > mutateProb)
                        continue;

                    var current = (int)(prop.GetValue(child) ?? 0);
                    var delta = _rng.NextDouble() < 0.5 ? -1 : 1;
                    prop.SetValue(child, Math.Clamp(current + delta, 0, 3));
                }
            }

            return child.Normalize();
        }

        private double SampleGaussian(double mean, double stddev)
        {
            var u1 = 1.0 - _rng.NextDouble();
            var u2 = 1.0 - _rng.NextDouble();
            var stdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
            return mean + stddev * stdNormal;
        }
    }
}
