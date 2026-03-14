# GMN配置已成功应用

## 配置摘要

✓ **API类型**: Anthropic Claude
✓ **API Base**: https://codeflow.asia
✓ **模型**: claude-sonnet-4-6
✓ **环境变量**:
  - `ANTHROPIC_AUTH_TOKEN` (已设置)
  - `ANTHROPIC_BASE_URL` (已设置)

## 测试结果

```
✓ API连接成功
✓ 模型响应正常
✓ 系统就绪
```

## 下一步操作

### 选项1: 快速开始（推荐）

```bash
cd /Users/kalment/projects/tractor/cardgames/rl_training
./quickstart.sh
```

### 选项2: 手动执行

```bash
cd /Users/kalment/projects/tractor/cardgames/rl_training

# 1. 测试API连接
python3 test_llm_api.py

# 2. 生成专家数据（建议先测试100局）
python3 llm_teacher.py --generate --num-games 100

# 3. 预训练
python3 pretrain.py

# 4. PPO训练
python3 train_enhanced.py

# 5. 监控训练（另开终端）
tensorboard --logdir logs/tensorboard
```

## 成本估算

使用Claude Sonnet 4.6通过GMN：

| 阶段 | 游戏数 | 预计Token | 预计成本 |
|------|--------|-----------|----------|
| 测试 | 10局 | ~50K | $0.15 |
| 小规模 | 100局 | ~500K | $1.50 |
| 中规模 | 500局 | ~2.5M | $7.50 |
| 大规模 | 1000局 | ~5M | $15.00 |

**建议**: 先用100局测试效果，满意后再扩大规模。

## 训练时间表

### 阶段1: 数据生成（1-2小时）
- 100局游戏
- 生成约10,000条决策样本
- 成本: ~$1.50

### 阶段2: 预训练（2-4小时）
- 监督学习
- 达到40-50%胜率
- 成本: $0（本地M4）

### 阶段3: PPO微调（3-7天）
- 自我对弈优化
- 达到65-70%胜率
- 成本: $0（本地M4）

**总时间**: 3-7天
**总成本**: ~$1.50-15（取决于数据规模）

## 配置文件

`config.yaml` 已更新：

```yaml
llm:
  api_type: "anthropic"
  api_key: "${ANTHROPIC_AUTH_TOKEN}"
  api_base: "${ANTHROPIC_BASE_URL}"
  model: "claude-sonnet-4-6"
  max_tokens: 1024
  temperature: 0.7
```

## 常见问题

### Q: 如何查看API使用情况？
A: 检查GMN控制台的使用统计。

### Q: 如果API调用失败怎么办？
A:
1. 运行 `python3 test_llm_api.py` 诊断问题
2. 检查网络连接
3. 确认API密钥有效
4. 查看错误日志

### Q: 可以暂停和恢复吗？
A: 可以！数据生成支持断点续传：
```bash
# 从第50局继续
python3 llm_teacher.py --generate --num-games 100 --resume-from 50
```

### Q: 如何调整生成质量？
A: 修改 `config.yaml` 中的参数：
```yaml
llm:
  temperature: 0.5  # 降低随机性，提高一致性
  max_tokens: 2048  # 增加token数，获得更详细分析
```

## 监控和调试

### 查看生成进度
```bash
tail -f data/expert_dataset.json
```

### 查看训练日志
```bash
tensorboard --logdir logs/tensorboard
```

### 系统资源监控
```bash
# CPU和内存
htop

# GPU使用（M4）
sudo powermetrics --samplers gpu_power -i 1000
```

## 技术支持

如遇问题，请提供：
1. 错误信息
2. `python3 test_llm_api.py` 输出
3. `python3 test_system.py` 输出
4. 相关日志文件

---

**准备好了吗？运行 `./quickstart.sh` 开始训练！**
