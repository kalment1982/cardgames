# 规则AI V3 并行实施任务清单 v1.0

- 状态：实施任务清单
- 日期：2026-03-19
- 适用分支：`ruleAIV3`
- 目标：把 `规则AI V3_首批冻结策略_v1.0.md` 拆成可由多个 Codex agent 并行实施的工作包

---

## 1. 文档定位

本文档不是策略文档，而是实施拆分文档。

用途：
- 给多个 Codex agent 分派并行任务
- 控制写入边界，减少互相覆盖
- 规定每个任务包的测试门槛与合并顺序

本轮唯一策略真值源：
- `doc/规则AI V3_首批冻结策略_v1.0.md`

工作原则：
- 不在 `V21` 上继续原地堆改
- 以新增 `V30` 命名空间为主，最后再接入 `AIPlayer`
- 先做“可并行骨架”，再做“策略实现”，最后做“接线与回归”

---

## 2. 并行实施总原则

### 2.1 目标拆分原则

RuleAI V3 这轮先只落地以下冻结范围：
- `Lead-001 / 003 / 005 / 006 / 007 / 008 / 009`
- `Bottom-001 / 003 / 006 / 008 / 009 / 011`
- `Mate-001 / 003`
- `Memory-002 / 006`

本轮明确不做：
- 跟牌策略域
- 毙牌策略域
- 埋底策略域
- 多副牌门优先级专项细化
- 评分器精确权重调优

### 2.2 文件边界原则

为避免多个 agent 反复修改同一批 V21 文件，本轮采用新目录：

```text
src/Core/AI/V30/
tests/V30/
```

推荐子目录：

```text
src/Core/AI/V30/Contracts/
src/Core/AI/V30/Lead/
src/Core/AI/V30/Bottom/
src/Core/AI/V30/Memory/
src/Core/AI/V30/Scoring/
src/Core/AI/V30/Explain/
```

说明：
- `Scoring/` 本轮先作为预留目录，不进入第一批并行写入范围
- 第一批冻结策略优先在各自策略域内完成规则实现，避免过早抽公共评分层导致冲突

硬规则：
- 一个 agent 只拥有一组明确写入目录
- 不允许两个 agent 同时改同一实现文件
- 公共模型文件由协调 agent 统一创建，其他 agent 基于它开发

### 2.3 并发建议

建议并发数：`5` 个实施 agent + `1` 个协调 agent

原因：
- 少于 5 个，测试设计与业务实现会互相挤占时间
- 多于 5 个，`Intent / Integration / Test ownership` 很容易发生冲突

---

## 3. 实施阶段

### 阶段 A：串行打底

先由协调 agent 完成基础骨架，再放并行。

### 阶段 B：并行实现

Lead / Bottom / Memory-Mate / Explain / Test-Validation 五包并行推进。

### 阶段 C：串行集成

由协调 agent 把 V30 接进 `AIPlayer` 和配置开关，然后做统一回归。

---

## 4. 阶段 A：协调 agent 串行打底

### A0. 建立 V30 骨架

目标：
- 创建 V30 目录结构
- 创建最小公共模型与接口
- 确定后续 agent 的写入边界

建议文件：

```text
src/Core/AI/V30/Contracts/PhaseKindV30.cs
src/Core/AI/V30/Contracts/DecisionIntentKindV30.cs
src/Core/AI/V30/Contracts/RuleAIContextV30.cs
src/Core/AI/V30/Contracts/PhaseDecisionV30.cs
src/Core/AI/V30/Contracts/ResolvedIntentV30.cs
src/Core/AI/V30/Contracts/DecisionExplanationV30.cs
src/Core/AI/V30/Contracts/ScoredActionV30.cs
src/Core/AI/V30/Contracts/V30FeatureFlags.cs
```

测试要求：
- `tests/V30/Contracts/` 下每个公共模型至少有 1 个基础构造测试

完成标准：
- 所有后续 agent 可以只基于 `Contracts` 开发
- 后续任务不需要再反复改公共枚举和字段名

### A1. 建立 V30 上下文组装入口

目标：
- 先把 V30 的输入管线固定住
- 让后续策略包能直接消费统一上下文

建议文件：

```text
src/Core/AI/V30/Contracts/RuleAIContextBuilderV30.cs
src/Core/AI/V30/Contracts/HandProfileBuilderV30.cs
src/Core/AI/V30/Contracts/MemorySnapshotBuilderV30.cs
```

注意：
- 本轮只补到 V3 冻结策略真正需要的字段
- 先不要把所有 V21 字段机械复制过去

测试要求：
- `tests/V30/Contracts/RuleAIContextBuilderV30Tests.cs`
- `tests/V30/Contracts/HandProfileBuilderV30Tests.cs`
- `tests/V30/Contracts/MemorySnapshotBuilderV30Tests.cs`

