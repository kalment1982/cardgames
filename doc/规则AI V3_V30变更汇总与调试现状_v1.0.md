# 规则AI V3_V30变更汇总与调试现状 v1.0

## 1. 文档目的

本文件用于统一沉淀 `RuleAI V30` 截至当前分支的实际落地内容，避免 `V30` 的策略、代码、测试、评估结论分散在多个上下文里难以追踪。

适用范围：

- `src/Core/AI/V30/`
- `tests/V30/`
- `V30 vs V21` 对战审计

不包含：

- `PPO AI`
- `V21` 的完整历史设计细节
- WebUI 与日志系统的通用实现说明

## 2. 当前代码落地范围

`V30` 当前已经形成完整的模块化骨架，而不是单点参数补丁：

- 合同层：
  - `Contracts/RuleAIContextV30`
  - `Contracts/DecisionFrameV30`
  - `Contracts/HandProfileV30`
  - `Contracts/MemorySnapshotV30`
- 首发层：
  - `Lead/LeadRuleEvaluatorV30`
  - `Lead/LeadCandidateGeneratorV30`
  - `Lead/LeadPriorityResolverV30`
  - `Lead/LeadPolicyV30`
- 跟牌层：
  - `Follow/FollowCandidateOverlayV30`
  - `Follow/FollowPolicyV30`
- 保底/抠底层：
  - `Bottom/*`
- 记牌/推断/协作层：
  - `Memory/InferenceEngineV30`
  - `Memory/PartnerCooperationPolicyV30`
  - `Memory/ThreatAssessmentV30`
- 解释与日志层：
  - `Explain/DecisionBundleBuilderV30`
  - `Explain/DecisionExplainerV30`
- 引擎整合层：
  - `RuleAIEngineV30`

## 3. 相对 V21 的核心新增能力

### 3.1 决策输入结构化

`V30` 不再只依赖旧版 `AIPlayer` 的即时打分，而是显式引入：

- 手牌结构画像
- 已出牌记忆
- 缺门/对子/拖拉机证据
- 底牌风险、保底压力、抠底压力
- 末盘级别

### 3.2 首发语义层

`V30 Lead` 先做语义判定，再落到具体牌：

- `SafeThrow`
- `StableSideSuitRun`
- `ForceTrump`
- `HandOffToMate`
- `PrepareFutureThrow`
- `BuildVoid`
- `ProbeWeakSuit`

### 3.3 跟牌语义层

`V30 Follow` 当前支持：

- `PassToMate`
- `TakeLead`
- `TakeScore`
- `PrepareEndgame`
- `MinimizeLoss`

### 3.4 解释与回放

`V30` 每次决策都可以输出：

- 语义意图
- 具体候选
- 候选排序
- 触发规则
- 选中原因

这使得逐墩复盘成为稳定流程，而不是靠猜。

## 4. 已落地的重要策略调整

以下为本轮迭代前后已经进入代码的重点变化。

### 4.1 Follow 层

#### 已落地

- 增加 `CandidateMetrics`
  - 统一收集 `CanBeatCurrentWinner`
  - `Security`
  - `ControlSpendCost`
  - `CandidatePoints`
  - `DiscardStrengthCost`
  - `HighControlCount`
- 增加 `TakeLead` / `PrepareEndgame` 跟牌意图
- 增加庄家侧零分墩 `tempo reclaim` 模式
  - 零分墩
  - 庄家侧
  - 自己后位/末段
  - 有低代价可赢牌时，允许主动拿回首发权
- 增加被动垫牌时的弱牌优先 tie-break
  - `PassToMate`
  - `MinimizeLoss`
- 增加“三家位险胜 + 后手敌家高主威胁”降档
  - 不再把所有 `fragile win` 都当成该抢的分墩

#### 影响

- 降低了“明明只是在垫牌，却把 A / K / 主牌先垫掉”的概率
- 降低了“三家位为了抢 5/10 分，结果被后手再反压”的风险

### 4.2 Lead 层

#### 已落地

- `ForceTrump` 抑制增强
  - 有稳定副牌线时阻断
  - 有队友副牌线时阻断
  - 有未来甩牌计划时阻断
  - 庄家侧前中盘若存在便宜副牌探路，阻断
  - 非保底/非末盘时，不再允许用高控制主牌作为“便宜抽主”
- concrete landing 增加 `force_trump` 二次抑制
  - 防止语义层想抽主，但落地层明明有更便宜的副牌控场牌仍然硬抽主
  - `force_trump` 不再允许以“语义优先级”压过分数更高的具体候选
- `ProbeWeakSuit` 不再等价于“随便打一张”
  - 庄家侧若存在便宜零分副牌 probe：
    - 过滤泛化的低价值分牌 probe
    - 过滤高控制主牌 / 王 probe
