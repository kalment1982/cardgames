# 规则AI M1最小落地任务清单 v1.0

## 1. 文档定位
- 状态：实施任务清单
- 版本：v1.0
- 日期：2026-03-15
- 对应设计基线：`doc/规则AI架构设计_v2.1.md`

本文件只覆盖规则AI 2.1 的 M1 阶段，不实现新的意图引擎和评分器，只做：
- 统一上下文
- 统一解释日志
- Shadow Compare
- 回归基线
- 性能基线

M1 目标：
- 不改变现有旧路径行为
- 为 M2 跟牌升级提供稳定骨架
- 建立可观测、可回退、可验证的基础设施

## 2. M1 范围边界
### 2.1 M1 要做
1. 定义最小上下文对象
2. 定义统一解释对象
3. 实现 `RuleAIContextBuilder`
4. 在 `AIPlayer.Lead()` / `AIPlayer.Follow()` 接入上下文和解释日志
5. 引入 `Feature Flag + Shadow Mode`
6. 引入 `ai.decision / ai.compare / ai.perf` 三类日志
7. 建立旧路径回归测试基线
8. 建立性能基线记录

### 2.2 M1 不做
1. 不实现真正的 `IntentResolver`
2. 不引入新的评分器
3. 不替换旧 `Follow` 或 `Lead` 逻辑
4. 不实现 `ProtectBottom` 的真实策略逻辑
5. 不引入复杂概率推断
6. 不调整难度参数体系

## 3. M1 成功标准
满足以下条件即可认为 M1 完成：

1. 旧路径行为不变
- 关闭新路径时，`Lead/Follow` 的返回结果与当前版本一致

2. 新骨架可用
- 每次决策都能输出统一解释对象
- `RuleAIContextBuilder` 能稳定构建 `Lead` 和 `Follow` 两类上下文

3. Shadow Compare 可运行
- 同时运行旧路径和新路径占位实现
- 能输出分歧日志
- 默认不影响正式出牌结果

4. 回归测试建立
- 至少 10 个固定场景
- 旧路径全部通过

5. 性能基线建立
- `p95(total_ms) < 50ms`
- 解释日志和 Shadow Compare 不导致明显卡顿

## 4. 建议新增类型
### 4.1 `PhaseKind`
文件建议：
- `src/Core/AI/V21/PhaseKind.cs`

职责：
- 标记当前 AI 决策属于哪个阶段

建议定义：

```csharp
namespace TractorGame.Core.AI.V21;

public enum PhaseKind
{
    Bid,
    Bury,
    Lead,
    Follow
}
```

### 4.2 `RuleAIContext`
文件建议：
- `src/Core/AI/V21/RuleAIContext.cs`

M1 最小字段只保留 8 个：

```csharp
namespace TractorGame.Core.AI.V21;

public sealed class RuleAIContext
{
    public PhaseKind Phase { get; init; }
    public List<Card> MyHand { get; init; } = new();
    public AIRole Role { get; init; }
    public List<Card> LeadCards { get; init; } = new();
    public List<Card> CurrentWinningCards { get; init; } = new();
    public bool PartnerWinning { get; init; }
    public int TrickScore { get; init; }
    public int CardsLeftMin { get; init; }
}
```

说明：
- M1 不放 `LegalActions`
- M1 不放 `MemorySnapshot`
- M1 不放 `BottomRisk`
- M1 不放 `DecisionFrame` 完整对象

### 4.3 `DecisionExplanation`
文件建议：
- `src/Core/AI/V21/DecisionExplanation.cs`

M1 建议字段 8 个：

```csharp
namespace TractorGame.Core.AI.V21;

public sealed class DecisionExplanation
{
    public PhaseKind Phase { get; init; }
    public string PrimaryIntent { get; init; } = string.Empty;
    public string SelectedReason { get; init; } = string.Empty;
    public int CandidateCount { get; init; }
    public List<string> TopCandidates { get; init; } = new();
    public List<double> TopScores { get; init; } = new();
    public string SelectedAction { get; init; } = string.Empty;
    public Dictionary<string, object?> Tags { get; init; } = new();
}
```

说明：
- `PrimaryIntent` 在 M1 只是解释标签，不是真实意图解析器结果
- `TopScores` 可先为空，或仅记录旧逻辑已有排序值
- `Tags` 用于兼容不同阶段扩展

## 5. 建议新增组件
### 5.1 `RuleAIContextBuilder`
文件建议：
- `src/Core/AI/V21/RuleAIContextBuilder.cs`

职责：
- 只负责组装上下文
- 不做决策
- 不做评分

建议接口：

```csharp
namespace TractorGame.Core.AI.V21;

public sealed class RuleAIContextBuilder
{
    public RuleAIContext BuildLeadContext(
        List<Card> hand,
        AIRole role,
        int cardsLeftMin);

    public RuleAIContext BuildFollowContext(
        List<Card> hand,
        List<Card> leadCards,
        List<Card>? currentWinningCards,
        AIRole role,
        bool partnerWinning,
        int trickScore,
        int cardsLeftMin);
}
```

