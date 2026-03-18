# PPO AI C#训练引擎桥接设计 v1.0

## 1. 文档定位

本文档用于定义 `PPO AI` 在阶段 1 使用 `C# 正式引擎 + stdio JSON bridge` 的最小桥接方案。

本文档只回答以下问题：

1. 为什么 `PPO AI` 训练环境应切到 C# 正式引擎
2. `PPO AI` 的 C# 训练引擎 Host 需要包含哪些内容
3. Python 与 C# 之间如何通过 `stdio JSON bridge` 交互
4. 阶段 1 的最小 API 和最小 JSON schema 是什么

本文档不负责描述：

1. PPO 网络结构
2. PPO 超参数
3. 奖励函数细节
4. PPO observation 编码细节

这些内容应分别留在：

- `doc/PPO训练架构设计_v1.0.md`
- `doc/AI共享决策输入定义_v1.0.md`
- `doc/AI共享决策输入编码定义_v1.0.md`

---

## 2. 方案结论

阶段 1 的 `PPO AI` 训练环境采用：

- `C# 正式引擎`
- `stdio JSON bridge`
- `Python 负责训练，C# 负责规则与环境推进`

不采用：

1. `rl_training/game_engine.py` 作为主训练环境
2. `pythonnet` 直接嵌入调用 C# 运行时
3. 本地 HTTP / gRPC 作为第一版桥接方式

---

## 3. 为什么选择 C# 正式引擎

### 3.1 规则一致性

当前正式规则与大量 bug fix 已经沉淀在 C# 引擎中，主要包括：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/Game.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/PlayValidator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/FollowValidator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/ThrowValidator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/TrickJudge.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/LevelManager.cs`

若继续使用 Python 简化环境作为主训练环境，会导致：

1. 训练规则与正式规则不一致
2. PPO 学到的行为无法直接迁移到正式游戏
3. 很难区分“PPO 没学会”还是“环境口径不一致”

### 3.2 当前仓库已有桥接雏形

当前仓库已存在可复用思路：

- `/Users/karmy/Projects/CardGame/tractor/rl_training/bridge_server.py`
- `/Users/karmy/Projects/CardGame/tractor/tools/PpoBridgeEval/Program.cs`

说明：

- 当前项目已经验证过 Python 与 C# 进程间通信的基本可行性
- 阶段 1 不需要从零设计跨语言调用模式

### 3.3 stdio JSON bridge 最轻量

第一版优先考虑：

1. 实现简单
2. 调试直接
3. 不引入额外服务框架
4. 不增加复杂部署依赖

因此优先选择：

- `单个 C# Host 进程`
- `stdin 请求`
- `stdout 响应`
- `JSON 作为协议载体`

---

## 4. 引擎包括哪些内容

从 `PPO AI` 训练环境角度看，C# 训练引擎至少包括 6 类能力。

### 4.1 游戏状态与主循环

职责：

- 管理整局生命周期
- 管理当前 phase、当前玩家、当前墩、当前回合
- 管理整局分数、庄家、已完成墩

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/Game.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/GameState.cs`

### 4.2 发牌与底牌

职责：

- 洗牌
- 发牌
- 保留底牌
- 维护发牌顺序和庄家初始状态

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/Deck.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/DealingPhase.cs`

### 4.3 亮主 / 反主

职责：

- 校验亮主合法性
- 决定当前主花色 / 无主
- 管理亮主优先级

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/TrumpBidding.cs`

阶段 1 约束：

- 亮主 / 反主先由 C# 引擎内部规则逻辑或现有 RuleAI 自动完成
- 不进入 PPO 决策空间

### 4.4 埋底

职责：

- 庄家拿到底牌
- 选择埋底
- 管理埋底后的手牌与底牌状态

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/BottomBurying.cs`

阶段 1 约束：

- 埋底先由 C# 引擎内部规则逻辑或现有 RuleAI 自动完成
- 不进入 PPO 决策空间

### 4.5 规则校验器

职责：

- 提供出牌合法性真值源
- 提供合法动作边界

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/PlayValidator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/FollowValidator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/ThrowValidator.cs`

必须覆盖：

1. 首发合法性
2. 跟牌合法性
3. 甩牌合法性与成功判定
4. 多张结构约束

### 4.6 赢家判定、计分与升级

职责：

- 判定每一墩赢家
- 统计每一墩得分
- 统计整局得分
- 计算升级数和下一局庄家

主要代码来源：

- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/TrickJudge.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/Rules/ScoreCalculator.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/GameFlow/LevelManager.cs`