完成标准：
- Lead / Bottom / Mate / Memory 四类策略都能从统一 context 取数

---

## 5. 阶段 B：并行工作包

## 5.1 Agent-L：Lead 域实现包

职责：
- 只负责首发域
- 不改 `Bottom / Memory / AIPlayer`

拥有目录：

```text
src/Core/AI/V30/Lead/
tests/V30/Lead/
```

建议文件：

```text
src/Core/AI/V30/Lead/LeadCandidateGeneratorV30.cs
src/Core/AI/V30/Lead/LeadPriorityResolverV30.cs
src/Core/AI/V30/Lead/LeadPolicyV30.cs
src/Core/AI/V30/Lead/LeadRuleEvaluatorV30.cs
```

负责落地的冻结条目：
- `Lead-001`
- `Lead-003`
- `Lead-005`
- `Lead-006`
- `Lead-007`
- `Lead-008`
- `Lead-009`

必须实现的能力：
- 首发总优先级顺序
- 高收益安全甩牌与低分甩牌分层
- 庄家前两墩稳副兑现
- 稳副协作跑分
- 交牌给队友必须满足双条件
- 三对子经营
- 为甩牌调主的硬约束
- 做绝门只允许拆弱对

明确不负责：
- 跟牌
- 毙牌
- 埋底
- AIPlayer 接线

测试要求：
- `Lead-001` 至少 3 例
- `Lead-007` 至少 3 例，其中必须有“禁止盲交”
- `Lead-008` 至少 4 例，覆盖：
  - 大对子控场
  - 小对子耗资源
  - 为甩牌调主成功
  - 为甩牌调主被硬约束拦截
- `Lead-009` 至少 3 例，覆盖允许拆弱对与禁止拆强结构

建议测试文件：

```text
tests/V30/Lead/LeadPriorityResolverV30Tests.cs
tests/V30/Lead/LeadCandidateGeneratorV30Tests.cs
tests/V30/Lead/LeadPolicyV30Tests.cs
```

完成标准：
- 不依赖 Bottom/Memory 复杂实现，也能跑通基础首发决策
- 所有 Lead 冻结条目都有明确单测

---

## 5.2 Agent-B：Bottom 域实现包

职责：
- 只负责保底 / 抠底模式与末盘控制
- 不改 Lead 候选和首发主逻辑

拥有目录：

```text
src/Core/AI/V30/Bottom/
tests/V30/Bottom/
```

建议文件：

```text
src/Core/AI/V30/Bottom/BottomModeResolverV30.cs
src/Core/AI/V30/Bottom/BottomScoreEstimatorV30.cs
src/Core/AI/V30/Bottom/BottomPlanSelectorV30.cs
src/Core/AI/V30/Bottom/EndgameControlPolicyV30.cs
```

负责落地的冻结条目：
- `Bottom-001`
- `Bottom-003`
- `Bottom-006`
- `Bottom-008`
- `Bottom-009`
- `Bottom-011`

必须实现的能力：
- 正常运营 / 保底关注 / 强保底
- 普通 / 抠底关注 / 强抠底
- 底牌默认估分与高分信号上调
- 单扣 vs 双扣分层判断
- 大小王顺序例外
- 强保底模式下的主牌资源冻结

明确不负责：
- 首发候选生成
- 协作送分
- 缺门推断细节

测试要求：
- 模式切换至少 4 例
- 单扣 / 双扣至少 3 例
- 大小王顺序至少 3 例
- 底牌估分至少 3 例

建议测试文件：

```text
tests/V30/Bottom/BottomModeResolverV30Tests.cs
tests/V30/Bottom/BottomScoreEstimatorV30Tests.cs
tests/V30/Bottom/BottomPlanSelectorV30Tests.cs
tests/V30/Bottom/EndgameControlPolicyV30Tests.cs
```

完成标准：
- Bottom 域可以独立给出模式和建议，不依赖 Lead 实现细节

---

## 5.3 Agent-M：Mate / Memory 域实现包

职责：
- 只负责协作与记牌应用
- 不改 Lead/Bottom 的具体策略实现

拥有目录：

```text
src/Core/AI/V30/Memory/
tests/V30/Memory/
```

建议文件：

```text
src/Core/AI/V30/Memory/PartnerCooperationPolicyV30.cs
src/Core/AI/V30/Memory/InferenceEngineV30.cs
src/Core/AI/V30/Memory/ThreatAssessmentV30.cs
```

负责落地的冻结条目：
- `Mate-001`
- `Mate-003`
- `Memory-002`
- `Memory-006`

