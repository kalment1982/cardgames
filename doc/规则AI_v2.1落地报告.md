# 规则AI v2.1 落地报告

## 1. 文档信息
- 状态：已落地
- 对应设计：`doc/规则AI架构设计_v2.1.md`
- 参考文档：`doc/tractor_rule_ai_design V2.md`
- 落地日期：2026-03-15

## 2. 落地范围
本次落地覆盖以下阶段与能力：
- 亮主/反主
- 埋底
- 首发
- 跟牌
- 统一上下文组装
- 统一意图解析
- 统一候选生成
- 统一评分器
- 统一解释日志
- 记牌快照与轻量推断

未包含：
- RL 接口实际接入
- 全局搜索/MCTS
- 神经网络策略

## 3. 架构落地结果
### 3.1 基础模型与上下文
已落地：
- `src/Core/AI/V21/RuleAIContext.cs`
- `src/Core/AI/V21/RuleAIModels.cs`
- `src/Core/AI/V21/RuleAIContextBuilder.cs`
- `src/Core/AI/V21/DecisionExplanation.cs`
- `src/Core/AI/V21/PhaseKind.cs`
- `src/Core/AI/V21/RuleAIOptions.cs`
- `src/Core/AI/V21/AIDecisionLogContext.cs`

能力：
- 支持 `Bid / BuryBottom / Lead / Follow` 四阶段上下文组装
- 支持 `RuleProfile / DifficultyProfile / StyleProfile`
- 支持 `HandProfile / MemorySnapshot / InferenceSnapshot / DecisionFrame`
- 支持底分风险、掉庄风险、比分压力、残局阶段统一建模

### 3.2 记牌与推断
已落地：
- `src/Core/AI/CardMemory.cs`
- `src/Core/AI/V21/MemorySnapshotBuilder.cs`
- `src/Core/AI/V21/InferenceEngine.cs`
- `src/Core/AI/V21/EndgamePolicy.cs`

新增能力：
- `GetPlayedCountSnapshot`
- `GetVoidSuitsSnapshot`
- `GetNoPairEvidenceSnapshot`
- `GetNoTractorEvidenceSnapshot`
- `GetPlayedTrumpCount`
- 拖拉机缺失证据跟踪
- 底分威胁、队友持稳概率、断门风险等轻量推断

### 3.3 意图与阶段策略
已落地：
- `src/Core/AI/V21/IntentResolver.cs`
- `src/Core/AI/V21/LeadPolicy2.cs`
- `src/Core/AI/V21/FollowPolicy2.cs`
- `src/Core/AI/V21/BuryPolicy2.cs`
- `src/Core/AI/V21/BidPolicy2.cs`

意图覆盖：
- `TakeScore`
- `ProtectBottom`
- `SaveControl`
- `PassToMate`
- `ForceTrump`
- `ShapeHand`
- `PreserveStructure`
- `TakeLead`
- `MinimizeLoss`
- `PrepareEndgame`
- `PrepareThrow`
- `AttackLongSuit`
- `ProbeWeakSuit`

### 3.4 候选生成与评分
已落地：
- `src/Core/AI/V21/LeadCandidateGenerator.cs`
- `src/Core/AI/V21/FollowCandidateGenerator.cs`
- `src/Core/AI/V21/BuryCandidateGenerator.cs`
- `src/Core/AI/V21/ActionScorer.cs`
- `src/Core/AI/V21/DecisionExplainer.cs`
- `src/Core/AI/V21/RuleAIUtility.cs`

策略特点：
- 首发优先考虑结构性候选，显式支持拖拉机、对子、安全甩牌
- 跟牌不再只看“最小合法”，而是区分送分、低成本反超、止损、保底
- 埋底不再只是最小牌，而是联合考虑分牌、主牌、结构、做短门
- 评分器统一计算收益、成本、风险，并输出结构化解释

## 4. 入口接线结果
### 4.1 AI 主入口
已改造：
- `src/Core/AI/AIPlayer.cs`

结果：
- `Lead` 已接入 `LeadPolicy2`
- `Follow` 已接入 `FollowPolicy2`
- `BuryBottom` 已接入 `BuryPolicy2`
- 保留旧路径、Feature Flag、Shadow Compare、解释日志、性能日志

### 4.2 亮主兼容入口
已改造：
- `src/Core/AI/Bidding/BidPolicy.cs`

