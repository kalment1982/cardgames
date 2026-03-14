# 拖拉机AI训练指南

## 目录

1. [环境准备](#环境准备)
2. [快速开始](#快速开始)
3. [阶段1: LLM数据生成](#阶段1-llm数据生成)
4. [阶段2: 监督学习预训练](#阶段2-监督学习预训练)
5. [阶段3: PPO强化学习](#阶段3-ppo强化学习)
6. [监控和调试](#监控和调试)
7. [常见问题](#常见问题)
8. [最佳实践](#最佳实践)

---

## 环境准备

### 系统要求

- **硬件**: Mac M4 16GB（或同等配置）
- **操作系统**: macOS 14+
- **Python**: 3.9+
- **磁盘空间**: 至少10GB可用空间

### 安装依赖

```bash
cd /Users/kalment/projects/tractor/cardgames/rl_training

# 安装Python依赖
pip3 install -r requirements.txt

# 验证安装
python3 test_system.py
```

### 配置API

你的CODEX API已经配置好：
```bash
# 环境变量已设置
echo $CODEX_API_KEY    # sk-fdefdf55827dde1f3...
echo $CODEX_API_BASE   # https://gmn.chuangzuoli.com/v1
```

配置文件 `config.yaml` 已自动使用这些环境变量。

---

## 快速开始

### 一键启动（推荐）

```bash
./quickstart.sh
```

这个脚本会：
1. 检查环境配置
2. 测试API连接
3. 引导你生成数据
4. 自动执行预训练
5. 启动PPO训练

### 手动执行

如果你想更精细地控制每个步骤：

```bash
# 1. 测试API
python3 test_llm_api.py

# 2. 生成数据（先测试10局）
python3 llm_teacher.py --generate --num-games 10

# 3. 预训练
python3 pretrain.py

# 4. PPO训练
python3 train_enhanced.py
```

---

## 阶段1: LLM数据生成

### 目标

使用Claude Sonnet 4.6分析游戏状态，生成高质量的专家决策数据。

### 步骤

#### 1.1 测试API连接

```bash
python3 test_llm_api.py
```

**预期输出**:
```
============================================================
测试LLM API连接
============================================================

配置信息:
  API类型: openai
  API Base: https://gmn.chuangzuoli.com/v1
  API Key: sk-fdefdf55827dde1f3...
  模型: claude-sonnet-4-6

正在测试API连接...
✓ API连接成功！

测试响应:
  拖拉机80分是一款流行的中国扑克牌游戏...

============================================================
✓ LLM配置正确，可以开始生成专家数据！
============================================================
```

#### 1.2 小规模测试（10局）

```bash
python3 llm_teacher.py --generate --num-games 10
```

**目的**:
- 验证数据生成流程
- 检查LLM决策质量
- 估算时间和成本

**预期时间**: 5-10分钟
**预期成本**: ~$0.15

**输出示例**:
```
LLM Teacher initialized:
  API Type: openai
  API Base: https://gmn.chuangzuoli.com/v1
  Model: claude-sonnet-4-6

Collecting expert dataset: 10 games
Starting from game 0
Progress: 10/10 games, 1234 samples

Dataset collection complete!
Total samples: 1234
Saved to: data/expert_dataset.json
```

#### 1.3 检查数据质量

```bash
python3 -c "
import json
with open('data/expert_dataset.json', 'r') as f:
    data = json.load(f)
print(f'总样本数: {len(data)}')
print(f'第一个样本: {data[0].keys()}')
print(f'状态维度: {len(data[0][\"state\"])}')
print(f'动作索引: {data[0][\"action\"]}')
"
```

**预期输出**:
```
总样本数: 1234
第一个样本: dict_keys(['state', 'action', 'player'])
状态维度: 412
动作索引: 3
```

#### 1.4 扩大规模（100-1000局）

如果测试满意，扩大数据规模：

```bash
# 100局（推荐）
python3 llm_teacher.py --generate --num-games 100

# 500局（高质量）
python3 llm_teacher.py --generate --num-games 500

# 1000局（最佳效果）
python3 llm_teacher.py --generate --num-games 1000
```

**时间和成本**:
| 规模 | 时间 | 成本 | 样本数 |
|------|------|------|--------|
| 100局 | 1-2小时 | $1.50 | ~10K |
| 500局 | 5-10小时 | $7.50 | ~50K |
| 1000局 | 10-20小时 | $15.00 | ~100K |

#### 1.5 断点续传

如果生成过程中断：

```bash
# 从第50局继续
python3 llm_teacher.py --generate --num-games 100 --resume-from 50
```

### 监控进度

```bash
# 另开终端，实时查看进度
watch -n 5 'wc -l data/expert_dataset.json'

# 查看最新样本
tail -f data/expert_dataset.json
```

### 成本控制

```bash
# 设置最大游戏数
python3 llm_teacher.py --generate --num-games 100

# 监控API使用
# 访问GMN控制台查看实时消费
```

---

## 阶段2: 监督学习预训练

### 目标

使用LLM生成的专家数据训练策略网络，快速学会基本技巧。

### 步骤

#### 2.1 检查数据

```bash
# 确认数据文件存在
ls -lh data/expert_dataset.json

# 查看样本数量
python3 -c "
import json
with open('data/expert_dataset.json', 'r') as f:
    data = json.load(f)
print(f'样本数: {len(data)}')
"
```

#### 2.2 开始预训练

```bash
python3 pretrain.py --config config.yaml
```

**预期输出**:
```
Loaded 10234 expert samples

Starting pretraining...
  Epochs: 10
  Batch size: 256
  Learning rate: 0.0003
  Train samples: 9210
  Val samples: 1024

Epoch 1/10, Batch 10/36, Loss: 6.8234
Epoch 1/10, Batch 20/36, Loss: 5.9123
Epoch 1/10, Batch 30/36, Loss: 5.2456

Epoch 1/10 Summary:
  Train Loss: 5.4567, Train Acc: 12.34%
  Val Loss: 5.1234, Val Acc: 15.67%

  ✓ Best model saved! Val Acc: 15.67%

...

Epoch 10/10 Summary:
  Train Loss: 2.1234, Train Acc: 45.67%
  Val Loss: 2.3456, Val Acc: 42.34%

  ✓ Best model saved! Val Acc: 42.34%

Pretraining complete!
  Best validation accuracy: 42.34%
  Model saved to: checkpoints/pretrained_from_llm.pt
```

#### 2.3 验证预训练模型

```bash
python3 -c "
import torch
model_path = 'checkpoints/pretrained_from_llm.pt'
state_dict = torch.load(model_path, map_location='cpu')
print(f'模型参数数量: {len(state_dict)}')
print(f'模型大小: {sum(p.numel() for p in state_dict.values()) / 1e6:.2f}M 参数')
print('✓ 预训练模型加载成功')
"
```

### 预期效果

- **训练时间**: 2-4小时
- **最终准确率**: 40-50%
- **模型大小**: ~5-10MB
- **胜率**: 40-50%（对抗随机AI）

### 调优建议

如果效果不理想：

```yaml
# 修改 config.yaml
pretrain:
  epochs: 15              # 增加训练轮数
  learning_rate: 0.0001   # 降低学习率
  batch_size: 128         # 减小批次大小
```

---

## 阶段3: PPO强化学习

### 目标

通过自我对弈优化策略，超越LLM水平，达到65-70%胜率。

### 步骤

#### 3.1 启动训练

```bash
python3 train_enhanced.py --config config.yaml
```

**预期输出**:
```
Using device: mps
State dimension: 412
Loading pretrained model from: checkpoints/pretrained_from_llm.pt
✓ Pretrained model loaded! Starting RL fine-tuning...

Starting PPO training...
  Total iterations: 1000
  Games per iteration: 1000
  Workers: 8

[Iteration 1/1000] Collecting experience...
Simulating games: 100%|████████████| 1000/1000 [10:23<00:00, 1.60it/s]
Computing advantages...
Updating policy...

[Iteration 1] Summary:
  Avg Reward: -2.34
  Avg Length: 52.3
  Policy Loss: 0.4567
  Value Loss: 1.2345
  Time: 623.4s
  CPU: 85.3%, Memory: 42.1%

...
```

#### 3.2 启动TensorBoard监控

**另开终端**:
```bash
cd /Users/kalment/projects/tractor/cardgames/rl_training
tensorboard --logdir logs/tensorboard
```

然后访问: http://localhost:6006

#### 3.3 监控关键指标

在TensorBoard中关注：

**训练指标**:
- `Train/AvgReward`: 应该逐渐上升
- `Train/PolicyLoss`: 应该逐渐下降
- `Train/ValueLoss`: 应该逐渐下降

**系统指标**:
- `System/CPU`: 应该在70-90%
- `System/Memory`: 应该在40-60%

### 训练时间表

| 天数 | 迭代 | 预期胜率 | 里程碑 |
|------|------|----------|--------|
| 1天 | ~100 | 55% | 稳定出牌 |
| 2-3天 | ~200-300 | 60% | 基本策略 |
| 4-5天 | ~400-500 | 65% | 高级技巧 |
| 6-7天 | ~600-700 | 70% | 超越LLM |

### 中途检查

```bash
# 查看最新检查点
ls -lht checkpoints/

# 加载检查点测试
python3 -c "
import torch
checkpoint = torch.load('checkpoints/checkpoint_100.pt', map_location='cpu')
print(f'迭代: {checkpoint[\"iteration\"]}')
print('✓ 检查点加载成功')
"
```

### 提前停止

如果达到目标胜率，可以提前停止：

```bash
# Ctrl+C 停止训练
# 最新的检查点会自动保存
```

---

## 监控和调试

### 实时监控

#### 系统资源

```bash
# CPU和内存
htop

# GPU使用（M4）
sudo powermetrics --samplers gpu_power -i 1000

# 磁盘空间
df -h
```

#### 训练日志

```bash
# 实时查看训练输出
tail -f logs/training.log

# 查看错误日志
tail -f logs/error.log
```

### TensorBoard可视化

访问 http://localhost:6006 查看：

1. **SCALARS**: 训练曲线
   - Reward趋势
   - Loss变化
   - 系统资源

2. **DISTRIBUTIONS**: 参数分布
   - 权重分布
   - 梯度分布

3. **HISTOGRAMS**: 激活值
   - 网络层激活
   - 动作概率

### 性能分析

```bash
# 查看训练速度
python3 -c "
import json
with open('logs/tensorboard/events.out.tfevents.*', 'r') as f:
    # 分析每次迭代时间
    pass
"
```

### 调试技巧

#### 问题1: 训练不收敛

**症状**: Reward不上升，Loss不下降

**排查**:
```bash
# 检查奖励设计
python3 -c "
from game_engine import TractorGame
game = TractorGame()
game.reset()
# 模拟一局游戏，打印奖励
"

# 降低学习率
# 修改 config.yaml: learning_rate: 0.0001
```

#### 问题2: 内存溢出

**症状**: OOM错误

**解决**:
```yaml
# 修改 config.yaml
ppo:
  batch_size: 128           # 从256降到128
  games_per_iteration: 500  # 从1000降到500
  num_workers: 4            # 从8降到4
```

#### 问题3: GPU利用率低

**症状**: MPS使用率<50%

**解决**:
```yaml
# 修改 config.yaml
ppo:
  batch_size: 512           # 增大批次
  ppo_epochs: 8             # 增加PPO轮数
```

---

## 常见问题

### Q1: API调用失败怎么办？

**A**:
```bash
# 1. 检查网络
ping gmn.chuangzuoli.com

# 2. 验证API密钥
echo $CODEX_API_KEY

# 3. 测试连接
python3 test_llm_api.py

# 4. 查看错误日志
cat logs/llm_teacher.log
```

### Q2: 如何暂停和恢复训练？

**A**:
```bash
# 暂停: Ctrl+C

# 恢复: 重新运行，会自动加载最新检查点
python3 train_enhanced.py

# 或指定检查点
python3 train_enhanced.py --resume checkpoints/checkpoint_100.pt
```

### Q3: 如何评估模型效果？

**A**:
```bash
# 对抗规则AI
python3 evaluate.py --model checkpoints/checkpoint_500.pt --opponent hard

# 自我对弈
python3 evaluate.py --model checkpoints/checkpoint_500.pt --self-play
```

### Q4: 训练太慢怎么办？

**A**:
```yaml
# 方案1: 减少游戏数
ppo:
  games_per_iteration: 500  # 从1000降到500

# 方案2: 减少迭代次数
ppo:
  num_iterations: 500       # 从1000降到500

# 方案3: 增加并行度
ppo:
  num_workers: 12           # 如果CPU核心够多
```

### Q5: 如何调整训练质量？

**A**:
```yaml
# 提高质量（更慢）
ppo:
  games_per_iteration: 2000
  ppo_epochs: 8
  batch_size: 512

# 平衡速度和质量（推荐）
ppo:
  games_per_iteration: 1000
  ppo_epochs: 4
  batch_size: 256

# 快速迭代（更快但质量略低）
ppo:
  games_per_iteration: 500
  ppo_epochs: 2
  batch_size: 128
```

---

## 最佳实践

### 1. 渐进式训练

```bash
# 阶段1: 小规模测试
python3 llm_teacher.py --generate --num-games 10
python3 pretrain.py
python3 train_enhanced.py  # 运行50次迭代后停止

# 阶段2: 中等规模
python3 llm_teacher.py --generate --num-games 100
python3 pretrain.py
python3 train_enhanced.py  # 运行200次迭代

# 阶段3: 大规模训练
python3 llm_teacher.py --generate --num-games 500
python3 pretrain.py
python3 train_enhanced.py  # 完整训练
```

### 2. 定期备份

```bash
# 每天备份检查点
cp -r checkpoints checkpoints_backup_$(date +%Y%m%d)

# 备份配置
cp config.yaml config_backup_$(date +%Y%m%d).yaml

# 备份数据
cp data/expert_dataset.json data/expert_dataset_backup_$(date +%Y%m%d).json
```

### 3. 版本控制

```bash
# 记录每次训练的配置
git add config.yaml
git commit -m "Training run $(date +%Y%m%d): 100 games, 500 iterations"

# 标记重要检查点
git tag -a v1.0 -m "First successful training: 65% win rate"
```

### 4. 实验追踪

创建训练日志：
```bash
cat > training_log.md << EOF
# 训练记录

## 实验1: $(date +%Y-%m-%d)
- 数据规模: 100局
- 预训练准确率: 42%
- PPO迭代: 200
- 最终胜率: 60%
- 备注: 基线实验

## 实验2: $(date +%Y-%m-%d)
- 数据规模: 500局
- 预训练准确率: 48%
- PPO迭代: 500
- 最终胜率: 68%
- 备注: 增加数据规模
EOF
```

### 5. 超参数搜索

```bash
# 创建多个配置文件
cp config.yaml config_lr_high.yaml
cp config.yaml config_lr_low.yaml

# 修改学习率
# config_lr_high.yaml: learning_rate: 0.001
# config_lr_low.yaml: learning_rate: 0.0001

# 并行训练（不同终端）
python3 train_enhanced.py --config config_lr_high.yaml
python3 train_enhanced.py --config config_lr_low.yaml

# 比较结果
tensorboard --logdir logs/
```

---

## 下一步

训练完成后：

1. **评估模型**: 对抗不同难度的AI
2. **导出模型**: 转换为C#可用格式
3. **集成到游戏**: 替换现有AI
4. **持续优化**: 收集人类对局数据继续训练

详见: [模型部署指南](DEPLOYMENT.md)

---

## 支持

遇到问题？

1. 查看 [常见问题](#常见问题)
2. 检查 [训练设计文档](TRAINING_DESIGN.md)
3. 查看日志文件: `logs/`
4. 运行诊断: `python3 test_system.py`

祝训练顺利！🚀
