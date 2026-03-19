using System.Collections.Generic;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 记牌快照（确定事实 + 概率事实容器）。
    /// </summary>
    public sealed class MemorySnapshotV30
    {
        public Dictionary<string, int> PlayedCountByCard { get; init; } = new();

        public Dictionary<int, List<string>> VoidSuitsByPlayer { get; init; } = new();

        public Dictionary<int, List<string>> NoPairEvidence { get; init; } = new();

        public Dictionary<int, List<string>> NoTractorEvidence { get; init; } = new();

        public List<string> KnownBottomCards { get; init; } = new();

        /// <summary>
        /// 已出分总和（确定事实）。
        /// </summary>
        public int PlayedScoreTotal { get; init; }

        /// <summary>
        /// 已出分牌张数（确定事实）。
        /// </summary>
        public int PlayedScoreCardCount { get; init; }

        /// <summary>
        /// 概率事实容器，供 Memory 模块映射（例如 has_suit_prob:p2:Heart）。
        /// </summary>
        public Dictionary<string, double> ProbabilisticFacts { get; init; } = new();
    }
}

