namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// V30 主/次意图枚举。
    /// </summary>
    public enum DecisionIntentKindV30
    {
        Unknown = 0,
        TakeScore = 1,
        ProtectBottom = 2,
        SaveControl = 3,
        PassToMate = 4,
        ForceTrump = 5,
        ShapeHand = 6,
        PreserveStructure = 7,
        TakeLead = 8,
        MinimizeLoss = 9,
        PrepareEndgame = 10,
        PrepareThrow = 11,
        AttackLongSuit = 12,
        ProbeWeakSuit = 13
    }
}