---

## 5. 阶段 1 最小范围

阶段 1 的 `PPO AI` 训练桥接只支持：

1. 整局训练环境由 C# 引擎推进
2. PPO 只接管 `PlayTricks`
3. 亮主 / 反主 / 埋底由 C# 侧自动完成
4. Python 不负责规则判定

不在阶段 1 实现：

1. Python 自己枚举合法动作
2. Python 自己判定胜负
3. Python 自己维护整局牌面规则

---

## 6. C# EngineHost 职责边界

建议单独实现一个 `PPO AI EngineHost`，而不是直接复用 WebUI Host。

`EngineHost` 只负责：

1. 管理一个或多个 `Game` 实例
2. 对外暴露训练环境 API
3. 生成合法动作集合
4. 执行动作并推进环境
5. 返回状态快照、终局结果和奖励基础数据

`EngineHost` 不负责：

1. WebUI 页面
2. 浏览器日志上传
3. 线上对局入口
4. RuleAI 完整策略解释日志

---

## 7. 最小 API 设计

阶段 1 最小 API 建议如下。

### 7.1 `reset`

作用：

- 新开一局
- 自动完成发牌、亮主、埋底
- 将环境推进到第一个需要 PPO 决策的位置

请求字段：

- `type = "reset"`
- `seed`
- `ppo_seats`
- `rule_ai_seats`

返回字段：

- `ok`
- `env_id`
- `done`
- `current_player`
- `legal_actions`
- `state_snapshot`
- `meta`

### 7.2 `step`

作用：

- 接收 PPO 选择的动作
- 执行动作
- 让当前动作之后、直到下一个 PPO 座位之前的其他座位按既定策略自动走完
- 推进到下一个 PPO 决策点或终局

请求字段：

- `type = "step"`
- `env_id`
- `action_slot`

返回字段：

- `ok`
- `done`
- `current_player`
- `legal_actions`
- `state_snapshot`
- `terminal_result`

### 7.3 `get_state_snapshot`

作用：

- 返回当前局面的结构化状态
- 供 Python 侧做 observation 编码与调试

请求字段：

- `type = "get_state_snapshot"`
- `env_id`

返回字段：

- `ok`
- `state_snapshot`

### 7.4 `get_legal_actions`

作用：

- 返回当前 PPO 可选的全部合法动作
- 用于构造固定动作空间下的 `legal mask`

请求字段：

- `type = "get_legal_actions"`
- `env_id`

返回字段：

- `ok`
- `legal_actions`

### 7.5 `close`

作用：

- 关闭环境或整个 Host

请求字段：

- `type = "close"`
- `env_id` 或 `scope = "host"`

返回字段：

- `ok`

---

## 8. legal_actions 设计要求

`legal_actions` 的定义必须满足：

1. 来源于 C# 正式规则，不来源于 RuleAI 候选偏好
2. 表示“当前状态下全部合法动作”
3. 每个合法动作必须能映射到固定动作空间 slot
4. 不允许静默裁剪

每个 action 至少应包含：

- `slot`
- `cards`
- `pattern_type`
- `is_lead`
- `is_follow`
- `is_throw`
- `is_trump_cut`

说明：

- `slot` 用于 PPO 动作空间
- `cards` 用于环境真实执行
- 其他字段用于日志和调试

---

## 9. state_snapshot 最小结构

`state_snapshot` 应服务于：

1. PPO observation 编码
2. 日志排查
3. 训练中断重放

阶段 1 最小建议字段：

- `env_id`
- `seed`
- `phase`
- `dealer`
- `current_player`
- `trump_suit`
- `is_no_trump`
- `level_rank`
- `hands`
  - 仅当前 PPO 控制玩家手牌可见
- `played_tricks`
- `current_trick`
- `current_trick_score`
- `current_winning_player`
- `defender_score`
- `tricks_remaining`
- `cards_left_by_player`

说明：

- 这里返回的是结构化状态
- 具体 observation 编码方式由 Python 侧按共享编码文档处理

---

## 10. terminal_result 最小结构

终局时应返回：

- `winner_team`
- `my_team_won`
- `my_team_final_score`
- `my_team_level_gain`
- `defender_score`
- `next_dealer`

作用：

- 支撑 PPO 终局奖励计算
- 支撑评估脚本

---

## 11. JSON 协议示例

### 11.1 reset 请求

