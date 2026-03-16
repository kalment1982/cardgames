# AI决策日志事件字典与 DecisionBundle Schema v1.0

## 1. 文档定位

- 本文档定义 AI 决策日志事件口径与 `ai.bundle` 的结构化 schema。
- 适用范围：现有 `WebUI / WebUIHost / Core AI`。
- 与 `AI决策日志设计_v1.0.md` 配套使用。

## 2. 事件字典

| 事件 | category | level | 说明 | 必填核心字段 |
|---|---|---|---|---|
| `ai.decision` | `decision` | `INFO` | 单次 AI 决策摘要 | `decision_trace_id`, `phase`, `path`, `player_index`, `primary_intent`, `selected_reason`, `selected_cards` |
| `ai.compare` | `diag` | `INFO` | legacy 与 V21 对照结果 | `decision_trace_id`, `phase`, `divergence`, `old_path`, `new_path`, `old_action`, `new_action` |
| `ai.perf` | `perf` | `INFO` | 决策耗时与规模信息 | `decision_trace_id`, `phase`, `path`, `player_index`, `candidate_count`, `selected_count` |
| `ai.bundle` | `diag` | `INFO` | 单次 AI 决策的完整结构化调试包 | `decision_trace_id`, `phase`, `path`, `player_index`, `bundle_version`, `context_snapshot`, `candidate_details`, `selected_action` |

## 3. 事件字段口径

## 3.1 `ai.decision`

### payload

| 字段 | 类型 | 说明 |
|---|---|---|
| `decision_trace_id` | string | 单次决策唯一追踪 ID |
| `phase` | string | `Lead / Follow / BuryBottom / Bid` |
| `path` | string | `legacy / rule_ai_v21_*` |
| `phase_policy` | string | 具体 phase policy 名称 |
| `difficulty` | string | `Easy / Medium / Hard / Expert` |
| `player_index` | int | 决策玩家 |
| `role` | string | `Dealer / DealerPartner / Opponent` |
| `partner_winning` | bool | 当前是否队友暂时领先 |
| `primary_intent` | string | 主要意图 |
| `secondary_intent` | string | 次要意图 |
| `selected_reason` | string | 最终选择原因码 |
| `candidate_count` | int | 候选数量 |
| `selected_cards` | card[] | 选中的卡组 |
| `top_candidates` | string[] | 摘要展示的候选 |
| `top_scores` | number[] | 候选分数摘要 |
| `hard_rule_rejects` | string[] | 硬规则拒绝原因 |
| `risk_flags` | string[] | 风险标记 |
| `selected_action_features` | object | 选中动作的特征分解 |
| `tags` | string[] | 标签 |
| `hand_count` | int | 当前手牌数 |
| `trump_count` | int | 当前主牌数 |
| `point_card_count` | int | 当前分牌数 |
| `trick_score` | int | 当前墩分 |
| `cards_left_min` | int | 最少剩余手牌数 |

### metrics

| 字段 | 类型 | 说明 |
|---|---|---|
| `total_ms` | number | 总耗时 |
| `context_ms` | number | 上下文组装耗时 |
| `legacy_ms` | number | legacy 决策耗时 |
| `shadow_ms` | number | shadow compare 耗时 |
| `selected_score` | number | 选中动作的分数 |

## 3.2 `ai.compare`

### payload

| 字段 | 类型 | 说明 |
|---|---|---|
| `decision_trace_id` | string | 单次决策追踪 ID |
| `phase` | string | 决策阶段 |
| `player_index` | int | 玩家 |
| `role` | string | 角色 |
| `partner_winning` | bool | 当前队友是否暂时领先 |
| `divergence` | bool | 新旧路径结果是否分歧 |
| `old_path` | string | 旧路径名 |
| `new_path` | string | 新路径名 |
| `old_action` | card[] | 旧路径动作 |
| `new_action` | card[] | 新路径动作 |
| `old_reason` | string | 旧路径原因 |
| `new_reason` | string | 新路径原因 |
| `old_intent` | string | 旧路径意图 |
| `new_intent` | string | 新路径意图 |

### metrics

| 字段 | 类型 | 说明 |
|---|---|---|
| `old_candidate_count` | number | 旧路径候选数 |
| `new_candidate_count` | number | 新路径候选数 |

## 3.3 `ai.perf`

### payload

| 字段 | 类型 | 说明 |
|---|---|---|
| `decision_trace_id` | string | 单次决策追踪 ID |
| `phase` | string | 决策阶段 |
| `path` | string | 实际采用路径 |
| `player_index` | int | 玩家 |
| `candidate_count` | int | 候选数 |
| `selected_count` | int | 选中牌数 |
| `shadow_compared` | bool | 是否进行了 shadow compare |

### metrics

| 字段 | 类型 | 说明 |
|---|---|---|
| `total_ms` | number | 总耗时 |
| `context_ms` | number | 上下文耗时 |
| `legacy_ms` | number | legacy 耗时 |
| `shadow_ms` | number | shadow compare 耗时 |

## 3.4 `ai.bundle`

### payload 顶层

