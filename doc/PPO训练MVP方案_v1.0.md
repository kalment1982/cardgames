# 拖拉机 PPO 训练 MVP 方案 v1.0

## 1. 文档定位

本文档是 PPO 训练的**第一版最小可行方案**,用于回答一个核心问题:

**PPO 能否在现有训练环境里学会打拖拉机,并稳定逼近 RuleAI 水平?**

### 本文档验证的目标:

- ✅ PPO 是否能从随机策略提升到"会打牌"
- ✅ PPO 是否能逼近 RuleAI 基线水平(胜率 >40%)
- ✅ 训练是否稳定可持续

### 本文档不验证的目标:

- ❌ 不验证生产环境可上线
- ❌ 不验证正式 C# 引擎对齐
- ❌ 不验证一定超过 RuleAI
- ❌ 不验证复杂联赛系统

**这些目标放到第二阶段,只有 MVP 成功后才投入。**

---

## 2. 总体时间规划

**目标周期**: 2-4 周

- Phase 1: 训练闭环跑通 (1 周)
- Phase 2: 逼近 RuleAI 基线 (1-2 周)
- Phase 3: 工程化决策 (评估后决定)

---

## 3. Phase 1: 训练闭环跑通

### 3.1 目标

证明 PPO 能从随机策略提升到"会打牌"。

### 3.2 范围约束

**只训练出牌阶段:**
- 发牌、亮主、反主、埋底: 用规则策略或固定逻辑
- PPO 只接管 PlayTricks 阶段

**使用现有环境:**
- Python 游戏引擎 (`rl_training/game_engine.py`)
- 固定动作索引 + legal mask
- 不切换到 C# 引擎

**最小状态编码:**
只保留 3 组基础信息:

1. **基础局面**
   - 当前玩家座位
   - 庄闲身份
   - 主花色/级牌
   - 当前比分
   - 各玩家剩余牌数

2. **手牌结构**
   - 手牌编码(one-hot 或 count)
   - 主牌数量
   - 对子数量
   - 高牌数量

3. **当前墩上下文**
   - 首发牌型
   - 当前已出牌
   - 当前赢家
   - 当前墩分数
   - 是否必须跟同门

**奖励函数:**
```python
# 终局奖励
reward = +10.0 if win else -10.0

# 升级数奖励(n=升级数,可为负)
reward += 2.0 * level_change

# 得分奖励
reward += score_delta * 0.02
```

**不加过程奖励。** 先验证终局信号能否驱动学习,如果学不动再考虑加。

**对手设置:**
- 只和 RuleAI 对战
- 不做复杂对手池

### 3.3 验收标准

1. ✅ 能稳定跑完整训练循环(至少 1000 局)
2. ✅ 非法动作率 < 5%
3. ✅ 对随机策略有明显提升
4. ✅ 对 RuleAI 胜率从 ~0% 提升到 >20%

### 3.4 失败信号

如果出现以下情况,需要调整方案:
- 训练 500 局后胜率仍然 <5%
- 非法动作率持续 >20%
- 训练曲线完全不收敛

---

## 4. Phase 2: 逼近 RuleAI 基线

### 4.1 目标

从"会打"提升到"接近 RuleAI"(胜率 >40%)。

### 4.2 允许的改进

**只在 Phase 1 基础上增加 3 类改进:**

**1. 补充状态信息**
- 当前墩详细信息(lead pattern 类型)
- 基础记牌(已出的关键大牌)
- 主牌/副牌结构统计
- 保底/抠底压力标志位

**2. 可选: 加入过程奖励**

仅当 Phase 1 终局信号不足以驱动学习时,才考虑加入:
- 保底成功/失败
- 抠底成功/失败
- 高分墩得失

**默认不加。** 过程奖励容易过度引导,先观察纯终局奖励的效果。

**3. 训练稳定性优化**
- 加入自我对弈(30% self-play + 70% vs RuleAI)
- 超参数调优(学习率、熵系数等)
- Checkpoint 回滚机制

### 4.3 验收标准

1. ✅ 对 RuleAI 胜率 >40%
2. ✅ 不再频繁出现明显低级错误
3. ✅ 训练曲线稳定,不频繁崩溃

### 4.4 成功信号