```json
{
  "type": "reset",
  "seed": 12345,
  "ppo_seats": [0, 2],
  "rule_ai_seats": [1, 3]
}
```

### 11.2 reset 响应

```json
{
  "ok": true,
  "type": "reset_result",
  "env_id": "env_0001",
  "done": false,
  "current_player": 0,
  "legal_actions": [],
  "state_snapshot": {
    "phase": "PlayTricks",
    "dealer": 1,
    "current_player": 0,
    "is_lead": true,
    "my_seat": 0,
    "my_role": "defender",
    "trump_suit": "Spade",
    "is_no_trump": false,
    "level_rank": "Two",
    "current_trick": [],
    "lead_cards": [],
    "current_winning_cards": [],
    "current_trick_score": 0,
    "current_winning_player": -1,
    "defender_score": 0,
    "trick_index": 0,
    "play_position": 0,
    "cards_left_by_player": [25, 25, 25, 25],
    "played_trick_count": 0,
    "terminal": false
  }
}
```

### 11.3 step 请求

```json
{
  "type": "step",
  "env_id": "env_0001",
  "action_slot": 37
}
```

### 11.4 step 响应

```json
{
  "ok": true,
  "type": "step_result",
  "env_id": "env_0001",
  "done": false,
  "current_player": 2,
  "legal_actions": [],
  "state_snapshot": {
    "phase": "PlayTricks",
    "dealer": 1,
    "current_player": 2,
    "is_lead": false,
    "my_seat": 2,
    "my_role": "defender",
    "trump_suit": "Spade",
    "is_no_trump": false,
    "level_rank": "Two",
    "lead_cards": [
      { "suit": "Heart", "rank": "Ten", "text": "♥10" }
    ],
    "current_winning_cards": [
      { "suit": "Heart", "rank": "Ace", "text": "♥A" }
    ],
    "current_trick_score": 10,
    "current_winning_player": 1,
    "defender_score": 20,
    "trick_index": 7,
    "play_position": 2,
    "cards_left_by_player": [18, 18, 18, 18],
    "played_trick_count": 7,
    "terminal": false
  }
}
```

---

## 12. 精确 JSON Schema

本节定义阶段 1 可直接编码实现的最小协议结构。

### 12.1 通用请求结构

所有请求统一包含：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `type` | string | 是 | 请求类型，例如 `reset` / `step` / `get_state_snapshot` |
| `request_id` | string | 否 | 请求唯一标识，便于日志与调试 |

### 12.2 通用响应结构

所有响应统一包含：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `ok` | bool | 是 | 是否成功 |
| `type` | string | 是 | 响应类型 |
| `request_id` | string | 否 | 回传请求 ID |
| `error_code` | string | 否 | 失败时的错误码 |
| `error_message` | string | 否 | 失败时的人类可读错误信息 |

### 12.3 `reset` 请求

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `type` | string | 是 | 固定为 `reset` |
| `seed` | int | 是 | 随机种子 |
| `ppo_seats` | int[] | 是 | PPO 控制座位，阶段 1 固定为长度 2 |
| `rule_ai_seats` | int[] | 是 | RuleAI 控制座位，阶段 1 固定为长度 2 |
| `auto_phase_policy` | object | 否 | 非 PPO 阶段自动策略配置 |

`auto_phase_policy` 建议字段：

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `auto_bid` | bool | 否 | 是否自动亮主，默认 `true` |
| `auto_bury` | bool | 否 | 是否自动埋底，默认 `true` |
| `rule_ai_difficulty` | string | 否 | 非 PPO 座位使用的 RuleAI 难度 |

### 12.4 `reset` 响应

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `ok` | bool | 是 | 是否成功 |
| `type` | string | 是 | 固定为 `reset_result` |
| `env_id` | string | 是 | 环境实例 ID |
| `done` | bool | 是 | 是否已直接终局 |
| `current_player` | int | 是 | 当前应行动玩家 |
| `legal_actions` | object[] | 是 | 当前全部合法动作 |
| `state_snapshot` | object | 是 | 当前状态快照 |
| `meta` | object | 否 | 额外元信息 |

### 12.5 `step` 请求

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `type` | string | 是 | 固定为 `step` |
| `env_id` | string | 是 | 环境实例 ID |
| `action_slot` | int | 是 | PPO 选中的固定动作槽位 |

