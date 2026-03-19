using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 候选动作评分结果。
    /// </summary>
    public sealed class ScoredActionV30
    {
        public List<Card> Cards { get; init; } = new();

        public double Score { get; init; }

        public string ReasonCode { get; init; } = string.Empty;

        public Dictionary<string, double> Features { get; init; } = new();

        public WinSecurityLevelV30 WinSecurity { get; init; } = WinSecurityLevelV30.Unknown;
    }
}

