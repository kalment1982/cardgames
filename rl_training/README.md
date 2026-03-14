# 增强版RL训练系统使用指南

## 📚 文档导航

- **[快速开始](#快速开始)** - 立即开始训练
- **[训练设计](TRAINING_DESIGN.md)** - 完整的技术设计文档
- **[训练指南](TRAINING_GUIDE.md)** - 详细的操作指南和故障排查
- **[CODEX配置](GMN_SETUP.md)** - API配置说明

## 系统架构

```
PPO + 增强状态编码 + LLM Teacher
├── 增强状态编码 (412维)
│   ├── 基础特征 (338维)
│   ├── 对手手牌推断 (54维)
│   └── 历史出牌模式 (20维)
├── LLM Teacher (预训练)
│   ├── 支持自定义API配置
│   ├── 生成专家数据
│   └── 监督学习预训练
└── PPO微调
    ├── 多进程并行
    ├── MPS加速 (M4 GPU)
    └── 自我对弈优化
```

## 快速开始

### 1. 配置已完成 ✓

你的CODEX API已经配置好：
- `CODEX_API_KEY`: API密钥 ✓
- `CODEX_API_BASE`: https://gmn.chuangzuoli.com/v1 ✓

配置文件已更新为使用这些环境变量。

### 2. 测试API连接

```bash
cd rl_training

# 测试LLM API连接
python3 test_llm_api.py
```

### 3. 生成专家数据

```bash
cd rl_training

# 生成1000局专家数据（预计成本: $100-200）
python llm_teacher.py --generate --num-games 1000

# 如果中断，可以继续：
python llm_teacher.py --generate --num-games 1000 --resume-from 500
```

### 4. 预训练策略网络

```bash
# 使用专家数据预训练（约1天，达到50%胜率）
python pretrain.py --config config.yaml
```

### 5. PPO微调

```bash
# 继续PPO自我对弈训练（5-10天，达到70%胜率）
python train_enhanced.py --config config.yaml
```

### 6. 监控训练进度

```bash
# 启动TensorBoard
tensorboard --logdir logs/tensorboard
```

然后访问 http://localhost:6006

## 配置说明

### config.yaml 详解

```yaml
# LLM API配置
llm:
  api_type: "openai"              # API类型: openai / anthropic / custom
  api_key: "${YOUR_KEY_VAR}"      # 支持任意环境变量名
  api_base: "${YOUR_URL_VAR}"     # 支持任意环境变量名
  model: "gpt-4"                   # 模型名称
  max_tokens: 1024                 # 最大token数
  temperature: 0.7                 # 温度参数
  timeout: 30                      # 超时时间(秒)

# 数据生成配置
data_generation:
  num_games: 1000                  # 生成游戏数量
  save_path: "data/expert_dataset.json"
  batch_size: 10                   # 每批保存一次
  resume_from: null                # 断点续传

# 预训练配置
pretrain:
  epochs: 10                       # 训练轮数
  batch_size: 256                  # 批次大小
  learning_rate: 0.0003            # 学习率
  weight_decay: 0.0001             # 权重衰减
  validation_split: 0.1            # 验证集比例

# PPO训练配置
ppo:
  num_iterations: 1000             # 总迭代次数
  games_per_iteration: 1000        # 每次迭代游戏数
  num_workers: 8                   # 并行进程数
  learning_rate: 0.0003            # 学习率
  gamma: 0.99                      # 折扣因子
  gae_lambda: 0.95                 # GAE参数
  clip_epsilon: 0.2                # PPO裁剪
  value_loss_coef: 0.5             # 价值损失系数
  entropy_coef: 0.01               # 熵系数
  max_grad_norm: 0.5               # 梯度裁剪
  ppo_epochs: 4                    # 每次迭代的PPO轮数
  batch_size: 256                  # 批次大小

# 增强状态编码
state_encoding:
  base_dim: 338                    # 基础特征维度
  opponent_belief_dim: 54          # 对手推断维度
  play_pattern_dim: 20             # 历史模式维度
  # 总维度: 412

# 系统配置
system:
  device: "mps"                    # mps / cpu / cuda
  num_workers: 8                   # 工作进程数
  log_interval: 10                 # 日志间隔
  eval_interval: 50                # 评估间隔
  save_interval: 100               # 保存间隔
  tensorboard_dir: "logs/tensorboard"
```

## 训练流程

### 阶段1: LLM数据生成 (1-2天)

- 调用Codex API分析游戏状态
- 生成10,000-50,000条专家决策
- 成本: $100-200

### 阶段2: 监督学习预训练 (1天)

- 用专家数据训练策略网络
- 快速学会基本技巧
- 达到50%胜率

### 阶段3: PPO自我对弈 (5-10天)

- 在预训练基础上继续优化
- 超越LLM水平
- 达到70%+胜率

### 总时间: 7-13天

对比纯PPO的20天，节省7-10天！

## 目录结构

```
rl_training/
├── config.yaml                    # 配置文件
├── llm_teacher.py                 # LLM Teacher实现
├── enhanced_state_encoder.py      # 增强状态编码器
├── pretrain.py                    # 预训练脚本
├── train_enhanced.py              # 增强训练脚本
├── game_engine.py                 # 游戏引擎
├── ppo_agent.py                   # PPO智能体
├── state_encoder.py               # 基础状态编码器
├── requirements.txt               # 依赖包
├── data/                          # 数据目录
│   └── expert_dataset.json        # 专家数据集
├── checkpoints/                   # 检查点目录
│   ├── pretrained_from_llm.pt     # 预训练模型
│   └── checkpoint_*.pt            # 训练检查点
└── logs/                          # 日志目录
    └── tensorboard/               # TensorBoard日志
```

## 常见问题

### Q: 如何使用不同的API？

A: 修改 `config.yaml` 中的 `api_type`：

```yaml
# OpenAI格式
llm:
  api_type: "openai"
  api_key: "${YOUR_KEY}"
  api_base: "${YOUR_URL}"

# Anthropic格式
llm:
  api_type: "anthropic"
  api_key: "${YOUR_KEY}"
  api_base: "${YOUR_URL}"
```

### Q: 如何调整训练速度？

A: 修改并行进程数和游戏数量：

```yaml
ppo:
  num_workers: 8              # 增加进程数（不超过CPU核心数）
  games_per_iteration: 1000   # 减少游戏数加快迭代
```

### Q: 如何节省API成本？

A: 减少生成的游戏数量：

```yaml
data_generation:
  num_games: 500              # 从1000减少到500
```

即使只有500局数据，也能显著提升训练效果。

### Q: 训练中断了怎么办？

A: 数据生成支持断点续传：

```bash
# 从第500局继续
python llm_teacher.py --generate --resume-from 500
```

PPO训练会自动加载最新的检查点。

### Q: 如何评估训练效果？

A: 查看TensorBoard：

```bash
tensorboard --logdir logs/tensorboard
```

关注指标：
- `Train/AvgReward`: 平均奖励（越高越好）
- `Train/PolicyLoss`: 策略损失（应该下降）
- `Train/ValueLoss`: 价值损失（应该下降）

## 性能优化

### M4芯片优化

系统已针对M4芯片优化：

1. **MPS加速**: 使用Metal Performance Shaders加速神经网络
2. **多进程并行**: 8个进程并行模拟游戏
3. **统一内存**: 充分利用M4的统一内存架构

### 内存使用

- 预训练: ~2GB
- PPO训练: ~4-6GB
- 峰值: ~8GB

16GB内存完全够用！

## 下一步

训练完成后，将模型集成回游戏：

1. 导出最佳模型
2. 转换为C#可用格式
3. 集成到AIPlayer
4. 测试对战效果

详见后续文档。
