# 拖拉机自主进化AI设计方案 v1.3

## 1. 本版修订目标

v1.3 是对 v1.2 的工程化修订版，重点吸收评审意见并消除实现歧义：

- 参数修复逻辑改为“按语义分组约束”，不再做全局同向约束
- 统计检验简化：只在候选筛选层做多重比较控制，最终晋升用单次比较
- 补齐日志字段，确保 DataEngine 能直接抽取训练与评估特征
- 去重哈希改为规范化浮点序列化，避免精度噪声导致伪差异
- 长尾配额、候选规模、课程学习改为“状态驱动动态策略”
- 增加回滚后冷却期与分级采样率策略

基线：
- `/Users/kalment/projects/tractor/cardgames/doc/自主进化AI设计方案_v1.2.md`

---

## 2. 基因约束：按语义分组，不做一刀切

### 2.1 约束规则

修复（Repair）阶段按参数语义组执行约束：

1. **随机率组（探索）**
   - 约束：`Easy >= Medium >= Hard >= Expert`

2. **攻击倾向组（如 FollowBeatAttemptBias）**
   - 约束：`Easy <= Medium <= Hard <= Expert`

3. **保守倾向组（如 LeadConservativeBias）**
   - 约束：`Easy >= Medium >= Hard >= Expert`

4. **中性组（无天然单调语义）**
   - 仅做范围裁剪，不做跨难度单调约束

> 结论：不再使用“全部参数 Easy>=Expert”这类全局规则，必须先分组再约束。

### 2.2 实现方式（推荐）

用 `GeneSpec` 显式声明每个参数的约束方向：

```csharp
public enum MonotonicConstraint { None, AscByDifficulty, DescByDifficulty }

public sealed record GeneSpec(
    string Name,
    double Min,
    double Max,
    MonotonicConstraint Constraint,
    int FloatPrecision = 6
);
```

Repair 按 `GeneSpec.Constraint` 执行，避免硬编码。

---

## 3. 统计检验简化（防止过度保守）

### 3.1 分层策略

1. **Layer1（24 候选筛选）**
   - 可选 A：只按点估计排序取 Top-K（最快）
   - 可选 B：BH(FDR=0.10)（更稳）

2. **Layer2/Layer3（小规模对比）**
   - 不再做多重比较校正
   - 使用单次比较（paired self-play + bootstrap CI）

3. **最终晋升**
   - 主规则：`95% CI 下界 > 0`
   - 不使用 Bonferroni（避免过度保守造成进化停滞）

### 3.2 质量晋升（平局晋升）

当胜率差在 `[-0.5%, +0.5%]` 内，满足任一可晋升（标记“质量晋升”）：

- `BlunderRate` 下降 `>= 10%`
- `P99Latency` 下降 `>= 10%`
- `Diversity` 提升 `>= 8%`

---

## 4. 日志字段补齐（DataEngine 可用前提）

当前日志事件可用，但训练所需关键字段不完整。新增 `ai.decision` 事件（schema v1.2）：

- **payload**
  - `ai_player_index`
  - `ai_difficulty`
  - `decision_type`（lead/follow/bury）
  - `candidate_count`
  - `selected_cards`
- **metrics**
  - `decision_latency_ms`
  - `is_legal`（0/1）
  - `is_blunder`（0/1/null，在线可先 null）
  - `is_optimal`（0/1/null）

建议接入点：
- 在自博弈执行器（Evolution Runner 内）包装 AI 调用并落日志
- 线上对局只做轻量指标采样（见第 9 节）

---

## 5. 候选去重：规范化哈希

避免 `hash(str(params))` 造成浮点精度伪差异，使用规范化序列化：

```python
# pseudo
key = tuple(round(v, 6) for v in ordered_values)
h = sha256(repr(key).encode()).hexdigest()
```

要求：
- 固定字段顺序
- 固定浮点精度（建议 6 位）
- 离散参数先转 int 再入 key

---

## 6. 动态探索策略（配额 + 候选数）

### 6.1 长尾样本配额

按代际状态动态调整：

- 前 5 代（探索期）：`30%`
- 正常期：`15%`
- 停滞期（连续 8 代无晋升）：`40%`

### 6.2 候选数量 N

- 前 3 代：`N=32`
- 第 4-10 代：`N=24`
- 停滞期：`N=40`

并行资源不足时可自动降级到 `N=24/16`，优先保证 Layer3 质量。

---

## 7. MetaController 分层对齐（实时 + 长期）

避免“只看最近40局”导致新账号误判，采用双通道分层：

1. **实时分层（Recent-40）**
   - 用最近 40 局胜率和净分差快速响应当前状态

2. **长期分层（Lifetime）**
   - 用总局数 + 历史胜率校准
   - 例：总局数 `<20` 强制 novice 权重上调

3. **融合策略**
   - `final_score = 0.6 * recent + 0.4 * lifetime`（可调）
   - 引入滞回阈值防抖动（升档 +2%，降档 -2%）

---

## 8. 数据新鲜度衰减（含特例）

标准样本权重：

```
w = exp(-ln(2) * days_old / 7.0)
```

说明：该式对应“半衰期 7 天”。

特例：
- `hard_cases`：不衰减，`w=1`
- 稀有长尾：半衰期翻倍（14 天），衰减更慢

---

## 9. 回滚冷却与日志采样率

### 9.1 回滚冷却

- 任一灰度阶段触发回滚后，`24h` 内禁止再次晋升
- 冷却期仅允许评估，不允许 promote，防止反复横跳

### 9.2 日志采样率分级

为控制存储与吞吐，按用途分级：

- 训练数据：`100%` 采样
- 监控指标：`10%` 采样
- 详细上下文（调试）：`1%` 采样

采样策略需可配置，并支持按故障开关临时提升采样率。

---

## 10. 晋升门禁（v1.3 最终）

候选可晋升 Champion 需满足：

1. 非法出牌率 `= 0`
2. 性能达标：平均 `<100ms`，P99 `<150ms`
3. 胜率统计：`95% CI 下界 > 0`，或满足“质量晋升”条件
4. 稳定性：3 个 seed 重复评估通过
5. 若处于回滚冷却期：禁止晋升

---

## 11. 工程落地清单（P0）

1. `GeneSpec + Repair`：实现按语义分组单调约束
2. `ai.decision` 日志扩展：补齐 latency/legal/blunder/difficulty 字段
3. `LayeredEvaluator`：支持动态 N、动态长尾配额、质量晋升判定
4. `StatsAnalyzer`：Layer1 可选 BH；Final 仅 bootstrap CI
5. `ReleaseManager`：新增回滚冷却期与采样率配置

---

**文档版本**: v1.3  
**创建日期**: 2026-03-14  
**上一版本**: `/Users/kalment/projects/tractor/cardgames/doc/自主进化AI设计方案_v1.2.md`