结果：
- 对外接口不变
- 内部委托 `BidPolicy2`

### 4.3 上层调用点
已适配：
- `src/Core/AI/Evolution/LeagueArena/SelfPlayEngine.cs`
- `WebUI/Pages/GamePage.razor`
- `WebUI/Application/AITurnService.cs`
- `WebUI/Application/UiTestActionService.cs`

结果：
- WebUI 与自博弈流程可继续使用
- 埋底阶段可透传可见底牌信息

## 5. 行为变化摘要
### 5.1 跟牌
从：
- “合法集合里挑一手”

升级到：
- 队友领先时优先送分且避免无意义超压
- 对手领先但零分墩时，避免高成本硬抢
- 高分墩和残局时允许提升争墩强度
- 将结构损失、高牌损失、主牌消耗纳入统一评分

### 5.2 首发
从：
- 以局部启发式为主

升级到：
- 独立的首发候选与评分流程
- 优先识别拖拉机、对子、强结构候选
- 支持 `ForceTrump / AttackLongSuit / ProbeWeakSuit / PrepareThrow`
- 在非保底场景下提升结构性首发优先级

### 5.3 埋底
从：
- 偏“最小牌/低值牌”

升级到：
- 同时考虑保底、保主、保结构、做短门
- 高底分/高掉庄风险时显式触发 `ProtectBottom`

### 5.4 亮主
从：
- 独立策略

升级到：
- 接入统一 `RuleAIContext`
- 保留“提前亮主碰运气”机制
- 输出统一解释信息

## 6. 解释日志落地结果
日志事件：
- `ai.decision`
- `ai.compare`
- `ai.perf`

关键字段已落地：
- `phase_policy`
- `primary_intent`
- `secondary_intent`
- `selected_reason`
- `candidate_count`
- `top_candidates`
- `top_scores`
- `hard_rule_rejects`
- `risk_flags`
- `selected_action_features`

## 7. 测试矩阵
### 7.1 新增测试
- `tests/RuleAIContextBuilderTests.cs`
- `tests/V21/RuleAIProfileTests.cs`
- `tests/V21/HandProfileBuilderTests.cs`
- `tests/V21/MemorySnapshotBuilderTests.cs`
- `tests/V21/InferenceEngineTests.cs`
- `tests/V21/EndgamePolicyTests.cs`
- `tests/V21/IntentResolverTests.cs`
- `tests/V21/LeadCandidateGeneratorTests.cs`
- `tests/V21/FollowCandidateGeneratorTests.cs`
- `tests/V21/BuryCandidateGeneratorTests.cs`
- `tests/V21/ActionScorerTests.cs`
- `tests/V21/DecisionExplainerTests.cs`
- `tests/V21/LeadPolicy2Tests.cs`
- `tests/V21/FollowPolicy2Tests.cs`
- `tests/V21/BuryPolicy2Tests.cs`
- `tests/V21/BidPolicy2Tests.cs`
- `tests/V21/RuleAIOptionsTests.cs`
- `tests/V21/AIPlayerV21IntegrationTests.cs`

### 7.2 复用验证
- `tests/RegressionTests/BaselineBehaviorTests.cs`
- 既有 `AIPlayer`、规则校验、对局流程测试

## 8. 自测结果
### 8.1 测试
执行：
- `dotnet test TractorGame.csproj --no-restore -v minimal`

结果：
- `651` 个测试全部通过

### 8.2 构建
执行：
- `dotnet build TractorGame.csproj --no-restore -v minimal`
- `dotnet build tractor.sln --no-restore -v minimal`
- `dotnet build WebUI/WebUI.csproj --no-restore -v minimal`

结果：
- 全部通过

## 9. 风险与后续建议
当前已具备继续迭代的稳定基础，但仍建议下一步继续补强：
- 首发与甩牌的安全值模型可继续细化
- 末墩控制可增加更强的专项场景库
- 解释日志可继续补充 `candidate_features` 的可视化消费端
- RL 对接接口可在下一版本补齐 `RulePolicy / RuleValue / RuleFeatures`

## 10. 结论
`规则AI架构设计_v2.1` 已从“设计文档”落地为可运行、可测试、可解释的正式实现。

当前代码状态已经满足：
- 有统一架构
- 有统一入口
- 有阶段策略
- 有解释日志
- 有接口测试
- 有全量回归验证
