# 拖拉机/升级 规则AI参考文档 V2（外部思路汇总）

> 说明：本文档是参考文档，不是本项目正式设计文档，不作为实现、评审或验收基线。
> 正式方案请以 `doc/规则AI架构设计_v2.1.md` 为准。

## 1. 文档目标
本设计用于落地一个“会打整局”的规则AI，要求：
- 决策逻辑清晰、可解释、可复盘
- 结构稳定（不随机抖动）
- 能通过参数调优持续变强
- 与本地冻结规则完全一致
- 可平滑接入RL（行为克隆/约束RL）

---

## 2. 项目规则画像（本地冻结口径）
本框架按当前项目锁定规则运行，以下项优先级高于策略偏好：

- 对局模式：4人，2v2组队（单机 1人+3AI）
- 主牌版本：非2常主（主牌=大小王+级牌+主花色）
- 反底：可选规则，一期关闭（`enable_counter_bottom=false`）
- 三王反底：不启用（即使二期开反底，也仅允许“同王对子”参与）
- 甩牌失败处罚：可配置，默认0分（`throw_fail_penalty=0`）
- 抠底倍数：不封顶（按公式递增）
- 分数关/必打关：启用（5/10/K/A）
- 拖拉机：允许断档拖（当级牌在中间时）
- 关键强约束：不同花色级牌不构成对子

> 结论：这份文档可作为下一代规则AI框架，但必须以上述口径作为硬约束注入执行层，而不是仅作为策略偏好。

---

## 3. 设计总览
系统只保留三层决策和一个统一执行流程：

- 战略层（局级）：当前整局目标是什么
- 战术层（墩级）：当前这一墩怎么配合
- 执行层（动作级）：具体出哪张牌

执行层统一使用4步法：
1. 过滤（硬约束）
2. 打分（基础EV）
3. 场景修正（比分/残局）
4. 稳定选牌（tie-break）

规则优先级固定为：
`HardRule > TacticalGoal > EVScore > TieBreak`

---

## 4. 最小数据结构（仅保留决策必需）

```python
class State:
    hand
    legal_actions
    trump

    game_profile = {
      "mode": "2v2",
      "enable_counter_bottom": False,
      "throw_fail_penalty": 0,
      "bottom_multiplier_cap": None,
      "enable_mandatory_levels": True
    }

    score = {
      "us": 0,
      "opp": 0,
      "lead": 0,
      "target": 80,
      "bottom_est": 0
    }

    round = {
      "trick_idx": 0,
      "my_pos": 1,
      "cards_left_min": 25,
      "phase": "OPEN"
    }

    belief = {
      "void_prob": {},
      "trump_mu": {},
      "pair_mu": {},
      "tractor_prob": {},
      "high_control_prob": {}
    }

    intent = {
      "mate_intent": "UNKNOWN",
      "opp_intent": "UNKNOWN",
      "conf_mate": 0.0,
      "conf_opp": 0.0
    }

    risk = {
      "budget": 0.50,
      "mode": "BAL"
    }
```

说明：
- 不存“不会影响决策”的字段
- 复杂日志由独立回放模块管理，不污染在线决策
- `game_profile`必须在对局启动时冻结，不允许局中变更

---

## 5. 三层决策职责

### 5.1 战略层（局级）
输入：`score + phase + risk`
输出：`strategy_mode`

候选模式：
- `CONTROL_SCORE`
- `CLEAR_TRUMP`
- `SAVE_BOTTOM`
- `AGGRESSIVE_CATCH`

职责边界：
- 只定方向，不选具体牌

### 5.2 战术层（墩级）
输入：`belief + intent + strategy_mode + my_pos`
输出：`trick_goal`

候选目标：
- `TAKE_TRICK`
- `PASS_TO_MATE`
- `HIDE_INFO`
- `ANTI_SWING`

职责边界：
- 只定本墩打法，不直接做EV排序

### 5.3 执行层（动作级）
输入：`legal_actions + trick_goal + strategy_mode + risk`
输出：`action`

职责边界：
- 只做动作选择
- 必须遵守4步法

---

## 6. 4步决策流程（执行层标准）

### Step 1 过滤（硬约束）
对`legal_actions`做两次过滤：
- 牌型合法性过滤（Pattern Validator）
- 硬规则过滤（Hard Rule Gate）

输出：`A_candidates`

```python
def filter_actions(state):
    A = [a for a in state.legal_actions if is_pattern_legal(a, state)]
    A = [a for a in A if pass_hard_rules(a, state)]
    return A
```

