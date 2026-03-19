using System;
using System.Globalization;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// Rule AI 开关：默认启用 V2.1，可通过配置或环境变量回退到 legacy。
    /// </summary>
    public sealed class RuleAIOptions
    {
        public bool UseRuleAIV30 { get; init; } = false;

        public bool UseRuleAIV21 { get; init; } = true;

        public bool EnableShadowCompare { get; init; } = true;

        public double ShadowSampleRate { get; init; } = 1.0;

        public bool DecisionTraceEnabled { get; init; } = true;

        public bool DecisionTraceIncludeTruthSnapshot { get; init; } = true;

        /// <summary>
        /// 0 表示不限制，保留全部候选。
        /// </summary>
        public int DecisionTraceMaxCandidates { get; init; } = 0;

        public static RuleAIOptions Default => new();

        public static RuleAIOptions FromEnvironment()
        {
            return Create(
                useRuleAIV30: ReadBool("TRACTOR_RULE_AI_V30_USE_NEW_PATH"),
                useRuleAIV21: ReadBool("TRACTOR_RULE_AI_V21_USE_NEW_PATH"),
                enableShadowCompare: ReadBool("TRACTOR_RULE_AI_V21_SHADOW_MODE"),
                shadowSampleRate: ReadRate("TRACTOR_RULE_AI_V21_SHADOW_RATE"),
                decisionTraceEnabled: ReadBool("TRACTOR_RULE_AI_V21_DECISION_TRACE_ENABLED"),
                decisionTraceIncludeTruthSnapshot: ReadBool("TRACTOR_RULE_AI_V21_DECISION_TRACE_INCLUDE_TRUTH"),
                decisionTraceMaxCandidates: ReadInt("TRACTOR_RULE_AI_V21_DECISION_TRACE_MAX_CANDIDATES"));
        }

        public static RuleAIOptions Create(
            bool? useRuleAIV30 = null,
            bool? useRuleAIV21 = null,
            bool? enableShadowCompare = null,
            double? shadowSampleRate = null,
            bool? decisionTraceEnabled = null,
            bool? decisionTraceIncludeTruthSnapshot = null,
            int? decisionTraceMaxCandidates = null,
            RuleAIOptions? fallback = null)
        {
            fallback ??= Default;

            return new RuleAIOptions
            {
                UseRuleAIV30 = useRuleAIV30 ?? fallback.UseRuleAIV30,
                UseRuleAIV21 = useRuleAIV21 ?? fallback.UseRuleAIV21,
                EnableShadowCompare = enableShadowCompare ?? fallback.EnableShadowCompare,
                ShadowSampleRate = ClampRate(shadowSampleRate ?? fallback.ShadowSampleRate),
                DecisionTraceEnabled = decisionTraceEnabled ?? fallback.DecisionTraceEnabled,
                DecisionTraceIncludeTruthSnapshot = decisionTraceIncludeTruthSnapshot ?? fallback.DecisionTraceIncludeTruthSnapshot,
                DecisionTraceMaxCandidates = ClampCandidateLimit(decisionTraceMaxCandidates ?? fallback.DecisionTraceMaxCandidates)
            };
        }

        private static bool? ReadBool(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            return value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
        }

        private static double? ReadRate(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
                return null;

            return ClampRate(parsed);
        }

        private static double ClampRate(double rate)
        {
            return Math.Clamp(rate, 0, 1);
        }

        private static int? ReadInt(string name)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (string.IsNullOrWhiteSpace(value))
                return null;

            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                return null;

            return ClampCandidateLimit(parsed);
        }

        private static int ClampCandidateLimit(int value)
        {
            return Math.Max(0, value);
        }
    }
}