必须实现的能力：
- 锁赢 / 高置信稳赢时才允许纯送分
- 无收益差异时小牌优先、保结构优先
- 确认绝门与 `>= 70%` 概率口径分层
- 后位反超风险必须按“还有谁没出”判定
- 候选安全等级至少区分：
  - `FragileWin`
  - `StableWin`
  - `LockWin`

明确不负责：
- 首发总优先级
- 保底模式切换
- AIPlayer 路由

测试要求：
- `Mate-001` 至少 3 例
- `Mate-003` 至少 3 例
- `Memory-002` 至少 4 例，覆盖确认绝门与概率判断
- `Memory-006` 至少 4 例，覆盖后位队友/对手差异

建议测试文件：

```text
tests/V30/Memory/PartnerCooperationPolicyV30Tests.cs
tests/V30/Memory/InferenceEngineV30Tests.cs
tests/V30/Memory/ThreatAssessmentV30Tests.cs
```

完成标准：
- Lead / Bottom 可以消费这些结果，但本包自身可独立测试

---

## 5.4 Agent-X：解释、日志与回归支撑包

职责：
- 只负责 V30 的解释层、日志结构、回归支撑
- 不实现业务策略本身

拥有目录：

```text
src/Core/AI/V30/Explain/
tests/V30/Explain/
tests/V30/Fixtures/
```

建议文件：

```text
src/Core/AI/V30/Explain/DecisionExplainerV30.cs
src/Core/AI/V30/Explain/AIDecisionLogContextV30.cs
src/Core/AI/V30/Explain/DecisionBundleBuilderV30.cs
tests/V30/Explain/DecisionExplainerV30Tests.cs
tests/V30/DecisionBundleScenarioFactoryV30.cs
```

必须实现的能力：
- 日志中明确区分：
  - 确定事实
  - 概率推断
  - 主意图
  - 候选列表
  - 淘汰原因
  - 最终动作
- 能把 V30 决策沉淀成 fixture
- 为后续异常扫描提供稳定字段

必须包含的日志字段：
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

测试要求：
- 每个日志结构文件至少 1 个序列化测试
- 至少 2 个 decision bundle fixture 测试

完成标准：
- 任何后续 V30 问题都能先看 bundle 再看回放

---

## 5.5 Agent-T：测试设计与验证包

职责：
- 不实现业务策略
- 专门为各模块设计测试用例、补 acceptance / regression 测试，并负责统一执行

拥有目录：

```text
tests/V30/Specs/
tests/V30/Acceptance/
tests/V30/Regression/
```

可新增文档：

```text
doc/规则AI V3_测试用例设计清单_v1.0.md
```

职责范围：
- 为 `Contracts / Lead / Bottom / Memory / Explain` 五类模块分别设计测试矩阵
- 把冻结稿条目映射为可执行测试清单
- 为每个模块补一组黑盒 acceptance 测试
- 在各模块实现完成后，统一执行并汇总结果

不负责：
- 业务实现
- AIPlayer 接线
- 修改 V21 行为

测试设计最低要求：
- 每个模块至少产出：
  - `正常触发` 用例
  - `不应触发` 用例
  - `边界回退` 用例
- 每个冻结条目至少有 1 条 acceptance 用例
- 每个公共模型至少有 1 条输入边界用例
- Explain 至少有 1 条日志字段完整性用例

建议测试文件：

```text
tests/V30/Specs/V30ModuleTestMatrixTests.cs
tests/V30/Acceptance/LeadAcceptanceTests.cs
tests/V30/Acceptance/BottomAcceptanceTests.cs
tests/V30/Acceptance/MemoryAcceptanceTests.cs
tests/V30/Acceptance/ExplainAcceptanceTests.cs
tests/V30/Regression/V30RegressionSmokeTests.cs
```

工作方式：
- 第一阶段：先写测试设计清单与测试骨架
- 第二阶段：等各模块 agent 交付后，补齐 fixture / scenario / expected
- 第三阶段：统一跑测试，输出模块通过情况

完成标准：
- 每个模块都有可追踪的测试矩阵
- 每个冻结条目都能在 acceptance / regression 层被定位
- 能给协调 agent 提供“哪些模块可合并、哪些模块仍阻塞”的判断

---

## 6. 阶段 C：协调 agent 串行集成包

### C1. V30 总装配

职责：
- 把 Lead / Bottom / Memory / Explain 接成统一 V30 决策管线

建议文件：

```text
src/Core/AI/V30/RuleAIEngineV30.cs
src/Core/AI/V30/RuleAIOptionsV30.cs
```

目标：
- 不让 `AIPlayer` 直接调多个零散服务
- 先在 V30 内部完成统一装配

### C2. AIPlayer 接线

