namespace TractorGame.Core.AI.V21
{
    using System.Collections.Generic;

    /// <summary>
    /// 从上层对局流程透传到 AI 决策日志的上下文。
    /// </summary>
    public sealed class AIDecisionLogContext
    {
        public string? SessionId { get; init; }

        public string? GameId { get; init; }

        public string? RoundId { get; init; }

        public string? TrickId { get; init; }

        public string? TurnId { get; init; }

        public int? PlayerIndex { get; init; }

        public string? Actor { get; init; }

        public string? DecisionTraceId { get; init; }

        public int? TrickIndex { get; init; }

        public int? TurnIndex { get; init; }

        public int? PlayPosition { get; init; }

        public int? DealerIndex { get; init; }

        public int? CurrentWinningPlayer { get; init; }

        public int? DefenderScore { get; init; }

        public int? BottomPoints { get; init; }

        public Dictionary<string, object?>? TruthSnapshot { get; init; }
    }
}
