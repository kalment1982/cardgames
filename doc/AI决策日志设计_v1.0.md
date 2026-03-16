# AI决策日志设计 v1.0

## 1. 文档定位

- 本文档是当前项目 AI 决策日志的正式设计文档。
- 目标范围：现有 `WebUI + WebUIHost + Core AI` 架构。
- 设计重点：在不重写 UI 的前提下，补齐浏览器侧 AI 决策日志，并提供可直接排查问题的结构化决策包。

## 2. 背景问题

当前日志体系已经能较好记录：

- 对局事实：`turn.start / play.accept / trick.finish`
- 规则结果：赢家、分数、手牌快照、回放 Markdown

但仍难以快速定位 AI 问题，原因是：

- WebUI 为 `BlazorWebAssembly`，AI 在浏览器沙箱内执行。
- 浏览器侧 `AIPlayer` 会生成 `ai.decision / ai.compare / ai.perf`，但默认不写本地文件。
- 现有 `/api/log-entry` relay 主要承接 UI 事件，未统一承接 AI 决策日志。
- 排查时通常只能看到“出了什么牌”，看不到“为什么这么出”。

## 3. 设计目标

本次设计要解决以下问题：

1. 让 WebUI 实战中的 AI 决策日志稳定落盘。
2. 让一次 AI 决策可以通过一个 `decision_trace_id` 串起 summary、compare、perf 与 bundle。
3. 让排查人员可在 1~3 分钟内判断问题属于：
   - 上下文错误
   - 意图错误
   - 候选生成错误
   - 评分错误
   - 执行映射错误
4. 保留现有 raw/replay 日志体系，不引入破坏性变更。

## 4. 非目标

- 不在本版本中实现完整的决策可视化页面。
- 不在本版本中为所有 legacy 路径补齐完整特征工程解释。
- 不将 AI 真实决策改为服务端托管。
- 不把所有中间局部变量无差别 dump 到日志中。

## 5. 输出物

本设计要求产出三类日志：

### 5.1 Raw JSONL

路径：

- `logs/raw/YYYY-MM-DD/tractor-YYYY-MM-DD-HH.jsonl`

用途：

- 全量事件流水
- 机器检索
- 与现有审计日志统一存档

新增/保留的 AI 事件：

- `ai.decision`
- `ai.compare`
- `ai.perf`
- `ai.bundle`

### 5.2 Replay Markdown

路径：

- `logs/replay/YYYY-MM-DD/tractor-<game_id>.md`

用途：

- 对局事实回放
- 文本时间线

约束：

- 不在 Markdown 中展开完整决策包正文，避免回放文件膨胀。
- 允许在文本事件流中保留 `ai.decision / ai.compare / ai.perf / ai.bundle` 的摘要行。

### 5.3 Decision Bundle JSON

路径：

- `logs/decision/YYYY-MM-DD/<round_id>/<trick_id>/<decision_trace_id>.json`

用途：

- 单次 AI 决策排查主入口
- 记录关键数据结构、中间决策、候选及评分
- 支持沉淀问题样例与回归基线

## 6. 核心设计

## 6.1 决策追踪单元

每一次 AI 决策定义为一个 `decision_trace`。

最小唯一键：

- `round_id`
- `trick_id`
- `turn_id`
- `player_index`
- `phase`

对外统一使用：

- `decision_trace_id`

要求：

- 同一决策产生的 `ai.decision / ai.compare / ai.perf / ai.bundle` 必须共享同一个 `decision_trace_id`
- `decision_trace_id` 也写入 `correlation_id`

## 6.2 日志分层

### Summary 层

面向快速查看，记录：

- 最终选择
- 意图
- 原因
- 对比
- 性能

对应事件：

- `ai.decision`
- `ai.compare`
- `ai.perf`

### Bundle 层

面向问题排查，记录：

- 上下文快照
- 意图快照
- 候选详情
- 比较与打分
- 调试真值快照

对应事件：

- `ai.bundle`

## 6.3 Bundle 内容结构

`ai.bundle` 必须包含以下 section：

1. `meta`
2. `context_snapshot`
3. `intent_snapshot`
4. `candidate_details`
5. `selected_action`
6. `compare_snapshot`
7. `perf_snapshot`
8. `truth_snapshot`
9. `algorithm_trace`

