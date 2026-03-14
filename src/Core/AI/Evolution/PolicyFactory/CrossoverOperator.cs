using System;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class CrossoverOperator
    {
        private readonly Random _rng;

        public CrossoverOperator(int seed = 0)
        {
            _rng = seed == 0 ? new Random() : new Random(seed);
        }

        public AIStrategyParameters Crossover(AIStrategyParameters parentA, AIStrategyParameters parentB)
        {
            var child = new AIStrategyParameters();
            var props = typeof(AIStrategyParameters).GetProperties();
            var blocks = ParameterGenome.GetBlocks();

            foreach (var block in blocks)
            {
                var fromA = _rng.NextDouble() < 0.5;
                var source = fromA ? parentA : parentB;

                foreach (var propertyName in block.Value)
                {
                    var prop = Array.Find(props, p => p.Name == propertyName);
                    if (prop == null)
                        continue;

                    prop.SetValue(child, prop.GetValue(source));
                }
            }

            return child.Normalize();
        }
    }
}
