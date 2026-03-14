# LLM技巧审核工具使用指南

## 概述

`skill_reviewer.py` 是一个使用LLM审核和优化拖拉机游戏技巧的工具。它可以：

1. **审核技巧文档** - 检查技巧的正确性、完整性
2. **建议新技巧** - 发现可能遗漏的重要技巧
3. **优化参数** - 调整参数的默认值

## 使用方式

### 1. 审核整个技巧文档

```bash
cd /Users/kalment/projects/tractor/cardgames/rl_training

python3 skill_reviewer.py \
  --action review \
  --doc ../doc/Tractor_Skills_And_Parameters.md \
  --output skill_review_result.md
```

**输出内容**:
- 总体评价
- 正确性评分和问题
- 完整性评分和遗漏技巧
- 参数设置建议
- 优先级调整建议
- 改进建议

**预期时间**: 1-2分钟
**预期成本**: ~$0.05

### 2. 建议新技巧

```bash
python3 skill_reviewer.py \
  --action suggest \
  --output new_skills_suggestions.md
```

**输出内容**:
- 5-10个可能遗漏的技巧
- 每个技巧的详细描述
- 重要性评分
- 建议的参数设置

**用途**:
- 发现文档中遗漏的技巧
- 扩展技巧体系
- 为新一代AI提供更多特征

### 3. 优化参数设置

```bash
python3 skill_reviewer.py \
  --action optimize \
  --output parameter_optimization.md
```

**输出内容**:
- 每个参数的调整建议
- 调整理由
- 新增参数建议
- 预期效果

**用途**:
- 优化现有参数的默认值
- 发现参数之间的不合理关系
- 为参数进化提供更好的起点

## 实际应用场景

### 场景1: 验证技巧正确性

**问题**: 不确定某个技巧描述是否正确

**解决**:
```bash
# 审核整个文档
python3 skill_reviewer.py --action review

# 查看审核结果
cat skill_review_result.md
```

**示例输出**:
```json
{
  "correctness": {
    "score": 8,
    "issues": [
      "1.1节中'避免垫对子'的优先级可能过低，实战中保留对子非常重要",
      "2.2节保底策略缺少'最后3墩'的具体判断标准"
    ],
    "suggestions": [
      "建议将PartnerWinning_KeepPairsPriority从0.60提高到0.75",
      "增加BottomProtection_TriggerThreshold参数，设置为3墩"
    ]
  }
}
```

### 场景2: 发现遗漏的技巧

**问题**: 感觉技巧体系不完整，但不知道缺什么

**解决**:
```bash
python3 skill_reviewer.py --action suggest
cat new_skills_suggestions.md
```

**示例输出**:
```markdown
## 建议的新技巧

### 1. 首轮试探策略
**描述**: 第一轮出牌时，通过出中等大小的牌试探对手的手牌强度
**重要性**: 8/10
**建议参数**:
- FirstRound_ProbeStrategy: 0.70
- FirstRound_AvoidBigCardsPriority: 0.65

### 2. 分数临界点判断
**描述**: 当分数接近80分临界点时（75-85分），调整策略
**重要性**: 9/10
**建议参数**:
- Critical_ScoreThreshold: 80
- Critical_ConservativeBias: 0.80
```

### 场景3: 优化参数设置

**问题**: 参数默认值不确定是否合理

**解决**:
```bash
python3 skill_reviewer.py --action optimize
cat parameter_optimization.md
```

**示例输出**:
```json
{
  "adjustments": [
    {
      "param": "PartnerWinning_GivePointsPriority",
      "current": 0.85,
      "suggested": 0.90,
      "reason": "送分给队友是最重要的配合策略，应该有更高优先级"
    },
    {
      "param": "CannotWin_PreserveLongSuitPriority",
      "current": 0.60,
      "suggested": 0.70,
      "reason": "保留长门对后期控制非常重要，权重偏低"
    }
  ],
  "new_params": [
    {
      "name": "PartnerWinning_PointCardThreshold",
      "value": 10,
      "reason": "定义'分牌'的阈值，10分以上才算重要分牌"
    }
  ]
}
```

## 工作流程建议

### 迭代优化流程

```
1. 审核现有文档
   ↓
2. 修复发现的问题
   ↓
3. 建议新技巧
   ↓
4. 补充遗漏的技巧
   ↓
5. 优化参数设置
   ↓
6. 更新AIStrategyParameters.cs
   ↓
7. 运行训练验证效果
   ↓
8. 回到步骤1
```