必须失败谓词（命中即剔除）：
- `HR001_INVALID_PATTERN`：非法牌型
- `HR002_LEVEL_PAIR_CROSS_SUIT`：不同花色级牌组成对子（禁止）
- `HR003_FOLLOW_COUNT_MISMATCH`：跟牌张数不匹配
- `HR004_FOLLOW_STRUCTURE_VIOLATION`：可跟对子/拖拉机却未跟结构
- `HR005_CUT_STRUCTURE_INVALID`：毙牌结构不成立（如首引拖拉机但未出同等主牌结构）
- `HR006_THROW_CHILD_BLOCKED`：甩牌任一子结构被合法拦截

甩牌失败落地规则：
- 从拟甩牌中按“单牌 > 对子 > 拖拉机”选最小合法牌型
- 处罚分使用`throw_fail_penalty`，默认0

### Step 2 打分（基础EV）
提取特征并加权：
- `win_prob`
- `score_gain`
- `structure_loss`
- `control_retention`
- `info_leak`
- `mate_sync`
- `opp_swing_risk`
- `min_cost_value`

\[
EV_0(a)=\sum_i w_i(pos,phase,risk\_mode)\cdot f_i(a)
\]

### Step 3 场景修正（战略注入）
修正项来自`strategy_mode + score + phase`：
- 残局（`cards_left_min <= 8`）提高保底/控分权重
- 领先时惩罚高风险动作
- 落后时提高抢上手/高收益动作权重

\[
EV(a)=EV_0(a)+Adjust(strategy\_mode, score, phase)
\]

### Step 4 稳定选牌（防抖）
若`Top1-Top2 < epsilon`（默认0.05），按固定顺序：
`保牌权 > 保结构 > 控分 > 隐信息`

```python
def stable_tiebreak(top_actions):
    # 固定优先级，禁止纯随机
    pass
```

---

## 7. 信息状态更新（Belief + Intent + Risk）

### 7.1 Belief更新
每墩结束后：
- 未跟花色：`void_prob[player][suit] += alpha_void`
- 将吃：`trump_mu[player] -= alpha_trump_consume`
- 高主将吃：`high_control_prob[player] += alpha_high_control`
- 拆对迹象：`pair_mu[player][suit] -= alpha_pair_break`
- 拖拉机可能性下降：`tractor_prob[player][suit] -= alpha_tractor_drop`

所有概率裁剪到`[0,1]`。

### 7.2 意图状态机（IntentFSM）
队友：
- `CLEAR_TRUMP / BUILD_SUIT / PROBE / SAVE_SCORE`

对手：
- `FORCE_TRUMP / SWING_SCORE / PREP_SWING`

阈值：
- 触发`0.60`
- 释放`0.40`

### 7.3 风险预算（RiskFSM）
按分差调整：
- 基础：0.50
- 领先20：0.35
- 领先40：0.25
- 落后20：0.70
- 落后40：0.85

---

## 8. 硬规则清单（项目化约束）
以下规则是“规则引擎层”硬约束，不参与学习：

1. 非法牌型动作禁用
2. 不同花色级牌不构成对子
3. 跟牌必须同张数，且优先满足结构（对子/拖拉机）
4. 缺门后可垫牌或毙牌，毙牌必须满足对应结构条件
5. 首引拖拉机时，毙牌方必须出主牌拖拉机结构才有效
6. 甩牌按子结构判定，任一子结构被合法拦截则甩牌失败
7. 甩牌失败后的出牌与处罚按配置执行（默认0罚分）
8. 非必要不拆关键结构（高对/拖拉机/关键主牌）
9. 队友清主时不逆节奏副攻
10. 剩牌<=8强制残局策略
11. 扣底风险高时禁止贪小分
12. tie-break固定顺序，禁止纯随机

---

## 9. 规则映射表（规则 -> 模块 -> 配置 -> 日志码）