如果达到以下任一标准,视为成功:
- 对 RuleAI 胜率稳定在 40-50%
- 在某些特定场景(如保底、抠底)明显优于随机

---

## 5. Phase 3: 工程化决策

**只有 Phase 2 成功,才进入这个阶段。**

此时需要决策:

1. **是否切换到正式 C# 引擎?**
   - 评估: Python 环境与 C# 规则的差异有多大
   - 如果差异小,可以继续用 Python
   - 如果差异大,需要桥接 C# 引擎

2. **是否改成候选动作评分?**
   - 评估: 固定动作索引是否成为瓶颈
   - 如果不是瓶颈,继续用固定索引
   - 如果是瓶颈,再改架构

3. **是否加入 DAgger?**
   - 评估: 离线数据分布偏移是否严重
   - 如果不严重,不需要 DAgger
   - 如果严重,加入在线纠偏

4. **是否做正式联赛系统?**
   - 评估: 是否需要对抗多样化对手
   - 如果 RuleAI 已经足够强,暂时不需要
   - 如果需要更强对手,再建联赛

---

## 6. MVP 核心原则

虽然方案要轻,但这些原则不能删:

### 6.1 评估原则

**最终评估看实战胜率,不看 teacher 一致率。**

评估指标优先级:
1. 对 RuleAI 胜率(主指标)
2. 合法率
3. 关键场景表现(保底、抠底、高分墩)
4. Teacher 一致率(仅供参考)

### 6.2 奖励原则

**只用终局奖励,不加过程奖励。**

```
终局胜负: ±10
升级数:   2 × n
得分:     score_delta × 0.02
```

过程奖励容易过度引导,先验证终局信号能否驱动学习。

### 6.3 日志原则

**必须保留详细决策日志。**

每次决策至少记录:
- game_id, round_id, trick_id
- player_index, phase
- state_summary
- legal_action_count
- selected_action
- policy_probabilities (top-5)
- value_estimate
- reward

### 6.4 评估脚本原则

**必须固定一套评估脚本和评估种子。**

避免"看起来变强"的假象:
- 固定 100 个评估种子
- 每次 checkpoint 都用相同种子评估
- 记录每个种子的胜负,不只看平均胜率

---

## 7. 状态设计(MVP 版)

### 7.1 基础局面组 (必需)

```python
state = {
    # 玩家身份
    'current_player': int,        # 0-3
    'dealer': int,                # 0-3
    'is_dealer_team': bool,       # 当前玩家是否庄家方

    # 游戏状态
    'trump_suit': int,            # 主花色编码
    'level_rank': int,            # 级牌
    'dealer_score': int,          # 庄家方得分
    'defender_score': int,        # 闲家方得分

    # 进度信息
    'tricks_played': int,         # 已打墩数
    'tricks_remaining': int,      # 剩余墩数
    'cards_in_hand': [int] * 4,   # 各玩家剩余牌数
}
```

### 7.2 手牌结构组 (必需)

```python
hand_features = {
    # 手牌编码
    'hand_cards': np.array,       # one-hot 或 count 编码

    # 结构统计
    'trump_count': int,           # 主牌数量
    'pair_count': int,            # 对子数量
    'tractor_potential': int,     # 拖拉机潜力
    'high_card_count': int,       # 高牌数量(A/K/Q)
    'total_points': int,          # 手牌总分
}
```

### 7.3 当前墩上下文组 (必需)

```python
trick_context = {
    # 当前墩信息
    'lead_pattern_type': int,     # 首发牌型(单/对/拖拉机/甩)
    'current_winner': int,        # 当前赢家
    'current_winner_team': int,   # 赢家队伍
    'trick_points': int,          # 当前墩分数

    # 跟牌约束
    'must_follow_suit': bool,     # 是否必须跟同门
    'can_discard': bool,          # 是否可以垫牌
    'can_trump': bool,            # 是否可以毙牌

    # 后手信息
    'players_remaining': int,     # 后手还剩几家
    'teammate_remaining': bool,   # 队友是否还没出
}
```

### 7.4 Phase 2 可选补充

