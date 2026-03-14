# AI自主进化技巧方案 - 无需人类知识

## 核心理念

**不告诉AI"怎么做"，只告诉AI"什么是好的结果"，让AI自己探索出技巧。**

---

## 方案对比

### 当前方案（参数进化）
```
人类定义规则 → AI调整参数权重
❌ 问题：
- 依赖人类技巧（对家赢牌垫小牌）
- 规则写死，无法创新
- 上限=人类水平
```

### 自主进化方案（强化学习）
```
AI自博弈 → 发现好策略 → 强化好策略 → 淘汰差策略
✅ 优点：
- 不需要人类技巧
- 可以发现新策略
- 理论上限>人类
```

---

## 一、核心算法：PPO（Proximal Policy Optimization）

### 1.1 为什么选PPO？

**对比其他算法**：
```
DQN（Deep Q-Network）：
- 适合离散动作空间
- ❌ 拖拉机出牌组合太多（C(25,1) + C(25,2) + ...）

A3C（Asynchronous Advantage Actor-Critic）：
- 需要多进程并行
- ❌ 实现复杂，调试困难

PPO（Proximal Policy Optimization）：
- 稳定性好
- 样本效率高
- ✅ 适合拖拉机这种复杂游戏
```

### 1.2 PPO核心思想

```python
# 伪代码
class PPOAgent:
    def __init__(self):
        self.policy_network = PolicyNet()  # 策略网络：state → action
        self.value_network = ValueNet()    # 价值网络：state → 预期胜率

    def train_step(self, trajectory):
        # 1. 计算优势函数（Advantage）
        advantage = actual_reward - predicted_value

        # 2. 更新策略网络（但不要更新太多）
        ratio = new_policy / old_policy
        clipped_ratio = clip(ratio, 0.8, 1.2)  # 限制更新幅度
        loss = -min(ratio * advantage, clipped_ratio * advantage)

        # 3. 更新价值网络
        value_loss = (actual_reward - predicted_value)^2

        self.optimize(loss + value_loss)
```

**关键点**：
- `clip(ratio, 0.8, 1.2)`：防止策略突变（稳定性）
- `advantage`：告诉AI哪些行为比预期好
- 不需要人类告诉AI"对家赢牌垫小牌"，AI自己会发现

---

## 二、状态表示（State Encoding）

### 2.1 输入特征（AI看到什么）

```python
class StateEncoder:
    def encode(self, game_state):
        features = []

        # 1. 手牌特征（108维）
        # 每张牌一个位置：大王、小王、♠A、♠A、♠K、...
        hand_vector = [0] * 108
        for card in my_hand:
            hand_vector[card_index(card)] += 1
        features.extend(hand_vector)

        # 2. 已出牌特征（108维）
        played_vector = [0] * 108
        for trick in played_tricks:
            for card in trick:
                played_vector[card_index(card)] += 1
        features.extend(played_vector)

        # 3. 当前墩特征（108维）
        current_trick_vector = [0] * 108
        for play in current_trick:
            for card in play.cards:
                current_trick_vector[card_index(card)] += 1
        features.extend(current_trick_vector)

        # 4. 角色特征（4维）
        role_vector = [
            1 if role == Dealer else 0,
            1 if role == DealerPartner else 0,
            1 if role == Opponent else 0,
            1 if is_my_turn else 0
        ]
        features.extend(role_vector)

        # 5. 局面特征（10维）
        situation_vector = [
            current_score / 200,           # 当前分数（归一化）
            tricks_remaining / 25,         # 剩余墩数
            my_team_winning_current_trick, # 我方是否赢当前墩
            partner_winning_current_trick, # 队友是否赢当前墩
            is_leading,                    # 是否先手
            trump_suit_encoding,           # 主花色（one-hot）
            level_rank_encoding,           # 当前级别
            ...
        ]
        features.extend(situation_vector)

        # 总维度：108*3 + 4 + 10 = 338维
        return torch.tensor(features, dtype=torch.float32)
```