M1 约束：
- 不依赖 `CardMemory`
- 不依赖 `Game`
- 只使用 `AIPlayer` 现有可直接拿到的参数

### 5.2 `DecisionLogFormatter`
文件建议：
- `src/Core/AI/V21/DecisionLogFormatter.cs`

职责：
- 将 `DecisionExplanation`、性能数据、compare 数据转换成日志 payload

原因：
- 避免在 `AIPlayer` 里堆 JSON 拼装逻辑

## 6. AIPlayer 修改范围
文件：
- [AIPlayer.cs](/Users/karmy/Projects/CardGame/tractor/src/Core/AI/AIPlayer.cs)

M1 只做外围接入，不改核心旧逻辑。

### 6.1 新增字段
建议新增：

```csharp
private readonly RuleAIContextBuilder _contextBuilder = new();

private bool _useRuleAIV21 = false;
private bool _enableShadowCompare = true;
private double _shadowSampleRate = 1.0;
```

说明：
- `_useRuleAIV21`
  - 是否真正启用新路径结果
  - M1 默认 `false`
- `_enableShadowCompare`
  - 是否运行新路径占位逻辑并记录对比
  - M1 默认开发/测试环境可 `true`
- `_shadowSampleRate`
  - Shadow 模式抽样比例
  - 默认 `1.0`
  - 后续若性能压力大可调低

### 6.2 `Follow()` 改造方式
建议结构：

```csharp
public List<Card> Follow(...)
{
    var perf = new AIPerfScope("Follow");
    var context = _contextBuilder.BuildFollowContext(...);

    var oldDecision = FollowOldPath(...);
    var oldExplanation = BuildM1ExplanationFromOldPath(context, oldDecision, ...);

    LogDecision(oldExplanation, context, perf);

    if (_enableShadowCompare && HitShadowSample())
    {
        var newDecision = FollowShadowPath(context, ...);
        var newExplanation = BuildShadowExplanation(context, newDecision, ...);
        LogCompare(oldDecision, newDecision, oldExplanation, newExplanation, context, perf);

        if (_useRuleAIV21)
            return newDecision;
    }

    return oldDecision;
}
```

M1 约束：
- `FollowOldPath(...)` 必须完整复用现有逻辑
- `FollowShadowPath(...)` 可以先直接调用旧逻辑，或只做一个占位新路径
- M1 的重点是“框架成型”，不是“新路径变强”

### 6.3 `Lead()` 改造方式
与 `Follow()` 相同：
- 先构建 context
- 走旧逻辑
- 生成 explanation
- 可选 shadow compare

### 6.4 M1 的意图标签规则
M1 不实现真正的 `IntentResolver`，只用解释标签：

建议映射：
- `Follow + partnerWinning=true` -> `PassToMate`
- `Follow + trickScore>=10 + canOvertake=true` -> `TakeScore`
- `Follow + partnerWinning=false + canOvertake=false` -> `MinimizeLoss`
- `Lead + throwCandidate=true` -> `PrepareThrow`
- `Lead + default` -> `TakeLead`

说明：
- 这里只用于日志
- 不参与决策

## 7. 日志事件设计
M1 建议统一 3 类日志事件。

### 7.1 `ai.decision`
用途：
- 记录实际返回路径的解释信息

建议字段：

```json
{
  "phase": "Follow",
  "path": "old",
  "primary_intent": "PassToMate",
  "selected_reason": "minimal_follow",
  "candidate_count": 5,
  "selected_action": ["♠5", "♠6"],
  "partner_winning": true,
  "trick_score": 15,
  "cards_left_min": 12
}
```

### 7.2 `ai.compare`
用途：
- 记录新旧路径分歧

建议字段：

```json
{
  "phase": "Follow",
  "old_action": ["♠5", "♠6"],
  "new_action": ["♥5", "♥5"],
  "divergence": true,
  "old_reason": "minimal_follow",
  "new_reason": "shadow_placeholder",
  "old_intent": "PassToMate",
  "new_intent": "PassToMate",
  "partner_winning": true,
  "trick_score": 15
}
```

### 7.3 `ai.perf`
用途：
- 记录性能基线

建议字段：

```json
{
  "phase": "Follow",
  "context_ms": 0.4,
  "candidate_ms": 1.2,
  "score_ms": 0.0,
  "compare_ms": 0.8,
  "candidate_count": 5,
  "total_ms": 3.1
}
```

## 8. 性能基线要求
M1 目标：
- 平均耗时 `< 20ms`
- `p95(total_ms) < 50ms`

后续目标：
- `M2 p95 < 100ms`
- `M3 p95 < 150ms`
- `M4 p95 < 200ms`

