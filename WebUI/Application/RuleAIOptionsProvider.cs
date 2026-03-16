using System;
using System.Globalization;
using Microsoft.Extensions.Configuration;
using TractorGame.Core.AI.V21;

namespace WebUI.Application;

public sealed class RuleAIOptionsProvider
{
    public RuleAIOptionsProvider(IConfiguration configuration)
    {
        var section = configuration.GetSection("RuleAI");
        Options = RuleAIOptions.Create(
            useRuleAIV21: ReadBool(section, "UseRuleAIV21"),
            enableShadowCompare: ReadBool(section, "EnableShadowCompare"),
            shadowSampleRate: ReadRate(section, "ShadowSampleRate"),
            decisionTraceEnabled: ReadBool(section, "DecisionTraceEnabled"),
            decisionTraceIncludeTruthSnapshot: ReadBool(section, "DecisionTraceIncludeTruthSnapshot"),
            decisionTraceMaxCandidates: ReadInt(section, "DecisionTraceMaxCandidates"),
            fallback: RuleAIOptions.FromEnvironment());
    }

    public RuleAIOptions Options { get; }

    private static bool? ReadBool(IConfiguration section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    private static double? ReadRate(IConfiguration section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            return null;

        return Math.Clamp(parsed, 0, 1);
    }

    private static int? ReadInt(IConfiguration section, string key)
    {
        var value = section[key];
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
            return null;

        return Math.Max(0, parsed);
    }
}