```python
# 记牌信息(Phase 2 加入)
memory_features = {
    'player_void_suits': np.array,  # 各玩家缺门情况
    'high_cards_played': np.array,  # 关键大牌是否已出
    'trump_count_estimate': [int] * 4,  # 各玩家主牌数估计
}

# 保底抠底信息(Phase 2 加入)
endgame_features = {
    'kitty_points': int,            # 底牌已知分数
    'need_protect_kitty': bool,     # 是否需要保底
    'can_dig_kitty': bool,          # 是否有机会抠底
    'last_trick_control': bool,     # 最后一墩控制权
}
```

---

## 8. 奖励设计(MVP 版)

### 8.1 统一奖励函数

Phase 1 和 Phase 2 使用同一个奖励函数,不做过程奖励:

```python
def compute_reward(game_result, level_change, score_delta):
    """
    终局奖励,不加过程奖励。

    Args:
        game_result: 'win' or 'loss'
        level_change: 升级数(正=升级,负=被升级,0=保级)
        score_delta: 己方得分变化
    """
    # 胜负奖励
    reward = 10.0 if game_result == 'win' else -10.0

    # 升级数奖励
    reward += 2.0 * level_change

    # 得分奖励
    reward += score_delta * 0.02

    return reward
```

### 8.2 典型场景奖励示例

```
庄家保级(闲家80分):
  庄家: +10 + 0 - 1.6 = +8.4
  闲家: -10 + 0 + 1.6 = -8.4

闲家升1级(100分):
  闲家: +10 + 2 + 2.0 = +14.0
  庄家: -10 - 2 - 2.0 = -14.0

闲家升3级(180分):
  闲家: +10 + 6 + 3.6 = +19.6
  庄家: -10 - 6 - 3.6 = -19.6
```

### 8.3 设计原则

- **不加过程奖励。** 过程奖励容易过度引导,先验证终局信号能否驱动学习。
- 如果训练完全学不动(Phase 1 失败),再考虑加入少量关键事件奖励(保底/抠底)。
- 终局胜负(±10)始终是主导信号。

---

## 9. 对手设计(MVP 版)

### 9.1 Phase 1 对手

**只用 RuleAI:**
```python
opponents = {
    'ruleai': RuleAIPlayer(version='v2.1')
}

# 训练时对手分布
opponent_distribution = {
    'ruleai': 1.0  # 100% RuleAI
}
```

### 9.2 Phase 2 对手

**加入自我对弈:**
```python
opponents = {
    'ruleai': RuleAIPlayer(version='v2.1'),
    'self': CurrentPPOCheckpoint(),
    'historical': HistoricalBestCheckpoint()
}

# 训练时对手分布
opponent_distribution = {
    'ruleai': 0.7,      # 70% RuleAI
    'self': 0.2,        # 20% 当前版本
    'historical': 0.1   # 10% 历史最佳
}
```

### 9.3 不做复杂对手池

**Phase 1-2 不需要:**
- 参数扰动 RuleAI
- 风格化对手(保守/激进)
- 完整联赛系统

**原因:**
- 先验证基础可行性
- 避免过早优化
- 降低实现复杂度

---

## 10. 训练超参数建议

### 10.1 PPO 核心参数

```yaml
ppo:
  learning_rate: 3e-4
  clip_epsilon: 0.2
  value_coef: 0.5
  entropy_coef: 0.01  # Phase 2 可以衰减到 0.005

  epochs_per_iteration: 10
  batch_size: 64
  gamma: 0.99
  gae_lambda: 0.95

  max_grad_norm: 0.5
```

### 10.2 训练循环参数

```yaml
training:
  games_per_iteration: 100
  eval_interval: 10  # 每 10 次迭代评估一次
  save_interval: 50  # 每 50 次迭代保存一次

  max_iterations: 5000
  early_stop_win_rate: 0.5  # 胜率达到 50% 可以考虑停止
```

### 10.3 评估参数

```yaml
evaluation:
  num_eval_games: 100
  eval_seeds: [固定 100 个种子]
  deterministic: true  # 评估时用确定性策略
```

---

## 11. 实施代码范围

### 11.1 需要修改的文件

**核心训练代码:**
```
rl_training/
├── game_engine.py          # 可能需要小调整
├── state_encoder.py        # 简化到 MVP 状态
├── ppo_agent.py            # 确认 PPO 实现正确
├── train_rl.py             # 主训练循环
└── evaluate.py             # 新增: 固定种子评估脚本
```

**不要动的文件:**
```
src/Core/                   # 不动 C# 引擎
WebUI/                      # 不动 UI
```

