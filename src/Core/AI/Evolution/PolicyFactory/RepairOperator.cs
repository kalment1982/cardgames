using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class RepairOperator
    {
        private readonly IReadOnlyList<GeneSpec> _geneSpecs;

        public RepairOperator()
        {
            _geneSpecs = BuildDefaultGeneSpecs();
        }

        public AIStrategyParameters Repair(AIStrategyParameters source)
        {
            var p = source.Clone();

            // Step 1: clamp and round all floating values.
            foreach (var spec in _geneSpecs)
            {
                var prop = typeof(AIStrategyParameters).GetProperty(spec.Name);
                if (prop == null)
                    continue;

                if (prop.PropertyType == typeof(double))
                {
                    var value = (double)(prop.GetValue(p) ?? 0.0);
                    value = Math.Clamp(value, spec.Min, spec.Max);
                    value = Math.Round(value, spec.FloatPrecision);
                    prop.SetValue(p, value);
                }
            }

            // Step 2: semantic monotonic constraints.
            EnforceDescending(ref p,
                nameof(AIStrategyParameters.EasyRandomnessRate),
                nameof(AIStrategyParameters.MediumRandomnessRate),
                nameof(AIStrategyParameters.HardRandomnessRate),
                nameof(AIStrategyParameters.ExpertRandomnessRate));

            // LeadThrowMinAdvantage is discrete.
            p.LeadThrowMinAdvantage = Math.Clamp(p.LeadThrowMinAdvantage, 0, 3);

            return p.Normalize();
        }

        private static void EnforceDescending(ref AIStrategyParameters p, params string[] names)
        {
            var props = names
                .Select(name => typeof(AIStrategyParameters).GetProperty(name))
                .Where(prop => prop != null)
                .ToList();

            for (var i = 0; i < props.Count - 1; i++)
            {
                var left = (double)(props[i]?.GetValue(p) ?? 0.0);
                var right = (double)(props[i + 1]?.GetValue(p) ?? 0.0);
                if (left < right)
                    props[i + 1]?.SetValue(p, left);
            }
        }

        private static IReadOnlyList<GeneSpec> BuildDefaultGeneSpecs()
        {
            var specs = new List<GeneSpec>();
            foreach (var prop in typeof(AIStrategyParameters).GetProperties())
            {
                if (prop.PropertyType == typeof(double))
                {
                    specs.Add(new GeneSpec
                    {
                        Name = prop.Name,
                        Min = 0,
                        Max = 1,
                        Constraint = MonotonicConstraint.None,
                        FloatPrecision = 6
                    });
                }
            }

            return specs;
        }
    }
}
