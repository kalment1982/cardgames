using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using TractorGame.Core.AI;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class ParameterGenome
    {
        private static readonly Dictionary<string, string[]> Blocks = new()
        {
            ["randomness"] = new[]
            {
                nameof(AIStrategyParameters.EasyRandomnessRate),
                nameof(AIStrategyParameters.MediumRandomnessRate),
                nameof(AIStrategyParameters.HardRandomnessRate),
                nameof(AIStrategyParameters.ExpertRandomnessRate)
            },
            ["lead"] = new[]
            {
                nameof(AIStrategyParameters.LeadThrowAggressiveness),
                nameof(AIStrategyParameters.LeadThrowMinAdvantage),
                nameof(AIStrategyParameters.LeadConservativeBias),
                nameof(AIStrategyParameters.LeadTractorPriority)
            },
            ["follow"] = new[]
            {
                nameof(AIStrategyParameters.FollowTrumpCutPriority),
                nameof(AIStrategyParameters.FollowStructureStrictness),
                nameof(AIStrategyParameters.FollowSaveTrumpBias),
                nameof(AIStrategyParameters.FollowBeatAttemptBias)
            },
            ["point"] = new[]
            {
                nameof(AIStrategyParameters.PointCardProtectionWeight),
                nameof(AIStrategyParameters.PartnerSupportPointBias),
                nameof(AIStrategyParameters.OpponentDenyPointBias)
            },
            ["endgame"] = new[]
            {
                nameof(AIStrategyParameters.BuryPointRisk),
                nameof(AIStrategyParameters.BuryTrumpProtection),
                nameof(AIStrategyParameters.EndgameFinishBias),
                nameof(AIStrategyParameters.EndgameStabilityBias)
            }
        };

        public ParameterGenome(AIStrategyParameters parameters)
        {
            Parameters = parameters?.Clone() ?? AIStrategyParameters.CreateDefault();
        }

        public AIStrategyParameters Parameters { get; }

        public string ComputeHash(int precision = 6)
        {
            var props = typeof(AIStrategyParameters)
                .GetProperties()
                .OrderBy(p => p.Name)
                .ToList();

            var values = new List<string>(props.Count);
            foreach (var prop in props)
            {
                var value = prop.GetValue(Parameters);
                if (value is double d)
                    values.Add(Math.Round(d, precision).ToString($"F{precision}", System.Globalization.CultureInfo.InvariantCulture));
                else
                    values.Add(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
            }

            var payload = string.Join("|", values);
            var bytes = Encoding.UTF8.GetBytes(payload);
            using var sha = SHA256.Create();
            return Convert.ToHexString(sha.ComputeHash(bytes));
        }

        public static IReadOnlyDictionary<string, string[]> GetBlocks()
        {
            return Blocks;
        }
    }
}
