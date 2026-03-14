# 拖拉机AI训练设计方案 v2.0

## 概述

本文档描述了基于**强化学习(PPO) + 增强状态编码 + LLM Teacher**的混合训练方案，用于训练拖拉机80分游戏AI。

## 设计目标

1. **快速收敛**: 7-13天达到70%胜率（对比纯PPO的20天）
2. **高质量决策**: 学习人类专家级别的策略
3. **自主优化**: 通过自我对弈超越LLM水平
4. **硬件优化**: 充分利用Mac M4 16GB的算力

## 系统架构

```
┌─────────────────────────────────────────────────────────┐
│                   训练流程                               │
├─────────────────────────────────────────────────────────┤
│                                                          │
│  阶段1: LLM数据生成 (1-2小时)                           │
│  ┌──────────────────────────────────────────┐          │
│  │ CODEX API (Claude Sonnet 4.6)            │          │
│  │ ↓                                         │          │
│  │ 分析游戏状态 → 生成专家决策               │          │
│  │ ↓                                         │          │
│  │ 10,000+ 高质量样本                        │          │
│  └──────────────────────────────────────────┘          │
│                    ↓                                     │
│  阶段2: 监督学习预训练 (2-4小时)                        │
│  ┌──────────────────────────────────────────┐          │
│  │ 策略网络 (412维输入 → 1000维输出)        │          │
│  │ ↓                                         │          │
│  │ 学习LLM的决策模式                         │          │
│  │ ↓                                         │          │
│  │ 达到40-50%胜率                            │          │
│  └──────────────────────────────────────────┘          │
│                    ↓                                     │
│  阶段3: PPO自我对弈 (3-7天)                             │
│  ┌──────────────────────────────────────────┐          │
│  │ 8进程并行游戏模拟                         │          │
│  │ ↓                                         │          │
│  │ GAE优势估计 + PPO策略更新                │          │
│  │ ↓                                         │          │
│  │ M4 MPS加速训练                            │          │
│  │ ↓                                         │          │
│  │ 达到65-70%胜率                            │          │
│  └──────────────────────────────────────────┘          │
│                                                          │
└─────────────────────────────────────────────────────────┘
```

## 核心技术

### 1. 增强状态编码 (412维)

#### 基础特征 (338维)
- **手牌编码** (108维): 当前玩家的手牌
- **已出牌编码** (108维): 历史已打出的牌
- **当前牌桌编码** (108维): 本轮已出的牌
- **角色特征** (4维): 玩家身份one-hot编码
- **局势特征** (10维): 轮次、分数、位置等

#### 对手手牌推断 (54维)
基于已出牌和当前牌桌，推断每种牌对手还可能有多少张：
```python
belief[card_type] = (total_count - seen_count - my_count) / 2.0
```

#### 历史出牌模式 (20维)
分析3个对手的出牌习惯：
- 出大牌倾向
- 出分牌倾向
- 出主牌倾向
- 出对子倾向
- 出牌频率

### 2. LLM Teacher

#### Prompt设计
```
你是拖拉机80分游戏的专家。请分析当前局面并选择最佳出牌。

**游戏状态:**
- 当前玩家: {player} ({'庄家方' if player in [0, 2] else '闲家方'})
- 主牌花色: {trump_suit}
- 主牌等级: {trump_rank}
- 当前轮次: {round}/13

**手牌:**
{hand_cards}

**当前牌桌:**
{current_trick}

**合法出牌选项:**
{legal_actions}

**请分析并选择最佳出牌。输出格式:**
```json
{
  "reasoning": "你的分析过程（考虑：当前局势、各选项优劣、配合策略）",
  "best_action": 0,
  "confidence": 0.85
}
```
```

#### 数据质量保证
- 只记录合法动作
- 解析失败时使用随机合法动作
- 定期保存，支持断点续传
- 每批休息1秒，避免API限流

### 3. PPO算法

#### 核心参数
```yaml
learning_rate: 0.0003
gamma: 0.99              # 折扣因子
gae_lambda: 0.95         # GAE参数
clip_epsilon: 0.2        # PPO裁剪
value_loss_coef: 0.5     # 价值损失系数
entropy_coef: 0.01       # 熵系数
max_grad_norm: 0.5       # 梯度裁剪
ppo_epochs: 4            # 每次迭代的PPO轮数
batch_size: 256          # 批次大小
```

#### 奖励设计
```python
# 单步奖励
if won_trick:
    reward += 0.5                    # 赢墩奖励
    reward += points_won * 0.05      # 得分奖励
    reward += 0.2                    # 控制权奖励

# 最终奖励
if game_won:
    reward += 10.0
else:
    reward -= 10.0
```

