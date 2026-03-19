namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 风险等级。
    /// </summary>
    public enum RiskLevelV30
    {
        None = 0,
        Low = 1,
        Medium = 2,
        High = 3
    }

    /// <summary>
    /// 比分压力等级。
    /// </summary>
    public enum ScorePressureLevelV30
    {
        Relaxed = 0,
        Tight = 1,
        Critical = 2
    }

    /// <summary>
    /// 末盘等级。
    /// </summary>
    public enum EndgameLevelV30
    {
        None = 0,
        Late = 1,
        FinalThree = 2,
        LastTrickRace = 3
    }

    /// <summary>
    /// 赢牌安全等级（Memory-006 需要）。
    /// </summary>
    public enum WinSecurityLevelV30
    {
        Unknown = 0,
        FragileWin = 1,
        StableWin = 2,
        LockWin = 3
    }
}