### 6.3.1 meta

记录：

- `decision_trace_id`
- `path`
- `phase`
- `difficulty`
- `player_index`
- `role`

### 6.3.2 context_snapshot

记录 AI 真正可见的输入：

- `my_hand`
- `lead_cards`
- `current_winning_cards`
- `visible_bottom_cards`
- `trick_score`
- `cards_left_min`
- `trick_index`
- `turn_index`
- `play_position`
- `dealer_index`
- `current_winning_player`
- `partner_winning`
- `game_config`
- `hand_profile`
- `memory_snapshot`
- `inference_snapshot`
- `decision_frame`

### 6.3.3 intent_snapshot

记录：

- `primary_intent`
- `secondary_intent`
- `selected_reason`
- `phase_policy`
- `risk_flags`
- `tags`

### 6.3.4 candidate_details

每个候选记录：

- `candidate_index`
- `cards`
- `score`
- `reason_code`
- `features`
- `is_selected`

约束：

- 默认保留全部候选，但允许通过配置限制最大数量
- legacy 路径若无法提供完整 feature，可为空对象，但仍保留候选卡组

### 6.3.5 compare_snapshot

记录 shadow compare 结果：

- `shadow_compared`
- `divergence`
- `old_path`
- `new_path`
- `old_action`
- `new_action`
- `old_reason`
- `new_reason`
- `old_intent`
- `new_intent`

### 6.3.6 perf_snapshot

记录：

- `total_ms`
- `context_ms`
- `legacy_ms`
- `shadow_ms`
- `candidate_count`
- `selected_score`

### 6.3.7 truth_snapshot

只用于调试，不参与 AI 决策：

- `hands_by_player`
- `current_trick`
- `bottom_cards`
- `defender_score`
- `current_player`

要求：

- 必须明确标注为调试真值
- 不得在 AI 逻辑中反向读取

### 6.3.8 algorithm_trace

记录算法痕迹，而不是源码文本：

- `policy_module`
- `path`
- `shadow_mode`
- `use_rule_ai_v21`
- `candidate_count`
- `compare_enabled`

## 7. 配置设计

在 `WebUI/wwwroot/appsettings.json` 中新增 `RuleAI` 日志配置：

- `UseRuleAIV21`
- `EnableShadowCompare`
- `ShadowSampleRate`
- `DecisionTraceEnabled`
- `DecisionTraceIncludeTruthSnapshot`
- `DecisionTraceMaxCandidates`

默认建议：

- `UseRuleAIV21 = true`
- `EnableShadowCompare = true`
- `ShadowSampleRate = 1.0`
- `DecisionTraceEnabled = true`
- `DecisionTraceIncludeTruthSnapshot = true`
- `DecisionTraceMaxCandidates = 16`

## 8. 排查流程

推荐固定按以下顺序排查：

1. 看 `turn.start / play.accept / trick.finish` 确认事实
2. 打开对应 `logs/decision/.../<decision_trace_id>.json`
3. 看 `context_snapshot` 是否正确
4. 看 `intent_snapshot` 是否合理
5. 看 `candidate_details` 中是否包含正确动作
6. 若包含但未选中，检查 `score / reason_code / features`
7. 结合 `compare_snapshot` 判断是 `legacy` 错还是 `V21` 错

## 9. V1.0 实施范围

本版本必须完成：

- WebUI 浏览器侧 AI 日志 relay
- `ai.decision / ai.compare / ai.perf / ai.bundle` 落盘
- Decision Bundle JSON 文件输出
- `decision_trace_id` 串联
- Truth Snapshot 调试快照

本版本可暂不完成：

- 单独的 `ai.context / ai.intent / ai.score` 独立事件
- Web 页面内置决策查看器

## 10. 验收标准

满足以下条件视为通过：

1. WebUI 实战中每次 AI 出牌都能看到 `ai.decision`
2. 当启用 shadow compare 时能看到 `ai.compare`
3. 每次 AI 决策都能在 `logs/decision` 下找到 bundle JSON
4. bundle 中能看到：
   - 手牌
   - 当前赢家
   - 主要意图
   - 候选列表
   - 打分
   - 最终选择
   - 真值快照
5. 能通过至少 3 局自动对局验证日志完整性与可读性