| 规则项 | 责任模块 | 配置键 | 日志码 |
|---|---|---|---|
| 2v2模式 | MatchInit | `mode=2v2` | `CFG_MODE_2V2` |
| 反底一期关闭 | PhaseController | `enable_counter_bottom=false` | `CFG_COUNTER_BOTTOM_OFF` |
| 三王反底关闭 | CounterBottomValidator | `allow_three_joker_counter=false` | `CB_TRIPLE_JOKER_DENY` |
| 甩牌失败默认0罚分 | ThrowResolver | `throw_fail_penalty=0` | `THROW_FAIL_PENALTY_APPLY` |
| 抠底不封顶 | ScoreEngine | `bottom_multiplier_cap=null` | `SCORE_BOTTOM_MULTIPLIER` |
| 必打关启用 | LevelEngine | `enable_mandatory_levels=true` | `LEVEL_MANDATORY_GATE` |
| 异花级牌非对子 | PatternValidator | `level_pair_same_suit_only=true` | `HR002_LEVEL_PAIR_CROSS_SUIT` |
| 跟牌结构强制 | FollowValidator | `strict_follow_structure=true` | `HR004_FOLLOW_STRUCTURE_VIOLATION` |
| 毙牌结构强制 | CutValidator | `strict_cut_structure=true` | `HR005_CUT_STRUCTURE_INVALID` |

---

## 10. 参数默认值（可直接落地）

```yaml
runtime:
  endgame_cards_left: 8
  ev_tie_epsilon: 0.05

profile:
  mode: 2v2
  enable_counter_bottom: false
  allow_three_joker_counter: false
  throw_fail_penalty: 0
  bottom_multiplier_cap: null
  enable_mandatory_levels: true
  level_pair_same_suit_only: true
  strict_follow_structure: true
  strict_cut_structure: true

risk_budget:
  base: 0.50
  lead_20: 0.35
  lead_40: 0.25
  trail_20: 0.70
  trail_40: 0.85

belief_init:
  void_prob: 0.15
  trump_mu: 6.0
  pair_mu: 1.20
  tractor_prob: 0.18
  high_control_prob: 0.30

belief_update:
  alpha_void: 0.22
  alpha_trump_consume: 1.00
  alpha_pair_break: 0.18
  alpha_tractor_drop: 0.15
  alpha_high_control: 0.12

intent_threshold:
  trigger: 0.60
  release: 0.40
```

---

## 11. 端到端流程（唯一入口）

```python
def decide(state):
    # 0) 更新状态
    update_belief(state)
    update_intent(state)
    update_risk(state)
    state.round.phase = "END" if state.round.cards_left_min <= 8 else state.round.phase

    # 1) 分层决策
    strategy_mode = strategic_policy(state)
    trick_goal = tactical_policy(state, strategy_mode)

    # 2) 执行层四步法（硬规则优先）
    A = filter_actions(state)
    scored = []
    for a in A:
        f = extract_features(a, state, trick_goal)
        ev0 = base_ev(f, state)
        ev = scenario_adjust(ev0, f, state, strategy_mode)
        scored.append((a, ev, f))

    action = stable_tiebreak(scored, epsilon=0.05)
    return action
```

---

## 12. 可解释输出规范
每手必须记录：
1. `strategy_mode`
2. `trick_goal`
3. `top3_actions + EV`
4. `chosen_action_reason`
5. `hard_rules_triggered`

每墩必须补充：
- 四家手牌快照（可脱敏）
- 亮主/反主信息
- 四家出牌轨迹
- 胜者与判定依据（主牌优先/同型比较/毙牌成立与否）
- 本墩得分与累计分

---

## 13. 验收清单（自动化）
以下检查项必须进入回归测试：

1. 异花级牌组成对子应判非法
2. 首引对子/拖拉机时，跟牌结构义务正确
3. 毙牌结构判定正确（尤其对子/拖拉机场景）
4. 甩牌判定正确（子结构拆解与拦截）
5. 甩牌失败后最小出牌正确
6. 甩牌失败罚分按配置生效（默认0）
7. 分数结算正确（庄/闲归属+抠底倍数）
8. 单墩胜者判定正确且有解释日志

---

## 14. 开发里程碑（两周）
- D1-D3：状态与合法动作、能跑完整局
- D4-D6：Belief/Intent/Risk接入
- D7-D9：执行层4步法与硬约束日志码
- D10-D12：回放可视化、参数搜索、规则回归
- D13-D14：稳定性修复与A/B评测

---

## 15. 与RL衔接（预留接口语义）
规则AI提供三种可学习信号：
- `rule_policy(state)`
- `rule_value(state)`
- `rule_features(state, action)`

用途：
- 行为克隆预训练
- 奖励塑形
- 约束式PPO（KL到规则策略）

前提：RL不得绕过HardRule Gate；任何策略输出先过Step1。

---

## 16. 实施结论
该文档可以作为规则AI下一代设计框架，前提是：
- 以本地冻结规则作为硬约束注入执行层
- 明确“规则优先级”与“配置冻结边界”
- 通过规则映射表和验收清单保证实现一致性

这样可同时满足：可解释、可调参、可演进到RL。
