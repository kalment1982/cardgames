# 规则AI V3 测试用例设计清单 v1.0

- 状态：测试设计基线（Agent-T）
- 日期：2026-03-19
- 适用分支：`ruleAIV3`
- 对应策略真值源：`规则AI V3_首批冻结策略_v1.0.md`

---

## 1. 设计目标

本清单用于把 V3 冻结条目转为可追踪测试资产，分三层：

1. `Specs`：测试矩阵校验（可立即执行）
2. `Acceptance`：条目级用例骨架（待 V30 模块接入）
3. `Regression`：跨模块冒烟回归（待 V30 总装配后执行）

---

## 2. 模块覆盖规则

每个模块都必须包含三类测试设计：

- `Positive`：正常触发
- `Negative`：不应触发
- `Boundary`：边界回退

模块范围：

- `Contracts`
- `Lead`
- `Bottom`
- `Memory`（含 Mate 条目）
- `Explain`

---

## 3. 冻结条目到 Acceptance 的映射

| 冻结条目 | Acceptance 用例 |
|---|---|
| `Lead-001` | `Lead001_DealerEarlyStableSideSuit_ShouldPreferStableSideSuit` |
| `Lead-003` | `Lead003_DrawTrumpRequiresClearBenefit_ShouldRejectIfNoBenefit` |
| `Lead-005` | `Lead005_SafeThrowWithScoreAtLeast10_ShouldBeTopPriority` |
| `Lead-006` | `Lead006_StableSuitTeamScoring_ShouldContinueAtConfidence70Percent` |
| `Lead-007` | `Lead007_HandoffRequiresDualConditions_ShouldRejectBlindHandoff` |
| `Lead-008` | `Lead008_PrepareThrow_ShouldRespectTrumpDrawHardConstraints` |
| `Lead-009` | `Lead009_CreateVoid_ShouldOnlyBreakWeakNonScorePairs` |
| `Bottom-001` | `Bottom001_DealerShouldEnterProtectModeEarly_WhenBottomRiskIsHigh` |
| `Bottom-003` | `Bottom003_ShouldPreferSingleBottom_WhenSingleBottomAlreadyWins` |
| `Bottom-006` | `Bottom006_DefaultOrderSmallThenBigJoker_WithThreatException` |
| `Bottom-008` | `Bottom008_DefenderBottomContest_IsDynamicAcrossPhases` |
| `Bottom-009` | `Bottom009_BottomScoreEstimate_ShouldIncreaseOnUnseenScoreSignals` |
| `Bottom-011` | `Bottom011_DealerProtectBottomMode_ShouldSwitchDynamically` |
| `Mate-001` | `Mate001_PartnerStableWin_ShouldAllowPointFeedWithoutOvertaking` |
| `Mate-003` | `Mate003_NoOutcomeDelta_ShouldPreferSmallCardAndPreserveStructure` |
| `Memory-002` | `Memory002_VoidInference_ShouldSeparateConfirmedAndProbabilisticFacts` |
| `Memory-006` | `Memory006_RearThreatAssessment_ShouldClassifyFragileStableLockWins` |

---

## 4. Explain 字段完整性要求

Explain 必须覆盖以下字段（字段名严格一致）：

- `phase`
- `primary_intent`
- `secondary_intent`
- `triggered_rules`
- `candidate_count`
- `candidate_summary`
- `rejected_reasons`
- `selected_action`
- `selected_reason`
- `known_facts`
- `estimated_facts`
- `win_security`
- `bottom_mode`
- `generated_at_utc`
- `log_context`

---

## 5. Smoke 回归最小集

第一批 smoke 回归要求至少覆盖：

1. `Lead-001`
2. `Lead-007`
3. `Bottom-001`
4. `Bottom-011`
5. `Mate-001`
6. Explain 字段完整性

---

## 6. 当前阶段说明

由于 `src/Core/AI/V30/` 业务实现尚未接入，本轮测试资产采用“可编译骨架 + 待接入场景”方式：

- 可执行：`Specs` 矩阵校验与 explain 字段目录校验
- 待执行：`Acceptance` 与 `Regression` 场景（已建立骨架与命名）

后续由各模块实现 agent 交付后，Agent-T 将补齐 fixture、输入构造与期望断言，移除 `Skip`。