**关键点**：
- 只包含**公开信息**（不能看对手手牌）
- 归一化到[0,1]（加速训练）
- 包含足够信息让AI做决策

### 2.2 动作表示（Action Encoding）

```python
class ActionEncoder:
    def encode_legal_actions(self, hand, game_state):
        legal_actions = []

        # 1. 如果是先手，枚举所有可能的出牌组合
        if is_leading:
            # 单张
            for card in hand:
                legal_actions.append([card])

            # 对子
            for card, count in card_counts.items():
                if count >= 2:
                    legal_actions.append([card, card])

            # 拖拉机
            for tractor in find_tractors(hand):
                legal_actions.append(tractor)

            # 甩牌（复杂组合）
            for throw_combo in find_throw_combos(hand):
                legal_actions.append(throw_combo)

        # 2. 如果是跟牌，只能跟首引花色
        else:
            legal_actions = validator.get_legal_follows(hand, lead_cards)

        # 3. 编码为向量
        action_vectors = []
        for action in legal_actions:
            vector = [0] * 108
            for card in action:
                vector[card_index(card)] += 1
            action_vectors.append(vector)

        return legal_actions, action_vectors
```

**关键点**：
- 只考虑**合法动作**（减少搜索空间）
- 动作数量动态变化（1-25张牌）
- 使用mask机制（非法动作概率=0）

---

## 三、奖励函数设计（Reward Shaping）

### 3.1 稀疏奖励（Sparse Reward）

```python
# 最简单的奖励：只看最终结果
def sparse_reward(game_result):
    if won:
        return +1
    else:
        return -1
```

**问题**：
- 信号太弱（25墩才有一次反馈）
- 学习慢（需要100万局+）
- 无法区分"好的输"和"差的输"

### 3.2 密集奖励（Dense Reward）- 推荐

```python
class RewardShaper:
    def calculate_reward(self, state_before, action, state_after):
        reward = 0

        # 1. 最终胜负（主要奖励）
        if game_ended:
            if won:
                reward += 10.0
            else:
                reward -= 10.0

        # 2. 赢墩奖励（中等奖励）
        if won_trick:
            reward += 0.5
            # 额外奖励：赢到分牌
            points_won = sum(get_points(card) for card in trick_cards)
            reward += points_won * 0.05  # 5分=0.25，10分=0.5

        # 3. 控制权奖励（小奖励）
        if won_trick and is_my_team:
            reward += 0.2  # 获得下轮先手权

        # 4. 保底奖励/惩罚（重要）
        if game_ended:
            if is_dealer_side:
                bottom_points = get_bottom_points()
                if bottom_attacked:
                    reward -= bottom_points * 0.1  # 被抠底惩罚
                else:
                    reward += 0.5  # 保底成功奖励

        # 5. 配合奖励（鼓励团队合作）
        if partner_won_trick and i_gave_points:
            reward += 0.3  # 给队友送分

        # 6. 效率惩罚（避免拖延）
        reward -= 0.01  # 每步-0.01，鼓励快速结束

        return reward
```

**关键点**：
- 主要奖励：胜负（10.0）
- 中等奖励：赢墩、得分（0.5-1.0）
- 小奖励：控制权、配合（0.2-0.3）
- 不需要告诉AI"对家赢牌垫小牌"，AI会自己发现：
  - 队友赢墩 + 我送分 → +0.3奖励
  - 多次后AI学会：队友赢时送分是好的

### 3.3 课程学习（Curriculum Learning）

```python
class CurriculumReward:
    def __init__(self):
        self.phase = 0  # 训练阶段

    def get_reward(self, state, action, result):
        # Phase 0（前10万局）：只学习合法出牌
        if self.phase == 0:
            if action_is_legal:
                return +0.1
            else:
                return -1.0  # 严重惩罚非法出牌

        # Phase 1（10-50万局）：学习赢墩
        elif self.phase == 1:
            if won_trick:
                return +1.0
            else:
                return -0.1

        # Phase 2（50-100万局）：学习得分
        elif self.phase == 2:
            return points_won * 0.1

        # Phase 3（100万局+）：学习胜负
        else:
            return full_reward()  # 使用完整奖励函数
```