职责：
- 把 V30 挂进当前运行链路
- 保留 V21 回退开关

建议改动文件：

```text
src/Core/AI/AIPlayer.cs
src/Core/AI/ChampionLoader.cs
src/Core/AI/AIStrategyParameters.cs
WebUI/wwwroot/appsettings.json
```

要求：
- 默认仍可先不开启 V30
- 能通过配置切换：
  - `Legacy`
  - `V21`
  - `V30`

### C3. 回归与自测

必须执行：

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/test_fast.sh
/Users/karmy/Projects/CardGame/tractor/scripts/test_standard.sh
```

若涉及规则层文件，再执行：

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/test_rule_regression.sh
```

如果 Lead / Bottom 行为改变明显，建议追加：
- 小批量自对弈
- decision bundle 异常扫描

---

## 7. 推荐 agent 分工

| Agent | 角色 | 负责内容 | 可写目录 |
|---|---|---|---|
| `Coordinator` | 协调与集成 | 阶段 A、阶段 C、合并、统一回归 | `src/Core/AI/V30/Contracts/` `src/Core/AI/AIPlayer.cs` |
| `Agent-L` | 首发策略 | Lead 全包 | `src/Core/AI/V30/Lead/` |
| `Agent-B` | 保底抠底 | Bottom 全包 | `src/Core/AI/V30/Bottom/` |
| `Agent-M` | 协作与记牌 | Mate / Memory 全包 | `src/Core/AI/V30/Memory/` |
| `Agent-X` | 日志与回归 | Explain / bundle / fixture | `src/Core/AI/V30/Explain/` `tests/V30/` |
| `Agent-T` | 测试设计与验证 | 测试矩阵 / acceptance / regression / 统一执行 | `tests/V30/Specs/` `tests/V30/Acceptance/` `tests/V30/Regression/` |

不建议这样拆：
- 两个 agent 同时改 `IntentResolver`
- 两个 agent 同时改 `ActionScorer`
- 两个 agent 同时改 `AIPlayer`

原因：
- 这些文件天然是冲突热点
- 会把并行优势抵消掉

---

## 8. 建议合并顺序

### 第一批合并

1. `Coordinator` 的 `Contracts + ContextBuilder`
2. `Agent-X` 的 `Explain skeleton`
3. `Agent-T` 的 `test matrix + test skeleton`

理由：
- 先把公共数据结构、日志结构和测试骨架固定住

### 第二批合并

1. `Agent-M`
2. `Agent-B`
3. `Agent-L`
4. `Agent-T` 补齐 acceptance / regression

理由：
- Lead 会消费 Memory / Bottom 的结果
- 先合入依赖，再合入最终策略包，最后由测试 agent 做统一校验

### 第三批合并

1. `Coordinator` 的 `RuleAIEngineV30`
2. `Coordinator` 的 `AIPlayer` 接线
3. 全量测试与小批量自对弈

---

## 9. 每个 agent 的提交要求

每个 agent 提交前必须报告：
- 改了哪些文件
- 哪些测试新增了
- 跑了哪些测试
- 还缺什么依赖
- 是否阻塞其他 agent

每个 agent 的任务描述必须带上：
- 负责条目编号
- 禁止写入的文件范围
- 最低测试要求
- 返回结果格式

推荐返回格式：

```text
已完成：
- 条目：
- 修改文件：
- 新增测试：
- 已运行测试：

待协调：
- 依赖：
- 风险：
```

---

## 10. 本轮实施的最小完成标准

满足以下条件才算 V3 首批落地完成：

1. V30 目录独立存在，未继续把 V21 改成不可维护状态
2. 冻结条目全部有对应实现入口
3. 每个冻结条目至少有 1 条精确测试
4. `AIPlayer` 可通过配置切换到 V30
5. 决策日志能看出：
   - 主意图
   - 关键事实
   - 概率推断
   - 候选
   - 淘汰原因
   - 最终动作
6. `test_fast.sh` 与 `test_standard.sh` 通过
7. `Agent-T` 的 acceptance / regression 测试通过，或明确列出阻塞项

---

## 11. 启动建议

如果要马上开始派工，建议按以下顺序发给多个 Codex agent：

1. 先由协调 agent 完成阶段 A
2. 再同时启动：
   - `Agent-L`
   - `Agent-B`
   - `Agent-M`
   - `Agent-X`
   - `Agent-T`
3. 五包全部回收后，再做阶段 C 集成

不建议一上来就让多个 agent 直接改 `AIPlayer` 或 `V21` 目录。

那样会重复 V2.1 的问题：
- 共享文件过多
- 策略口径漂移
- 候选、意图、评分器互相覆盖
- 最后很难知道到底是哪一层出了问题