#### GAE优势估计
```python
delta = reward + gamma * next_value - value
gae = delta + gamma * gae_lambda * gae
advantage = gae
return_value = advantage + value
```

### 4. 并行训练

#### 多进程架构
```
主进程 (MPS设备)
├── 策略网络
├── 优化器
└── 数据收集协调

工作进程 x8 (CPU)
├── 游戏模拟
├── 状态编码
└── 动作采样
```

#### 训练循环
```python
for iteration in range(1000):
    # 1. 并行收集1000局游戏数据
    trajectories = collect_trajectories(num_games=1000)

    # 2. 计算GAE优势
    advantages, returns = compute_gae(trajectories)

    # 3. PPO更新（4轮）
    for epoch in range(4):
        policy_loss, value_loss = update_policy(
            states, actions, advantages, returns
        )

    # 4. 评估和保存
    if iteration % 50 == 0:
        evaluate()
    if iteration % 100 == 0:
        save_checkpoint()
```

## 训练时间表

### 阶段1: LLM数据生成

| 规模 | 游戏数 | 样本数 | 时间 | 成本 |
|------|--------|--------|------|------|
| 测试 | 10局 | ~1K | 10分钟 | $0.15 |
| 小规模 | 100局 | ~10K | 1-2小时 | $1.50 |
| 中规模 | 500局 | ~50K | 5-10小时 | $7.50 |
| 大规模 | 1000局 | ~100K | 10-20小时 | $15.00 |

**推荐**: 先用100局测试效果，满意后扩展到500-1000局。

### 阶段2: 监督学习预训练

| 数据规模 | 训练时间 | 预期效果 |
|----------|----------|----------|
| 10K样本 | 1-2小时 | 35-40%胜率 |
| 50K样本 | 2-3小时 | 45-50%胜率 |
| 100K样本 | 3-4小时 | 50-55%胜率 |

**特点**:
- 快速学会基本技巧
- 避免低效的随机探索
- 为PPO提供良好初始化

### 阶段3: PPO自我对弈

| 天数 | 迭代次数 | 预期胜率 | 关键里程碑 |
|------|----------|----------|------------|
| 1天 | ~100 | 55% | 稳定出合法牌 |
| 2-3天 | ~200-300 | 60% | 基本策略形成 |
| 4-5天 | ~400-500 | 65% | 高级技巧出现 |
| 6-7天 | ~600-700 | 70% | 超越LLM水平 |

**训练速度**:
- 每次迭代: ~10-15分钟
- 每天: ~100-150次迭代
- M4并行效率: ~8x加速

## 性能优化

### M4芯片优化

#### MPS加速
```python
device = torch.device("mps")  # Metal Performance Shaders
network = PolicyValueNetwork(...).to(device)
```

**优势**:
- GPU加速神经网络前向/反向传播
- 统一内存架构，减少数据传输
- 针对Apple Silicon优化

#### 多进程并行
```python
num_workers = 8  # 充分利用M4的8核心
with ProcessPoolExecutor(max_workers=8) as executor:
    futures = [executor.submit(simulate_game, ...) for _ in range(1000)]
```

**优势**:
- 游戏模拟在CPU上并行
- 神经网络推理在GPU上
- CPU-GPU流水线，最大化利用率

#### 内存管理
```yaml
batch_size: 256          # 适配16GB内存
games_per_iteration: 1000  # 平衡内存和效率
```

**内存使用**:
- 预训练: ~2GB
- PPO训练: ~4-6GB
- 峰值: ~8GB
- 剩余: ~8GB (系统+其他)

### 训练稳定性

#### 梯度裁剪
```python
torch.nn.utils.clip_grad_norm_(network.parameters(), max_norm=0.5)
```

#### 优势标准化
```python
advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)
```

#### 学习率调度
```python
# 可选: 使用学习率衰减
scheduler = torch.optim.lr_scheduler.StepLR(optimizer, step_size=100, gamma=0.9)
```

## 监控和评估

### TensorBoard指标

#### 训练指标
- `Train/AvgReward`: 平均奖励（越高越好）
- `Train/AvgLength`: 平均游戏长度
- `Train/PolicyLoss`: 策略损失（应该下降）
- `Train/ValueLoss`: 价值损失（应该下降）
- `Train/Entropy`: 策略熵（保持一定探索）

#### 系统指标
- `System/CPU`: CPU使用率
- `System/Memory`: 内存使用率
- `System/GPU`: GPU使用率（MPS）

### 评估方法