**优点**：
- 从简单到复杂
- 加速学习
- 避免早期崩溃

---

## 四、训练流程

### 4.1 自博弈循环

```python
class SelfPlayTrainer:
    def __init__(self):
        self.agent = PPOAgent()
        self.replay_buffer = ReplayBuffer(capacity=100000)

    def train(self, num_iterations=1000):
        for iteration in range(num_iterations):
            # 1. 自博弈收集数据（1000局）
            trajectories = self.collect_trajectories(num_games=1000)

            # 2. 存入经验池
            self.replay_buffer.add(trajectories)

            # 3. 训练网络（10个epoch）
            for epoch in range(10):
                batch = self.replay_buffer.sample(batch_size=256)
                loss = self.agent.train_step(batch)

            # 4. 评估进度
            if iteration % 10 == 0:
                win_rate = self.evaluate(num_games=200)
                print(f"Iteration {iteration}: WinRate={win_rate:.2%}")

            # 5. 保存检查点
            if iteration % 100 == 0:
                self.agent.save(f"checkpoint_{iteration}.pth")

    def collect_trajectories(self, num_games):
        trajectories = []

        for game_id in range(num_games):
            game = Game()
            trajectory = []

            while not game.is_over():
                # 当前玩家
                player = game.current_player
                state = self.encode_state(game, player)

                # AI选择动作
                action, log_prob = self.agent.select_action(state)

                # 执行动作
                game.play(action)

                # 记录轨迹
                trajectory.append({
                    'state': state,
                    'action': action,
                    'log_prob': log_prob,
                    'player': player
                })

            # 游戏结束，计算奖励
            for step in trajectory:
                player = step['player']
                reward = self.calculate_reward(game, player)
                step['reward'] = reward

            trajectories.extend(trajectory)

        return trajectories
```

### 4.2 训练时间估算

```python
# 硬件配置
GPU = "NVIDIA RTX 4090"  # 或 A100
CPU_Cores = 32
RAM = 64GB

# 训练参数
games_per_iteration = 1000
iterations = 1000
total_games = 1_000_000

# 时间估算
time_per_game = 2秒  # 神经网络推理 + 游戏模拟
time_per_iteration = 1000 * 2秒 = 33分钟
total_time = 1000 * 33分钟 = 23天

# 优化后（并行 + GPU加速）
parallel_games = 32  # 32个游戏并行
time_per_iteration = 1000 / 32 * 2秒 = 1分钟
total_time = 1000 * 1分钟 = 17小时
```

**实际训练**：
- 第1天：学会合法出牌
- 第3天：学会基本策略（赢墩、得分）
- 第7天：学会配合（对家赢牌送分）
- 第14天：学会高级技巧（保底、抠底）
- 第30天：超越人类高手

---

## 五、网络架构

### 5.1 策略网络（Policy Network）

```python
class PolicyNetwork(nn.Module):
    def __init__(self):
        super().__init__()

        # 输入：338维状态向量
        self.encoder = nn.Sequential(
            nn.Linear(338, 512),
            nn.ReLU(),
            nn.LayerNorm(512),
            nn.Linear(512, 256),
            nn.ReLU(),
            nn.LayerNorm(256),
        )

        # 注意力机制（关注重要特征）
        self.attention = nn.MultiheadAttention(256, num_heads=8)

        # 输出：动作概率分布
        self.policy_head = nn.Sequential(
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, 108),  # 108维动作空间
            nn.Softmax(dim=-1)
        )

    def forward(self, state, legal_action_mask):
        # 编码状态
        x = self.encoder(state)

        # 注意力
        x, _ = self.attention(x, x, x)

        # 输出动作概率
        logits = self.policy_head(x)

        # 屏蔽非法动作
        logits = logits.masked_fill(~legal_action_mask, -1e9)
        probs = F.softmax(logits, dim=-1)

        return probs
```