M1 必须记录：
- `context_ms`
- `candidate_ms`
- `score_ms`
- `compare_ms`
- `total_ms`
- `candidate_count`

注意：
- M1 虽然不引入真正评分器，`score_ms` 字段仍保留，便于后续统一指标

## 9. 回归测试要求
### 9.1 目录建议
- `tests/RegressionTests/BaselineBehaviorTests.cs`
- `tests/RegressionFixtures/`

### 9.2 Fixture 建议结构
每个场景固定：
- `hand`
- `lead_cards`
- `current_winning_cards`
- `partner_winning`
- `role`
- `expected_old_action`

建议使用固定代码夹具或 JSON fixture，不依赖 UI 手工回放。

### 9.3 首批 10 个场景
1. `partner_winning_safe`
2. `partner_winning_fragile`
3. `opponent_high_score_cheap_overtake`
4. `opponent_high_score_too_expensive`
5. `void_with_valid_cut`
6. `void_with_invalid_cut`
7. `mixed_lead_minimal_follow`
8. `high_score_trick_preserve_structure`
9. `lead_safe_throw_candidate`
10. `endgame_pre_bottom_guard`

### 9.4 基线测试原则
- 默认只校验旧路径
- M1 不要求新路径比旧路径更优
- M1 只要求：
  - 旧路径结果不变
  - 新日志输出不影响旧行为

测试示意：

```csharp
[Fact]
public void OldPath_PartnerWinning_ShouldMatchBaseline()
{
    var scenario = RegressionScenarioFactory.Load("partner_winning_safe");
    var ai = BuildAi(useRuleAIV21: false, enableShadowCompare: false);

    var decision = ai.Follow(
        scenario.Hand,
        scenario.LeadCards,
        scenario.CurrentWinningCards,
        scenario.Role,
        scenario.PartnerWinning);

    Assert.Equal(scenario.ExpectedOldAction, decision);
}
```

## 10. Feature Flag 建议
M1 推荐至少引入以下开关：

### 10.1 `UseRuleAIV21`
- 类型：bool
- 默认：false
- 含义：是否真正启用 v2.1 新路径结果

### 10.2 `EnableShadowCompare`
- 类型：bool
- 默认：true（开发/测试）
- 含义：是否在旧路径之外运行 shadow path 并输出对比日志

### 10.3 `ShadowSampleRate`
- 类型：double
- 默认：1.0
- 范围：0.0 ~ 1.0
- 含义：shadow path 的抽样比例

## 11. 5天排期
### Day 1：接口定义
任务：
- 新建 `PhaseKind.cs`
- 新建 `RuleAIContext.cs`
- 新建 `DecisionExplanation.cs`
- 写类型注释
- 编译通过

验收：
- 类型可编译
- 无业务逻辑变更

### Day 2：Context 组装
任务：
- 新建 `RuleAIContextBuilder.cs`
- 实现 `BuildLeadContext()`
- 实现 `BuildFollowContext()`
- 写 3 个 Builder 单测

验收：
- `RuleAIContextBuilder` 单测通过

### Day 3：接入解释日志
任务：
- 修改 `AIPlayer.Follow()`
- 修改 `AIPlayer.Lead()`
- 统一接入 `DecisionExplanation`
- 输出 `ai.decision`
- 输出 `ai.perf`

验收：
- 跑一局游戏能看到统一决策日志
- 旧路径行为不变

### Day 4：接入 Shadow Compare
任务：
- 增加 `UseRuleAIV21`
- 增加 `EnableShadowCompare`
- 增加 `ShadowSampleRate`
- 实现 `ai.compare`

验收：
- 可同时跑新旧路径
- 日志中有 `divergence`
- 默认仍返回旧路径结果

### Day 5：回归测试与基线固化
任务：
- 建立 `RegressionFixtures`
- 新建 `BaselineBehaviorTests.cs`
- 落首批 10 个场景
- 跑测试
- 记录初始性能基线

验收：
- 回归测试全部通过
- 有性能基线输出

## 12. M1 完成后的交付物
1. 新类型
- `PhaseKind`
- `RuleAIContext`
- `DecisionExplanation`
- `RuleAIContextBuilder`

2. AI 接入
- `AIPlayer` 的旧路径接入解释日志
- Feature Flag + Shadow Compare

3. 日志能力
- `ai.decision`
- `ai.compare`
- `ai.perf`

4. 测试能力
- `BaselineBehaviorTests`
- `RegressionFixtures`
- Builder 单测

5. 性能基线
- 决策耗时
- 候选数量
- shadow compare 耗时

## 13. M1 结束后的下一步
M1 完成后，才能安全进入 M2：
- 真正实现 `FollowPolicy2`
- 引入最小意图解析
- 引入第一版评分器
- 开始让新路径在小范围场景接管实际出牌

M1 不是为了“立刻变强”，而是为了让后续每一步变强都可观测、可验证、可回退。
