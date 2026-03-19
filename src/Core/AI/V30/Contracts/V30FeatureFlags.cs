namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// V30 合同层特性开关。
    /// </summary>
    public sealed class V30FeatureFlags
    {
        /// <summary>
        /// 严格合同校验：输入缺失直接抛异常。
        /// </summary>
        public bool StrictContractValidation { get; init; } = true;

        /// <summary>
        /// 概率口径阈值，默认 70%。
        /// </summary>
        public double ProbabilityThreshold { get; init; } = 0.70;

        /// <summary>
        /// 无明确信号时的底牌默认估分。
        /// </summary>
        public int DefaultBottomEstimatePoints { get; init; } = 10;

        /// <summary>
        /// 底牌高分信号上调最大值。
        /// </summary>
        public int BottomSignalBoostCap { get; init; } = 20;

        /// <summary>
        /// 是否根据记牌信号做底牌估分上调。
        /// </summary>
        public bool EnableBottomSignalBoost { get; init; } = true;

        /// <summary>
        /// 默认特性开关集合。
        /// </summary>
        public static V30FeatureFlags Default => new V30FeatureFlags();
    }
}

