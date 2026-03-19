using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 手牌结构摘要（仅保留首批冻结策略需要字段）。
    /// </summary>
    public sealed class HandProfileV30
    {
        public int TrumpCount { get; init; }

        public int HighTrumpCount { get; init; }

        public int JokerCount { get; init; }

        public int LevelCardCount { get; init; }

        public int TrumpPairCount { get; init; }

        public int TrumpTractorCount { get; init; }

        public int ScoreCardCount { get; init; }

        public bool HasControlTrump { get; init; }

        public Dictionary<Suit, int> SuitLengths { get; init; } = new();

        public Suit? StrongestSuit { get; init; }

        public Suit? WeakestSuit { get; init; }

        public List<Suit> PotentialVoidTargets { get; init; } = new();

        public string StructureSummary { get; init; } = string.Empty;
    }
}

