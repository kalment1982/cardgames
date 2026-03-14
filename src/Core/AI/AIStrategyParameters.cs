using System;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// AI策略参数（v1）
    /// 说明：首批参数先提供可配置入口，后续可由自博弈/进化算法自动调优。
    /// </summary>
    public sealed class AIStrategyParameters
    {
        // 1-4: 随机性/探索
        public double EasyRandomnessRate { get; set; } = 0.35;
        public double MediumRandomnessRate { get; set; } = 0.20;
        public double HardRandomnessRate { get; set; } = 0.075;
        public double ExpertRandomnessRate { get; set; } = 0.025;

        // 5-8: 先手策略
        public double LeadThrowAggressiveness { get; set; } = 0.50;
        public int LeadThrowMinAdvantage { get; set; } = 1;
        public double LeadConservativeBias { get; set; } = 0.50;
        public double LeadTractorPriority { get; set; } = 0.70;

        // 9-12: 跟牌/毙牌策略
        public double FollowTrumpCutPriority { get; set; } = 0.65;
        public double FollowStructureStrictness { get; set; } = 0.80;
        public double FollowSaveTrumpBias { get; set; } = 0.40;
        public double FollowBeatAttemptBias { get; set; } = 0.70;

        // 13-15: 分牌策略
        public double PointCardProtectionWeight { get; set; } = 0.65;
        public double PartnerSupportPointBias { get; set; } = 0.75;
        public double OpponentDenyPointBias { get; set; } = 0.70;

        // 16-19: 扣底/收官策略
        public double BuryPointRisk { get; set; } = 0.30;
        public double BuryTrumpProtection { get; set; } = 0.80;
        public double EndgameFinishBias { get; set; } = 0.60;
        public double EndgameStabilityBias { get; set; } = 0.70;

        // ========== v2: 细粒度跟牌策略（解决"弱智"问题）==========

        // 20-23: 对家赢牌时的垫牌策略
        /// <summary>对家赢牌时，送分牌的优先级（0=不送，1=全送）</summary>
        public double PartnerWinning_GivePointsPriority { get; set; } = 0.85;
        /// <summary>对家赢牌时，垫小牌的优先级（0=垫大牌，1=垫小牌）</summary>
        public double PartnerWinning_DiscardSmallPriority { get; set; } = 0.80;
        /// <summary>对家赢牌时，避免垫主牌的程度（0=随意垫，1=绝不垫）</summary>
        public double PartnerWinning_AvoidTrumpPriority { get; set; } = 0.70;
        /// <summary>对家赢牌时，避免垫对子的程度（0=随意垫，1=保留对子）</summary>
        public double PartnerWinning_KeepPairsPriority { get; set; } = 0.60;

        // 24-27: 争胜时的用牌策略
        /// <summary>能赢时，用最小牌赢的倾向（0=用大牌，1=用最小能赢的牌）</summary>
        public double WinAttempt_UseMinimalCardsPriority { get; set; } = 0.75;
        /// <summary>能赢时，保留控制力的倾向（0=不考虑，1=优先保留大牌）</summary>
        public double WinAttempt_PreserveControlPriority { get; set; } = 0.70;
        /// <summary>能赢时，考虑下轮出牌权的价值（0=不考虑，1=高度重视）</summary>
        public double WinAttempt_NextLeadValueWeight { get; set; } = 0.65;
        /// <summary>领先时，降低争胜欲望的程度（0=照常争，1=保守不争）</summary>
        public double WinAttempt_LeadingConservativeBias { get; set; } = 0.55;

        // 28-31: 无法赢时的垫牌策略
        /// <summary>无法赢时，垫小牌的优先级（0=垫大牌，1=垫小牌）</summary>
        public double CannotWin_DiscardSmallPriority { get; set; } = 0.85;
        /// <summary>无法赢时，避免送分的程度（0=随意送，1=绝不送）</summary>
        public double CannotWin_AvoidPointsPriority { get; set; } = 0.80;
        /// <summary>无法赢时，避免垫主牌的程度（0=随意垫，1=绝不垫）</summary>
        public double CannotWin_AvoidTrumpPriority { get; set; } = 0.75;
        /// <summary>无法赢时，保留长门的倾向（0=不考虑，1=优先垫短门）</summary>
        public double CannotWin_PreserveLongSuitPriority { get; set; } = 0.60;

        // 32-35: 保底策略（庄家方专属）
        /// <summary>收官时，保护底牌的警觉度（0=不关心，1=高度警觉）</summary>
        public double BottomProtection_Alertness { get; set; } = 0.70;
        /// <summary>短门时，避免出牌的程度（0=照常出，1=绝不出）</summary>
        public double BottomProtection_AvoidShortSuitPriority { get; set; } = 0.75;
        /// <summary>收官时，优先出主牌的倾向（0=不优先，1=优先出主）</summary>
        public double BottomProtection_PreferTrumpPriority { get; set; } = 0.65;
        /// <summary>队友短门时，帮忙保底的积极性（0=不帮，1=积极帮）</summary>
        public double BottomProtection_HelpPartnerPriority { get; set; } = 0.60;

        // 36-39: 先手出牌优化
        /// <summary>先手时，出大牌控制的倾向（0=出小牌，1=出大牌）</summary>
        public double Lead_BigCardControlPriority { get; set; } = 0.60;
        /// <summary>先手时，清理短门的倾向（0=不清理，1=优先清理）</summary>
        public double Lead_ClearShortSuitPriority { get; set; } = 0.55;
        /// <summary>先手时，试探对手缺门的积极性（0=不试探，1=积极试探）</summary>
        public double Lead_ProbeOpponentPriority { get; set; } = 0.50;
        /// <summary>领先时，先手出安全牌的倾向（0=激进，1=保守）</summary>
        public double Lead_LeadingSafetyBias { get; set; } = 0.65;

        public AIStrategyParameters Clone()
        {
            return (AIStrategyParameters)MemberwiseClone();
        }

        public AIStrategyParameters Normalize()
        {
            EasyRandomnessRate = Clamp01(EasyRandomnessRate);
            MediumRandomnessRate = Clamp01(MediumRandomnessRate);
            HardRandomnessRate = Clamp01(HardRandomnessRate);
            ExpertRandomnessRate = Clamp01(ExpertRandomnessRate);

            LeadThrowAggressiveness = Clamp01(LeadThrowAggressiveness);
            LeadConservativeBias = Clamp01(LeadConservativeBias);
            LeadTractorPriority = Clamp01(LeadTractorPriority);
            FollowTrumpCutPriority = Clamp01(FollowTrumpCutPriority);
            FollowStructureStrictness = Clamp01(FollowStructureStrictness);
            FollowSaveTrumpBias = Clamp01(FollowSaveTrumpBias);
            FollowBeatAttemptBias = Clamp01(FollowBeatAttemptBias);
            PointCardProtectionWeight = Clamp01(PointCardProtectionWeight);
            PartnerSupportPointBias = Clamp01(PartnerSupportPointBias);
            OpponentDenyPointBias = Clamp01(OpponentDenyPointBias);
            BuryPointRisk = Clamp01(BuryPointRisk);
            BuryTrumpProtection = Clamp01(BuryTrumpProtection);
            EndgameFinishBias = Clamp01(EndgameFinishBias);
            EndgameStabilityBias = Clamp01(EndgameStabilityBias);

            // v2: 细粒度参数
            PartnerWinning_GivePointsPriority = Clamp01(PartnerWinning_GivePointsPriority);
            PartnerWinning_DiscardSmallPriority = Clamp01(PartnerWinning_DiscardSmallPriority);
            PartnerWinning_AvoidTrumpPriority = Clamp01(PartnerWinning_AvoidTrumpPriority);
            PartnerWinning_KeepPairsPriority = Clamp01(PartnerWinning_KeepPairsPriority);

            WinAttempt_UseMinimalCardsPriority = Clamp01(WinAttempt_UseMinimalCardsPriority);
            WinAttempt_PreserveControlPriority = Clamp01(WinAttempt_PreserveControlPriority);
            WinAttempt_NextLeadValueWeight = Clamp01(WinAttempt_NextLeadValueWeight);
            WinAttempt_LeadingConservativeBias = Clamp01(WinAttempt_LeadingConservativeBias);

            CannotWin_DiscardSmallPriority = Clamp01(CannotWin_DiscardSmallPriority);
            CannotWin_AvoidPointsPriority = Clamp01(CannotWin_AvoidPointsPriority);
            CannotWin_AvoidTrumpPriority = Clamp01(CannotWin_AvoidTrumpPriority);
            CannotWin_PreserveLongSuitPriority = Clamp01(CannotWin_PreserveLongSuitPriority);

            BottomProtection_Alertness = Clamp01(BottomProtection_Alertness);
            BottomProtection_AvoidShortSuitPriority = Clamp01(BottomProtection_AvoidShortSuitPriority);
            BottomProtection_PreferTrumpPriority = Clamp01(BottomProtection_PreferTrumpPriority);
            BottomProtection_HelpPartnerPriority = Clamp01(BottomProtection_HelpPartnerPriority);

            Lead_BigCardControlPriority = Clamp01(Lead_BigCardControlPriority);
            Lead_ClearShortSuitPriority = Clamp01(Lead_ClearShortSuitPriority);
            Lead_ProbeOpponentPriority = Clamp01(Lead_ProbeOpponentPriority);
            Lead_LeadingSafetyBias = Clamp01(Lead_LeadingSafetyBias);

            if (LeadThrowMinAdvantage < 0)
                LeadThrowMinAdvantage = 0;

            return this;
        }

        public static AIStrategyParameters CreateDefault()
        {
            return new AIStrategyParameters().Normalize();
        }

        /// <summary>
        /// 难度预设：后续可在此基础上叠加自适应参数。
        /// </summary>
        public static AIStrategyParameters CreatePreset(AIDifficulty difficulty)
        {
            var p = CreateDefault();

            switch (difficulty)
            {
                case AIDifficulty.Easy:
                    p.LeadConservativeBias = 0.75;
                    p.FollowBeatAttemptBias = 0.35;
                    p.PointCardProtectionWeight = 0.40;
                    p.BuryTrumpProtection = 0.55;
                    break;
                case AIDifficulty.Medium:
                    p.LeadConservativeBias = 0.55;
                    p.FollowBeatAttemptBias = 0.60;
                    p.PointCardProtectionWeight = 0.60;
                    p.BuryTrumpProtection = 0.70;
                    break;
                case AIDifficulty.Hard:
                    p.LeadConservativeBias = 0.45;
                    p.FollowBeatAttemptBias = 0.75;
                    p.PointCardProtectionWeight = 0.72;
                    p.BuryTrumpProtection = 0.82;
                    p.EndgameFinishBias = 0.72;
                    break;
                case AIDifficulty.Expert:
                    p.LeadConservativeBias = 0.35;
                    p.FollowBeatAttemptBias = 0.85;
                    p.PointCardProtectionWeight = 0.80;
                    p.BuryTrumpProtection = 0.88;
                    p.EndgameFinishBias = 0.80;
                    p.EndgameStabilityBias = 0.82;
                    break;
            }

            return p.Normalize();
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }
    }
}