### 12.6 `step` 响应

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `ok` | bool | 是 | 是否成功 |
| `type` | string | 是 | 固定为 `step_result` |
| `env_id` | string | 是 | 环境实例 ID |
| `done` | bool | 是 | 是否终局 |
| `current_player` | int | 否 | 下一个 PPO 决策玩家；若终局可为空 |
| `legal_actions` | object[] | 否 | 下一个 PPO 决策点的合法动作 |
| `state_snapshot` | object | 是 | 新状态快照 |
| `terminal_result` | object | 否 | 终局才返回；Python 侧基于它计算最终 reward |

### 12.7 `legal_action` 结构

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `slot` | int | 是 | 固定动作空间槽位 |
| `cards` | object[] | 是 | 该动作对应的具体牌组 |
| `pattern_type` | string | 是 | `single / pair / tractor / mixed / throw` |
| `system` | string | 否 | 所属体系，例如 `spade / heart / trump` |
| `is_lead` | bool | 是 | 是否首发动作 |
| `is_follow` | bool | 是 | 是否跟牌动作 |
| `is_throw` | bool | 是 | 是否甩牌动作 |
| `is_trump_cut` | bool | 是 | 是否属于主牌压制/毙牌 |
| `debug_key` | string | 否 | 调试稳定 key，用于日志核对 |

### 12.8 `state_snapshot` 结构

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `phase` | string | 是 | 当前阶段，阶段 1 应为 `PlayTricks` |
| `dealer` | int | 是 | 庄家座位 |
| `current_player` | int | 是 | 当前应行动玩家 |
| `is_lead` | bool | 是 | 当前 PPO 玩家是否为本墩首发 |
| `trump_suit` | string | 否 | 主花色 |
| `is_no_trump` | bool | 是 | 是否无主 |
| `level_rank` | string | 是 | 当前级牌 |
| `my_seat` | int | 是 | 当前 observation 所属 PPO 玩家座位 |
| `my_role` | string | 是 | `dealer / dealer_partner / defender` |
| `my_hand` | object[] | 是 | 当前 PPO 玩家手牌 |
| `current_trick` | object[] | 是 | 当前墩已出牌记录 |
| `lead_cards` | object[] | 是 | 当前墩首发牌 |
| `current_winning_cards` | object[] | 是 | 当前领先牌组 |
| `current_winning_player` | int | 是 | 当前赢家 |
| `current_trick_score` | int | 是 | 当前墩累计分数 |
| `defender_score` | int | 是 | 闲家方得分 |
| `trick_index` | int | 是 | 当前第几墩 |
| `play_position` | int | 是 | 当前玩家在本墩出牌位置 |
| `cards_left_by_player` | int[] | 是 | 四家剩余手牌数 |
| `played_trick_count` | int | 是 | 已完成墩数 |
| `terminal` | bool | 是 | 是否终局 |

### 12.9 `terminal_result` 结构

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `winner_team` | string | 是 | `ppo / opponent` |
| `my_team_won` | bool | 是 | PPO 队是否获胜 |
| `my_team_final_score` | int | 是 | PPO 控制一队的最终得分 |
| `my_team_level_gain` | int | 是 | PPO 控制一队本局升级数 |
| `defender_score` | int | 是 | 闲家总分 |
| `next_dealer` | int | 是 | 下一局庄家座位 |

### 12.10 `card` 结构

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `suit` | string | 是 | `Spade / Heart / Club / Diamond / Joker` |
| `rank` | string | 是 | `Two .. Ace / SmallJoker / BigJoker` |
| `score` | int | 否 | 分值，便于调试 |
| `text` | string | 否 | 展示文本，便于调试 |

### 12.11 `current_trick` 中单条 play 结构

| 字段 | 类型 | 必填 | 说明 |
|---|---|---|---|
| `player_index` | int | 是 | 出牌玩家 |
| `cards` | object[] | 是 | 该玩家当前墩打出的牌 |

### 12.12 奖励计算职责

阶段 1 的奖励计算职责明确如下：

1. C# `EngineHost` 不按 PPO 奖励公式计算最终 reward
2. C# 只返回 `terminal_result` 原始结算数据
3. Python 训练侧基于：
   - `my_team_won`
   - `my_team_level_gain`
   - `my_team_final_score`
   计算最终奖励

原因：

1. 奖励公式后续可能调整
2. 奖励逻辑属于 RL 训练逻辑，不属于环境真值源
3. 这样调整奖励公式时不需要修改 C# Host

---

## 13. 错误码建议

建议 `EngineHost` 至少返回以下错误码：

