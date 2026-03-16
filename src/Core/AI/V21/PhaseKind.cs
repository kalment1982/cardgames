namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// Rule AI 统一阶段标识（M1 最小集）。
    /// </summary>
    public enum PhaseKind
    {
        Unknown = 0,
        Bid = 1,
        BuryBottom = 2,
        Lead = 3,
        Follow = 4
    }
}