| 字段 | 类型 | 说明 |
|---|---|---|
| `decision_trace_id` | string | 单次决策追踪 ID |
| `bundle_version` | string | 当前 bundle schema 版本 |
| `phase` | string | 决策阶段 |
| `path` | string | 实际采用路径 |
| `player_index` | int | 玩家 |
| `bundle` | object | 决策包正文 |

## 4. DecisionBundle Schema

## 4.1 bundle 对象

```json
{
  "meta": {},
  "context_snapshot": {},
  "intent_snapshot": {},
  "candidate_details": [],
  "selected_action": {},
  "compare_snapshot": {},
  "perf_snapshot": {},
  "truth_snapshot": {},
  "algorithm_trace": {}
}
```

## 4.2 `meta`

```json
{
  "decision_trace_id": "follow_p2_trick_0009_turn_0003",
  "phase": "Follow",
  "path": "rule_ai_v21_follow_policy2",
  "difficulty": "Hard",
  "player_index": 2,
  "role": "DealerPartner",
  "session_id": "sess_xxx",
  "game_id": "game_xxx",
  "round_id": "round_xxx",
  "trick_id": "trick_0009",
  "turn_id": "turn_0003"
}
```

## 4.3 `context_snapshot`

```json
{
  "trick_index": 9,
  "turn_index": 3,
  "play_position": 3,
  "dealer_index": 0,
  "current_winning_player": 0,
  "partner_winning": true,
  "trick_score": 5,
  "cards_left_min": 17,
  "my_hand": [],
  "lead_cards": [],
  "current_winning_cards": [],
  "visible_bottom_cards": [],
  "game_config": {
    "trump_suit": "Diamond",
    "level_rank": "Two"
  },
  "hand_profile": {},
  "memory_snapshot": {},
  "inference_snapshot": {},
  "decision_frame": {}
}
```

## 4.4 `intent_snapshot`

```json
{
  "primary_intent": "PassToMate",
  "secondary_intent": "Unknown",
  "selected_reason": "follow_support_partner",
  "phase_policy": "FollowPolicy2",
  "risk_flags": [],
  "tags": ["FollowPolicy2", "balance"]
}
```

## 4.5 `candidate_details[]`

```json
{
  "candidate_index": 0,
  "cards": [],
  "score": 3.24,
  "reason_code": "pass_to_mate_send_points",
  "features": {
    "WinNowValue": 0.0,
    "PointSwingValue": 0.5,
    "TrumpConsumptionCost": 0.0
  },
  "is_selected": true
}
```

约束：

- `candidate_index` 按排序后顺序编号
- `is_selected=true` 的候选必须且只能有一个
- legacy 路径无 `features` 时记为 `{}`，无 `score` 时记为 `null`

## 4.6 `selected_action`

```json
{
  "cards": [],
  "score": 3.24,
  "reason_code": "pass_to_mate_send_points"
}
```

## 4.7 `compare_snapshot`

```json
{
  "shadow_compared": true,
  "divergence": true,
  "old_path": "legacy",
  "new_path": "rule_ai_v21_follow_policy2",
  "old_action": [],
  "new_action": [],
  "old_reason": "follow_support_partner",
  "new_reason": "protect_mate_lead_block_overtake",
  "old_intent": "PassToMate",
  "new_intent": "ProtectBottom"
}
```

## 4.8 `perf_snapshot`

```json
{
  "total_ms": 4.21,
  "context_ms": 0.38,
  "legacy_ms": 0.44,
  "shadow_ms": 3.11,
  "candidate_count": 4,
  "selected_score": 3.24
}
```

## 4.9 `truth_snapshot`

```json
{
  "enabled": true,
  "current_player": 2,
  "defender_score": 45,
  "hands_by_player": [],
  "current_trick": [],
  "bottom_cards": []
}
```

约束：

- `enabled=false` 时允许仅保留开关标记
- `truth_snapshot` 仅用于调试，不得回灌到 AI 决策逻辑

## 4.10 `algorithm_trace`

```json
{
  "policy_module": "rule_ai_v21_follow_policy2",
  "path": "rule_ai_v21_follow_policy2",
  "use_rule_ai_v21": true,
  "compare_enabled": true,
  "candidate_count": 4
}
```

## 5. 文件落盘规则

### Raw JSONL

- 保留完整 `ai.bundle` 事件
- 用于全局检索与审计

### Decision Bundle JSON

- 对每个 `ai.bundle` 事件，宿主侧额外输出一份 pretty JSON 文件
- 文件内容为完整 `LogEntry` 对象
- 文件名优先使用 `decision_trace_id`

推荐路径：

```text
logs/decision/YYYY-MM-DD/<round_id>/<trick_id>/<decision_trace_id>.json
```

## 6. 验收检查清单

一次 AI 决策完成后，应能同时查到：

1. `ai.decision`
2. `ai.perf`
3. 可选 `ai.compare`
4. 对应 `logs/decision/.../<decision_trace_id>.json`

并且 bundle 文件中至少能回答：

1. AI 当时手里有什么牌
2. 它认为当前赢家是谁
3. 它当时的主要意图是什么
4. 它生成了哪些候选
5. 正确动作是否在候选里
6. 为什么最终选了当前动作