- `Lead001` 从“庄家前两墩稳副兑现”扩展为“庄家侧中前盘连续稳副线”
  - 庄家或庄家队友
  - 有稳定副牌线
  - 未失控
  - 且相对普通 probe 有明确 EV 优势时，允许持续触发

#### 影响

- 减少了中前盘无收益抽主
- 减少了把 `10/K` 这种分牌当普通 probe 乱打
- 增强了庄家侧“赢一墩后继续沿副牌盈利线推进”的能力
- 降低了“刚拿到首发就盲交队友”“拿大王/主A去抽主”这类明显坏招

### 4.3 HandOffToMate 收紧

#### 已落地

- 庄家侧前几墩禁止过早 `HandOffToMate`
- 若普通 `probe` 的未来收益明显为正，则不允许用 `HandOffToMate` 抢优先级

#### 影响

- 避免了“明明自己还能继续推进，却过早把首发权交掉”的回退
- 修掉了多手失败局中 `handoff_to_mate` 的明确误判

## 5. 近期新增测试覆盖

本轮新增/强化的测试点：

- `tests/V30/Lead/LeadRuleEvaluatorV30Tests.cs`
  - 庄家中盘稳副线触发
  - 庄家队友中盘稳副线触发
  - 稳副优势不足时不触发
  - 稳副/甩牌计划阻断 `ForceTrump`
- `tests/V30/Lead/LeadConcreteLandingRegressionTests.cs`
  - 庄家侧不应把普通分牌当弱探路牌打出
  - `ForceTrump` 不应压过更合理副牌方案
  - 末盘应让位给 `PrepareEndgame`
- `tests/V30/Follow/FollowCandidateOverlayV30Tests.cs`
  - `PassToMate` / `MinimizeLoss` 的弱牌优先
  - 庄家侧零分墩 `TakeLead`
  - 三家位险胜 + 后手敌家高主威胁时降档

当前本轮相关聚焦测试结果：

- `50/50` 通过

执行命令：

```bash
dotnet test TractorGame.csproj --filter "FullyQualifiedName~LeadConcreteLandingRegressionTests|FullyQualifiedName~LeadRuleEvaluatorV30Tests|FullyQualifiedName~FollowCandidateOverlayV30Tests|FullyQualifiedName~RuleAIEngineV30LeadTests|FullyQualifiedName~FollowPolicyV30Tests" -v minimal
```

## 6. 最近评估结论

最近几轮 `V30 vs V21` 20 局审计结果大致在：

- 50%
- 45%
- 40%
- 35%
- 40%

说明：

- `V30` 已经明显不是“不能打”，但离“稳定压过 V21”还有距离
- 近期几轮下降说明前一轮改动有过度抑制的问题

### 当前确认的主问题

主问题仍然是 `Lead`，不是 `Follow`。

更具体地说：

- 庄家侧仍然缺少真正的“连续控场状态机”
- 很多失败局不是单手牌错误，而是：
  - 赢下一墩
  - 下一墩却切回泛化 `ProbeWeakSuit`
  - 或者无收益 `ForceTrump`
  - 没把同一条盈利线路持续走下去

### 当前确认的次问题

- 跟牌仍有一部分第三家抢分过激
- 多张垫牌/补牌时，偶尔仍会消耗偏强 filler

## 7. 当前调试方法

本线已经形成固定调试流程：

1. 跑 `V30 vs V21` 审计
2. 先看总胜率
3. 再看失败局逐墩 markdown
4. 对关键错手回到 decision bundle 看：
   - semantic candidate
   - concrete candidate
   - selected candidate
   - reason code
5. 将高频问题固化成测试
6. 再做下一轮对战

## 8. 下一阶段调试重点

接下来仍然应优先处理 `Lead`：

- 建立庄家侧“连续盈利线”规则
  - 稳副线延续
  - 队友接管线延续
  - 末盘控制线延续
- 减少赢墩后退化为无意义 `probe`
- 让 `ForceTrump` 真正只在明确收益场景触发

`Follow` 继续只做高置信补丁，不做大改：

- 第三家险胜降档
- filler 弱牌优先
- 被动垫牌避免强牌浪费

## 9. 当前结论

`V30` 现在已经具备：

- 完整模块化骨架
- 可解释决策链
- 可逐墩复盘能力
- 成体系的单元/回归测试

但尚未达到目标：

- 尚未稳定实现 `V30` 对 `V21` 的多数批次 `>60%` 胜率

因此当前阶段的工作重点不是再加大而全的新系统，而是继续围绕：

- 庄家侧首发连续性
- 弱 probe 过滤
- 跟牌风险控制

做小步快跑式迭代。