| 错误码 | 说明 |
|---|---|
| `ENV_NOT_FOUND` | `env_id` 无效或环境不存在 |
| `INVALID_REQUEST` | 请求结构不合法 |
| `INVALID_ACTION_SLOT` | 提交的 `action_slot` 不存在 |
| `ACTION_SLOT_NOT_LEGAL` | `action_slot` 当前不在合法动作集合中 |
| `PHASE_NOT_PLAY_TRICKS` | 阶段不在 PPO 接管范围内 |
| `ENGINE_INTERNAL_ERROR` | C# 引擎内部异常 |
| `ACTION_SPACE_OVERFLOW` | 当前合法动作数量超过固定动作空间容量 |

要求：

1. 不允许用单一字符串错误代替结构化错误码
2. `ACTION_SPACE_OVERFLOW` 必须显式暴露，不能静默裁剪

---

## 14. 日志要求

桥接层至少记录以下日志：

### 14.1 Host 运行日志

- `request_id`
- `env_id`
- `seed`
- `request_type`
- `elapsed_ms`
- `ok`
- `error_code`

### 14.2 训练调试日志

- 当前玩家
- `legal_action_count`
- `selected_action_slot`
- `selected_action_debug_key`
- `done`
- `terminal_result_present`

### 14.3 异常日志

- 反序列化失败
- 非法动作槽位
- 动作空间溢出
- C# 引擎异常栈

---

## 15. 实现任务拆分

建议按 5 个子任务落地。

### T1. 新建 `PPO AI EngineHost`

目标：

- 创建一个独立于 WebUI 的 C# 控制台 Host
- 支持 `stdin/stdout JSON`

产出：

- 新项目目录，例如 `tools/PpoEngineHost/`
- 主循环入口 `Program.cs`

### T2. 封装环境实例管理

目标：

- 一个 `env_id` 对应一个 `Game` 实例
- 支持 `reset / step / close`

产出：

- `EnvironmentManager`
- `EnvironmentSession`

### T3. 状态快照与合法动作导出

目标：

- 从 `Game` 导出 `state_snapshot`
- 从正式规则导出全部 `legal_actions`

产出：

- `StateSnapshotBuilder`
- `LegalActionExporter`

### T4. 动作槽位映射

目标：

- 将合法动作映射到固定动作空间 slot
- 校验 `slot -> concrete action` 唯一可解码

产出：

- `ActionSlotMapper`
- `ActionMaskBuilder`

### T5. Python 接入与回放验证

目标：

- Python 训练侧能通过 bridge 与 C# 引擎通信
- 固定 seed 下结果可复现

产出：

- Python 客户端封装
- 最小联调脚本
- 固定 seed 回放验证脚本

---

## 16. 验证清单

桥接层完成后，至少做以下验证：

1. `reset` 后能稳定进入第一个 PPO 决策点
2. `legal_actions` 全部为正式规则合法动作
3. `action_slot` 能唯一映射回具体出牌
4. 同一 seed 下 `state_snapshot` 和终局结果可复现
5. `terminal_result` 与正式 C# 游戏结算一致
6. 非法 `action_slot` 会返回结构化错误码
7. `ACTION_SPACE_OVERFLOW` 能被显式记录和暴露

---

## 17. Python 侧职责

在该桥接方案下，Python 只负责：

1. PPO 网络推理
2. PPO 训练循环
3. observation 编码
4. action mask 构造
5. 基于 `terminal_result` 计算最终 reward
6. checkpoint 保存与评估调度

Python 不负责：

1. 规则判定
2. 合法动作生成
3. 胜负判定
4. 升级数计算

---

## 18. 阶段 1 实施建议

阶段 1 最小实现顺序：

1. 新建 `PPO AI EngineHost`
2. 跑通 `reset / step / close`
3. 跑通 `state_snapshot / legal_actions`
4. 用固定座位和固定 seed 做回放一致性验证
5. 再接入 Python 训练循环

验证要求：

1. 同一 seed 下，C# Host 行为可复现
2. `legal_actions` 与正式规则一致
3. `terminal_result` 与正式游戏结算一致

---

## 19. 本版结论

阶段 1 的 `PPO AI` 训练环境桥接方案正式采用：

- `C# 正式引擎`
- `stdio JSON bridge`
- `Python 训练 / C# 环境推进`

这样可以在不引入过多工程复杂度的前提下，最大限度保证训练环境与正式规则一致。

---

**文档版本**：v1.0  
**创建日期**：2026-03-18  
**适用范围**：PPO AI 阶段1 C# 训练环境桥接设计