### 5.2 价值网络（Value Network）

```python
class ValueNetwork(nn.Module):
    def __init__(self):
        super().__init__()

        self.encoder = nn.Sequential(
            nn.Linear(338, 512),
            nn.ReLU(),
            nn.Linear(512, 256),
            nn.ReLU(),
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, 1),  # 输出：预期胜率
            nn.Tanh()  # 输出范围[-1, 1]
        )

    def forward(self, state):
        return self.encoder(state)
```

---

## 六、实施路线图

### Phase 1：基础框架（1周）
```
1. 实现状态编码器（StateEncoder）
2. 实现动作编码器（ActionEncoder）
3. 实现奖励函数（RewardShaper）
4. 实现自博弈循环（SelfPlayTrainer）
```

### Phase 2：网络训练（2周）
```
1. 训练策略网络（PolicyNetwork）
2. 训练价值网络（ValueNetwork）
3. 调试PPO算法
4. 优化训练速度（并行化）
```

### Phase 3：评估优化（1周）
```
1. 评估vs当前AI（Champion v19）
2. 评估vs人类玩家
3. 分析学到的策略
4. 微调奖励函数
```

---

## 七、预期效果

### 训练进度预测

```yaml
Day 1-3（10万局）:
  - 学会合法出牌（非法率<1%）
  - 学会基本跟牌
  - 胜率：30-40%（随机水平）

Day 4-7（50万局）:
  - 学会赢墩策略
  - 学会送分给队友
  - 胜率：45-50%（Medium水平）

Day 8-14（100万局）:
  - 学会保底/抠底
  - 学会用最小牌赢
  - 胜率：55-60%（Hard水平）

Day 15-30（200万局+）:
  - 学会高级配合
  - 发现新策略
  - 胜率：65-70%（超越人类）
```

### 可能发现的新策略

AI可能自己发现人类没想到的策略：
- 特殊局面下的最优甩牌组合
- 更精确的保底时机判断
- 更高效的队友配合模式
- 基于概率的风险决策

---

## 八、技术栈

```yaml
语言: Python 3.10+
深度学习框架: PyTorch 2.0
强化学习库: Stable-Baselines3 或 RLlib
游戏引擎: C# (现有) + Python wrapper
并行化: Ray (分布式训练)
监控: TensorBoard + Weights & Biases
```

---

## 九、与当前方案对比

| 维度 | 参数进化 | 强化学习 |
|------|---------|---------|
| 依赖人类知识 | ✅ 高度依赖 | ❌ 不依赖 |
| 训练时间 | 1-2天 | 2-4周 |
| 上限 | 人类水平 | 超越人类 |
| 可解释性 | ✅ 高 | ❌ 低（黑盒） |
| 实施难度 | ✅ 低 | ❌ 高 |
| 创新能力 | ❌ 无 | ✅ 可发现新策略 |

---

## 十、混合方案（推荐）

```
Week 1: 参数进化（快速可玩）
  - 使用v2参数
  - Defender 40% → 48%
  - 立即可玩

Week 2-3: 模仿学习（加速RL）
  - 用参数AI生成100万局数据
  - 训练神经网络模仿
  - 学会基本策略（不从零开始）

Week 4-6: 强化学习（超越人类）
  - 在模仿学习基础上用PPO优化
  - 自博弈200万局
  - 发现新策略，超越人类

优点：
- 快速上线（Week 1）
- 持续提升（Week 2-6）
- 最终达到最强（Week 6+）
```

---

## 结论

**回答你的问题**：

1. **不依赖外部技巧**：✅ 强化学习只需要奖励函数
2. **不依赖跟人玩**：✅ 自博弈即可
3. **AI自主进化**：✅ 通过PPO算法自动发现策略

**建议**：
- 短期：用参数进化（今天可玩）
- 长期：用强化学习（2-4周超越人类）
- 最佳：混合方案（渐进式提升）

**需要我开始实施哪个方案？**