### 11.2 新增文件建议

```
rl_training/
├── mvp_config.yaml         # MVP 专用配置
├── mvp_state_encoder.py    # MVP 简化状态编码
├── mvp_reward.py           # MVP 奖励函数
└── evaluate_fixed_seeds.py # 固定种子评估
```

---

## 12. 风险与边界说明

### 12.1 MVP 能验证的

✅ PPO 在当前 Python 环境里是否可学
✅ PPO 是否能逼近 RuleAI 基线水平
✅ 训练是否稳定可持续

### 12.2 MVP 不能验证的

❌ PPO 在正式 C# 引擎里的表现
❌ PPO 是否能超过 RuleAI 上限
❌ PPO 是否能在生产环境上线

### 12.3 关键假设

**假设 1: Python 环境与 C# 规则差异不大**
- 如果差异大,MVP 结果可能不适用于生产
- 需要在 Phase 3 验证这个假设

**假设 2: 固定动作索引足够表达拖拉机策略**
- 如果不够,需要改成候选动作评分
- 先用 MVP 验证,再决定是否改

**假设 3: RuleAI 是足够强的基线**
- 如果 RuleAI 太弱,超过它没有意义
- 如果 RuleAI 太强,可能需要更多训练时间

---

## 13. 成功标准与失败标准

### 13.1 Phase 1 成功标准

- ✅ 训练 1000 局后,对 RuleAI 胜率 >20%
- ✅ 非法动作率 <5%
- ✅ 训练曲线稳定上升

### 13.2 Phase 2 成功标准

- ✅ 训练 5000 局后,对 RuleAI 胜率 >40%
- ✅ 关键场景(保底、抠底)表现合理
- ✅ 不频繁出现明显低级错误

### 13.3 失败标准(需要调整方案)

- ❌ 训练 2000 局后,胜率仍然 <10%
- ❌ 非法动作率持续 >20%
- ❌ 训练曲线完全不收敛或频繁崩溃
- ❌ 出现明显的奖励 hacking 行为

---

## 14. 下一步行动

### 14.1 立即开始(本周)

1. **确认现有代码状态**
   - 检查 `ppo_agent.py` 是否实现正确
   - 检查 `game_engine.py` 是否稳定
   - 检查 `state_encoder.py` 是否过于复杂

2. **创建 MVP 配置**
   - 新建 `mvp_config.yaml`
   - 简化状态编码到 MVP 版本
   - 简化奖励函数到 MVP 版本

3. **跑第一轮训练**
   - 目标: 100 局训练
   - 验证: 训练循环能跑通
   - 观察: 合法率、胜率、loss 曲线

### 14.2 第一周目标

- ✅ 训练循环稳定运行
- ✅ 能完成 1000 局训练
- ✅ 初步观察到学习信号(胜率 >5%)

### 14.3 第二周目标

- ✅ 胜率提升到 >20%
- ✅ 开始 Phase 2 改进
- ✅ 调优超参数

---

## 15. 附录: 与原设计文档的对比

### 15.1 删除的复杂设计

| 原设计 | MVP 方案 | 原因 |
|--------|----------|------|
| C# 引擎桥接 | 继续用 Python | 工程量大,先验证可行性 |
| 候选动作评分 | 固定动作索引 | 实现简单,够用 |
| DAgger | 不做 | 增加复杂度,Phase 1-2 不需要 |
| 5 阶段训练 | 2 阶段 | 过度设计,先跑通基础 |
| 复杂联赛系统 | 简单对手池 | 避免过早优化 |
| 7 层架构 | 3 层(环境/模型/训练) | 降低复杂度 |

### 15.2 保留的核心原则

| 原则 | MVP 保留 | 说明 |
|------|----------|------|
| 评估看实战胜率 | ✅ | 不看 teacher 一致率 |
| 奖励以胜负为主 | ✅ | Shaping 只做辅助 |
| 详细决策日志 | ✅ | 必须可回放诊断 |
| 固定评估种子 | ✅ | 防止假象 |

---

**文档版本**: v1.0
**创建日期**: 2026-03-18
**适用对象**: PPO 训练第一版 MVP
**预期周期**: 2-4 周
**前置文档**: PPO训练架构设计_v1.0.md (作为长期愿景参考)
