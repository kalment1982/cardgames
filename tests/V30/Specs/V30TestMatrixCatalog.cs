using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TractorGame.Tests.V30.Specs
{
    public enum V30CaseType
    {
        Positive,
        Negative,
        Boundary
    }

    public sealed record V30TestCaseSpec(
        string CaseId,
        string Module,
        V30CaseType CaseType,
        string Title,
        string? FrozenEntryId = null,
        bool IsSmoke = false,
        string? Notes = null);

    public static class V30TestMatrixCatalog
    {
        public static readonly IReadOnlyList<string> FrozenEntryIds = new ReadOnlyCollection<string>(
            new List<string>
            {
                "Lead-001",
                "Lead-003",
                "Lead-005",
                "Lead-006",
                "Lead-007",
                "Lead-008",
                "Lead-009",
                "Bottom-001",
                "Bottom-003",
                "Bottom-006",
                "Bottom-008",
                "Bottom-009",
                "Bottom-011",
                "Mate-001",
                "Mate-003",
                "Memory-002",
                "Memory-006"
            });

        public static readonly IReadOnlyList<string> Modules = new ReadOnlyCollection<string>(
            new List<string> { "Contracts", "Lead", "Bottom", "Memory", "Explain" });

        public static readonly IReadOnlyList<string> RequiredExplainFields = new ReadOnlyCollection<string>(
            new List<string>
            {
                "phase",
                "primary_intent",
                "secondary_intent",
                "triggered_rules",
                "candidate_count",
                "candidate_summary",
                "rejected_reasons",
                "selected_action",
                "selected_reason",
                "known_facts",
                "estimated_facts",
                "win_security",
                "bottom_mode",
                "generated_at_utc",
                "log_context"
            });

        public static readonly IReadOnlyList<V30TestCaseSpec> Cases = new ReadOnlyCollection<V30TestCaseSpec>(
            new List<V30TestCaseSpec>
            {
                new("Contracts_Positive_ContextBuild_MinimalFields", "Contracts", V30CaseType.Positive, "最小上下文可成功构造"),
                new("Contracts_Negative_MissingRequiredFact_ShouldReject", "Contracts", V30CaseType.Negative, "缺少必要事实时拒绝构造"),
                new("Contracts_Boundary_ConfidenceAt70Percent", "Contracts", V30CaseType.Boundary, "70% 概率边界按概率口径处理"),

                new("Lead001_Positive_DealerEarlyStableSideSuit", "Lead", V30CaseType.Positive, "庄家前两墩稳副兑现", "Lead-001", true),
                new("Lead001_Negative_LostDominance_ShouldExit", "Lead", V30CaseType.Negative, "稳副主导权丢失应退出", "Lead-001"),
                new("Lead001_Boundary_Trick3_NoPriorityBonus", "Lead", V30CaseType.Boundary, "第三墩可延续但无优先级加成", "Lead-001"),

                new("Lead003_Positive_DrawTrumpWithClearBenefit", "Lead", V30CaseType.Positive, "抽主有明确收益才触发", "Lead-003"),
                new("Lead003_Negative_NoBenefit_ShouldNotDrawTrump", "Lead", V30CaseType.Negative, "无收益不抽主", "Lead-003"),
                new("Lead003_Boundary_IntentBoundaryToLead007Or008", "Lead", V30CaseType.Boundary, "出主意图边界归类", "Lead-003"),

                new("Lead005_Positive_HighValueSafeThrow", "Lead", V30CaseType.Positive, ">=10 分安全甩优先", "Lead-005"),
                new("Lead005_Negative_UnsafeThrow_ShouldReject", "Lead", V30CaseType.Negative, "不安全甩牌不应触发", "Lead-005"),
                new("Lead005_Boundary_ZeroPointThrow_Downgrade", "Lead", V30CaseType.Boundary, "0 分安全甩降级同层比较", "Lead-005"),

                new("Lead006_Positive_StableSuitTeamScoring", "Lead", V30CaseType.Positive, "稳副协作跑分", "Lead-006"),
                new("Lead006_Negative_KeyOpponentLikelyVoid_ShouldStop", "Lead", V30CaseType.Negative, "关键对手可能绝门不继续", "Lead-006"),
                new("Lead006_Boundary_Confidence70Percent_Continue", "Lead", V30CaseType.Boundary, "70% 未绝门边界继续", "Lead-006"),

                new("Lead007_Positive_HandoffWithDualConditions", "Lead", V30CaseType.Positive, "交牌权需双条件", "Lead-007"),
                new("Lead007_Negative_BlindHandoffRejected", "Lead", V30CaseType.Negative, "禁止盲交", "Lead-007", true),
                new("Lead007_Boundary_NoFollowupButNoEvidence_ShouldProbe", "Lead", V30CaseType.Boundary, "无后续但无证据时回退试探", "Lead-007"),

                new("Lead008_Positive_ThreePairs_ControlledPlay", "Lead", V30CaseType.Positive, "三对子经营", "Lead-008"),
                new("Lead008_Negative_DrawTrumpForThrow_ConstraintFail", "Lead", V30CaseType.Negative, "调主不满足硬约束应拒绝", "Lead-008"),
                new("Lead008_Boundary_QQOrLower_NotControlByDefault", "Lead", V30CaseType.Boundary, "QQ 及以下默认不算控场", "Lead-008"),

                new("Lead009_Positive_BreakWeakPairForVoid", "Lead", V30CaseType.Positive, "仅拆弱对做绝门", "Lead-009"),
                new("Lead009_Negative_BreakScorePair_ShouldReject", "Lead", V30CaseType.Negative, "禁止拆分对/强对子/拖拉机", "Lead-009"),
                new("Lead009_Boundary_AllowedBreakNeedsFollowupBenefit", "Lead", V30CaseType.Boundary, "允许拆弱对仍需明确后续收益", "Lead-009"),

                new("Bottom001_Positive_DealerBottomRiskEarlyTrigger", "Bottom", V30CaseType.Positive, "庄家保底提前触发", "Bottom-001", true),
                new("Bottom001_Negative_LowBottomScore_NoEscalation", "Bottom", V30CaseType.Negative, "低底分不提前升级", "Bottom-001"),
                new("Bottom001_Boundary_MediumBottomScore_StartAttention", "Bottom", V30CaseType.Boundary, "中底分进入保底关注", "Bottom-001"),

                new("Bottom003_Positive_DoubleNeeded_KeepStructure", "Bottom", V30CaseType.Positive, "单扣不够时可争双扣", "Bottom-003"),
                new("Bottom003_Negative_SingleEnough_AvoidRisk", "Bottom", V30CaseType.Negative, "单扣已够赢默认降风险", "Bottom-003"),
                new("Bottom003_Boundary_EqualStability_AllowDouble", "Bottom", V30CaseType.Boundary, "同稳时可追双扣", "Bottom-003"),

                new("Bottom006_Positive_SmallThenBig_Default", "Bottom", V30CaseType.Positive, "默认先小王后大王", "Bottom-006"),
                new("Bottom006_Negative_OvertakeRisk_DoNotPlaySmallFirst", "Bottom", V30CaseType.Negative, "有后位反压风险不机械先小王", "Bottom-006"),
                new("Bottom006_Boundary_FragileVsStableControl", "Bottom", V30CaseType.Boundary, "险胜与稳赢边界", "Bottom-006"),

                new("Bottom008_Positive_EarlyContestWhenTrumpStrong", "Bottom", V30CaseType.Positive, "前期主强可早纳入抠底", "Bottom-008"),
                new("Bottom008_Negative_LateOnlyDecision_ShouldReject", "Bottom", V30CaseType.Negative, "禁止末墩临时起意", "Bottom-008"),
                new("Bottom008_Boundary_Score60_RaisePriority", "Bottom", V30CaseType.Boundary, "闲家 60 分阈值边界", "Bottom-008"),

                new("Bottom009_Positive_UnseenScoreLikelyInBottom", "Bottom", V30CaseType.Positive, "空门未见分牌提高底牌估分", "Bottom-009"),
                new("Bottom009_Negative_NoSignal_KeepDefaultEstimate", "Bottom", V30CaseType.Negative, "无信号时默认估分", "Bottom-009"),
                new("Bottom009_Boundary_MultiSuitSignals_EscalateEstimate", "Bottom", V30CaseType.Boundary, "多门信号显著上调估值", "Bottom-009"),

                new("Bottom011_Positive_ModeSwitchToStrongProtect", "Bottom", V30CaseType.Positive, "保底模式动态切换", "Bottom-011", true),
                new("Bottom011_Negative_NormalMode_DoNotFreezeTrump", "Bottom", V30CaseType.Negative, "非强保底不冻结主牌", "Bottom-011"),
                new("Bottom011_Boundary_AllowGiveupSmallTrickUnder10", "Bottom", V30CaseType.Boundary, "强保底允许放弃 <=10 分墩", "Bottom-011"),

                new("Mate001_Positive_PartnerStableWin_SendPoints", "Memory", V30CaseType.Positive, "队友稳大可垫分", "Mate-001", true),
                new("Mate001_Negative_FragileLead_NoBlindPointFeed", "Memory", V30CaseType.Negative, "队友险胜不机械送分", "Mate-001"),
                new("Mate001_Boundary_HighConfidenceNotGuaranteed", "Memory", V30CaseType.Boundary, "高置信稳赢边界", "Mate-001"),

                new("Mate003_Positive_NoDelta_UseSmallCards", "Memory", V30CaseType.Positive, "无收益差异小牌优先", "Mate-003"),
                new("Mate003_Negative_OutcomeDeltaExists_DoNotUseShortcut", "Memory", V30CaseType.Negative, "存在收益差异不走小牌捷径", "Mate-003"),
                new("Mate003_Boundary_InfoInsufficient_DefaultConservative", "Memory", V30CaseType.Boundary, "信息不足走保守回退", "Mate-003"),

                new("Memory002_Positive_ConfirmedVoid_DrivesPlan", "Memory", V30CaseType.Positive, "确认绝门强驱动策略", "Memory-002"),
                new("Memory002_Negative_ProbabilisticFact_NotTreatedAsCertain", "Memory", V30CaseType.Negative, "概率信息不伪装事实", "Memory-002"),
                new("Memory002_Boundary_Use70PercentForTendencyOnly", "Memory", V30CaseType.Boundary, "70% 口径仅调倾向", "Memory-002"),

                new("Memory006_Positive_AssessRearOpponentThreat", "Memory", V30CaseType.Positive, "按后位未出玩家评估反超风险", "Memory-006"),
                new("Memory006_Negative_OnlyCompareCurrentWinner_ShouldReject", "Memory", V30CaseType.Negative, "只看当前赢家是错误判断", "Memory-006"),
                new("Memory006_Boundary_ClassifyFragileStableLockWin", "Memory", V30CaseType.Boundary, "险胜/稳赢/锁赢分层", "Memory-006"),

                new("Explain_Positive_RequiredFieldsPresent", "Explain", V30CaseType.Positive, "日志字段完整性校验", null, true),
                new("Explain_Negative_UnknownFieldAlias_ShouldReject", "Explain", V30CaseType.Negative, "禁止字段别名漂移"),
                new("Explain_Boundary_SplitKnownAndEstimatedFacts", "Explain", V30CaseType.Boundary, "确定事实与估计事实分层")
            });

        public static IReadOnlyDictionary<string, IReadOnlyList<V30TestCaseSpec>> CasesByModule { get; } =
            new ReadOnlyDictionary<string, IReadOnlyList<V30TestCaseSpec>>(
                Cases.GroupBy(c => c.Module).ToDictionary(g => g.Key, g => (IReadOnlyList<V30TestCaseSpec>)g.ToList()));
    }
}