### 定期审核

**建议频率**:
- 每次大版本更新前审核一次
- 发现AI表现异常时审核
- 新增技巧后审核
- 每月定期审核一次

## 成本估算

| 操作 | Token消耗 | 成本 | 时间 |
|------|-----------|------|------|
| 审核文档 | ~5K | $0.05 | 1-2分钟 |
| 建议技巧 | ~3K | $0.03 | 1分钟 |
| 优化参数 | ~2K | $0.02 | 1分钟 |
| **总计** | ~10K | **$0.10** | **3-4分钟** |

**结论**: 成本极低，可以频繁使用。

## 输出文件说明

### skill_review_result.md

完整的审核报告，包含：
- 总体评价
- 各维度评分（正确性、完整性、参数、优先级）
- 具体问题列表
- 改进建议
- 新技巧建议
- 新参数建议

### new_skills_suggestions.md

LLM建议的新技巧列表，包含：
- 技巧名称和描述
- 重要性评分
- 应用场景
- 建议的参数

### parameter_optimization.md

参数优化建议，包含：
- 每个参数的调整建议
- 调整理由
- 新增参数建议
- 预期效果

## 与训练系统的集成

### 集成到训练流程

```bash
# 1. 审核技巧文档
python3 skill_reviewer.py --action review

# 2. 根据审核结果更新文档
vim ../doc/Tractor_Skills_And_Parameters.md

# 3. 更新C#代码中的参数
vim ../src/Core/AI/AIStrategyParameters.cs

# 4. 重新编译
cd ../
dotnet build

# 5. 运行训练验证
cd rl_training
python3 llm_teacher.py --generate --num-games 10

# 6. 如果效果好，扩大规模
python3 llm_teacher.py --generate --num-games 100
```

### 与LLM Teacher配合

**LLM Teacher的两个用途**:

1. **生成训练数据** (已实现)
   - 分析游戏状态
   - 生成专家决策
   - 用于预训练

2. **审核技巧体系** (新增)
   - 验证技巧正确性
   - 发现遗漏技巧
   - 优化参数设置

**配合使用**:
```bash
# 先审核技巧
python3 skill_reviewer.py --action review

# 根据审核结果优化技巧文档
# ...

# 然后用优化后的技巧生成训练数据
python3 llm_teacher.py --generate --num-games 100
```

## 高级用法

### 审核特定技巧

修改 `skill_reviewer.py`，添加：

```python
# 审核单个技巧
result = reviewer.review_specific_skill(
    skill_name="对家赢牌时的垫牌策略",
    skill_description="""
    技巧：
    - 优先送分牌（5/10/K）
    - 垫小牌（3/4/6），保留大牌（A/K/Q）
    - 避免垫主牌
    - 避免垫对子和拖拉机
    """
)
print(result)
```

### 批量审核

```bash
# 创建审核脚本
cat > batch_review.sh << 'EOF'
#!/bin/bash

echo "开始批量审核..."

# 审核文档
python3 skill_reviewer.py --action review --output review_$(date +%Y%m%d).md

# 建议新技巧
python3 skill_reviewer.py --action suggest --output suggest_$(date +%Y%m%d).md

# 优化参数
python3 skill_reviewer.py --action optimize --output optimize_$(date +%Y%m%d).md

echo "✓ 批量审核完成！"
ls -lh *_$(date +%Y%m%d).md
EOF

chmod +x batch_review.sh
./batch_review.sh
```

## 注意事项

1. **API成本**: 虽然单次成本低，但频繁调用会累积
2. **结果验证**: LLM的建议需要人工验证，不能盲目采纳
3. **版本控制**: 每次审核后保存结果，便于对比
4. **实战验证**: 参数调整后必须通过实战验证效果

## 总结

LLM技巧审核工具提供了一个**低成本、高效率**的方式来：
- 验证技巧体系的正确性
- 发现遗漏的重要技巧
- 优化参数设置

**建议使用频率**:
- 开发阶段：每天审核一次
- 稳定阶段：每周审核一次
- 维护阶段：每月审核一次

**成本**: 每次审核约$0.10，完全可以接受。

**价值**: 帮助构建更完善的技巧体系，提升AI质量。