#### 自我对弈评估
```python
def evaluate():
    # 新模型 vs 旧模型
    win_rate = play_games(new_model, old_model, num_games=100)
    return win_rate
```

#### 对抗基准AI
```python
# 对抗不同难度的规则AI
win_rate_easy = play_vs_ai(model, difficulty="easy")
win_rate_medium = play_vs_ai(model, difficulty="medium")
win_rate_hard = play_vs_ai(model, difficulty="hard")
```

## 成本效益分析

### 方案对比

| 方案 | 时间 | 成本 | 最终胜率 | 优势 |
|------|------|------|----------|------|
| 纯PPO | 20天 | $0 | 65% | 无需API |
| PPO+LLM(100局) | 7-10天 | $1.50 | 68% | 快速收敛 |
| PPO+LLM(500局) | 8-12天 | $7.50 | 70% | 高质量 |
| PPO+LLM(1000局) | 10-13天 | $15.00 | 72% | 最佳效果 |

### ROI分析

**投入**:
- 时间: 7-13天（节省7-10天）
- 成本: $1.50-15.00
- 人力: 最小（自动化训练）

**产出**:
- 胜率提升: +3-7%
- 训练时间缩短: 35-50%
- 策略质量: 更接近人类专家

**结论**: ROI极高，强烈推荐使用混合方案。

## 风险和缓解

### 风险1: API成本超预算
**缓解**:
- 先用10-100局测试
- 监控API使用量
- 设置成本上限

### 风险2: 训练不稳定
**缓解**:
- 使用梯度裁剪
- 优势标准化
- 定期保存检查点

### 风险3: 过拟合LLM
**缓解**:
- 预训练后立即开始PPO
- 使用验证集监控
- 保持足够的探索（entropy_coef）

### 风险4: 硬件资源不足
**缓解**:
- 减少batch_size
- 减少num_workers
- 使用CPU训练（更慢但可行）

## 下一步优化

### 短期优化
1. **课程学习**: 从简单场景开始训练
2. **对手建模**: 显式建模对手策略
3. **奖励塑形**: 更细粒度的中间奖励

### 中期优化
1. **多任务学习**: 同时学习叫牌和出牌
2. **迁移学习**: 利用其他牌类游戏的知识
3. **集成学习**: 训练多个模型并集成

### 长期优化
1. **AlphaZero风格**: 结合MCTS搜索
2. **自我博弈进化**: 维护对手池
3. **人类反馈**: 结合人类专家标注

## 参考文献

1. **PPO算法**: Schulman et al. "Proximal Policy Optimization Algorithms" (2017)
2. **GAE**: Schulman et al. "High-Dimensional Continuous Control Using Generalized Advantage Estimation" (2016)
3. **AlphaGo**: Silver et al. "Mastering the game of Go with deep neural networks and tree search" (2016)
4. **OpenAI Five**: OpenAI. "Dota 2 with Large Scale Deep Reinforcement Learning" (2019)

## 附录

### A. 网络架构

```python
class PolicyValueNetwork(nn.Module):
    def __init__(self, state_dim=412, action_dim=1000):
        super().__init__()

        # 共享编码器
        self.encoder = nn.Sequential(
            nn.Linear(state_dim, 512),
            nn.ReLU(),
            nn.Linear(512, 256),
            nn.ReLU()
        )

        # 策略头
        self.policy_head = nn.Sequential(
            nn.Linear(256, 256),
            nn.ReLU(),
            nn.Linear(256, action_dim)
        )

        # 价值头
        self.value_head = nn.Sequential(
            nn.Linear(256, 128),
            nn.ReLU(),
            nn.Linear(128, 1)
        )
```

### B. 超参数调优建议

| 参数 | 默认值 | 调优范围 | 影响 |
|------|--------|----------|------|
| learning_rate | 3e-4 | 1e-4 ~ 1e-3 | 收敛速度 |
| gamma | 0.99 | 0.95 ~ 0.99 | 长期规划 |
| gae_lambda | 0.95 | 0.9 ~ 0.98 | 偏差-方差权衡 |
| clip_epsilon | 0.2 | 0.1 ~ 0.3 | 更新幅度 |
| entropy_coef | 0.01 | 0.001 ~ 0.1 | 探索程度 |

### C. 故障排查

**问题**: 训练不收敛
- 检查奖励设计是否合理
- 降低学习率
- 增加batch_size

**问题**: 内存溢出
- 减少batch_size
- 减少games_per_iteration
- 减少num_workers

**问题**: GPU利用率低
- 增加batch_size
- 检查数据加载是否成为瓶颈
- 使用更大的网络

**问题**: API调用失败
- 检查网络连接
- 验证API密钥
- 实施重试机制
