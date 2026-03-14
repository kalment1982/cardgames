# 拖拉机自主进化AI设计方案 v1.2

## 1. 版本目标

v1.2 在 v1.1 基础上补足“可实现细节”，重点解决以下问题：

- 变异/交叉/分群缺少可执行定义
- 统计检验与样本量说明不充分
- 灰度发布与回滚阈值缺少硬标准
- 课程学习采用固定代次，不够自适应
- 资源预算缺少硬件与存储建议

基线文档：
- `/Users/kalment/projects/tractor/cardgames/doc/AI_Training_Best_Practices.md`
- `/Users/kalment/projects/tractor/cardgames/doc/自主进化AI设计方案_v1.1.md`

---

## 2. 策略基因（Genome）细化

### 2.1 混合类型基因表示

不是全部连续变量，采用混合基因：

- 连续基因（double, [0,1]）：
  - `EasyRandomnessRate`
  - `MediumRandomnessRate`
  - `HardRandomnessRate`
  - `ExpertRandomnessRate`
  - 其余各类 Bias / Weight
- 离散基因（int）：
  - `LeadThrowMinAdvantage`（范围 [0, 3]）

### 2.2 高斯变异规则

对每个基因以概率 `p_mutate` 进行变异：

- `p_mutate_small = 0.25`（小步搜索）
- `p_mutate_medium = 0.10`（探索）

连续基因变异：

```
g' = clamp(g + Normal(0, sigma_g), lower, upper)
```

建议 sigma：
- 随机率相关参数：`sigma = 0.03`
- 策略 bias/weight：`sigma = 0.05`

离散基因变异：

```
LeadThrowMinAdvantage' = clamp(LeadThrowMinAdvantage + step, 0, 3)
step ∈ {-1, +1}, P(step=+1)=0.5
```

### 2.3 依赖约束与修复（Repair）

候选生成后必须执行 `Repair()`，保证语义一致：

1. 难度随机率单调：
   - `Easy >= Medium >= Hard >= Expert`
2. 高难度不应弱于低难度关键攻击参数：
   - `Hard.FollowBeatAttemptBias >= Medium.FollowBeatAttemptBias`
   - `Expert.FollowBeatAttemptBias >= Hard.FollowBeatAttemptBias`
3. 所有 double 参数裁剪到 `[0,1]`
4. `LeadThrowMinAdvantage` 强制 int 且在 `[0,3]`

---

## 3. 交叉（Crossover）细化

### 3.1 采用“分块交叉”而非逐参数随机混搭

按参数语义分为 5 块：

1. 随机探索块
2. 先手策略块
3. 跟牌策略块
4. 分牌策略块
5. 收官策略块

交叉方式：
- 以块为单位从父代 A/B 选择
- 每块独立 Bernoulli(0.5)
- 交叉后执行 `Repair()`

### 3.2 允许混合与禁止混合

- 允许：同一块内参数整体继承，避免语义破碎
- 禁止：在同一块内逐基因“乱拼”，防止出现相互矛盾的策略组合

---

## 4. DataEngine 可执行规范

### 4.1 分层采样策略

按四维分层：

- `Difficulty`（easy/medium/hard/expert）
- `Role`（dealer/dealerPartner/opponent）
- `Phase`（early/mid/late）
- `Pattern`（single/pair/tractor/throw/trump-cut）

采用分层水库采样，确保每层最小样本配额，避免主流局面淹没长尾。

### 4.2 新鲜度衰减

采用指数衰减：

```
weight(age_days) = exp(-ln(2) * age_days / half_life_days)
```

建议：`half_life_days = 7`

### 4.3 长尾与不平衡处理

- 稀有局面（终局高分争夺、复杂甩牌失败）设置最小配额 `>= 15%`
- 训练采样权重上调 2x
- 每代输出长尾覆盖率报告

### 4.4 数据质量门禁

每代训练前必须通过：

- schema version 匹配
- 字段完整率 `>= 99.5%`
- 去重率（相同局面哈希）`<= 30%`
- 特征泄漏检查（禁止非公开信息字段）

---

## 5. 统计检验与样本量

### 5.1 检验方法

- 主指标（胜率差）：
  - 使用 paired self-play（同牌序 + 换位）
  - 采用 bootstrap（10,000 次重采样）估计 95% CI
- 次指标（错误率、延迟）：
  - 比例指标用 Wilson CI
  - 连续指标用 bootstrap CI

### 5.2 多重比较控制

24 个候选并行筛选时：

- Layer 1：Benjamini-Hochberg（FDR 10%）用于筛选
- Layer 3（仅前2名）：Bonferroni 修正，显著性阈值 `alpha = 0.025`

### 5.3 样本量与升级策略

- 初审：3500 局用于快速判定
- 若胜率提升落在灰区（`+0.5% ~ +2.5%`），自动追加到 12,000 局再判定
- 若提升 `>= +2.5%` 且 CI 下界 `>0`，可直接通过

---

## 6. 晋升与回滚（SLO 化）

### 6.1 晋升规则（v1.2）

候选晋升条件：

1. 非法出牌率 `=0`
2. 性能达标：平均 `<100ms`，P99 `<150ms`
3. 收益达标（动态门槛）：
   - Bootstrapping: `>= +1.0%` 或 CI 下界 `> -0.5%`
   - Growth: `>= +1.5%` 且 CI 下界 `> 0`
   - Mature: `>= +0.5%` + 复核通过
4. 稳定性：3 个不同 seed 重复评估通过

### 6.2 明显错误率定义

明显错误（Blunder）定义为满足任一规则：

- 可合法赢墩且当前墩分值 >= 20 分时，选择了显著劣势动作
- 对家已赢墩时，存在合法“保分动作”却反向盖过对家导致净分下降
- 缺门可毙牌阻断对手高分墩但未毙，且造成净分损失 >= 10

计算方式：

- 规则探测器自动标注 + 专家样本集校准
- `BlunderRate = blunder_actions / eligible_actions`

### 6.3 灰度发布与回滚阈值

灰度阶段：`5% -> 25% -> 100%`

每阶段最短观察窗口：`24h` 且最小样本 `>= 3000` 局。

任一条件触发立即回滚：

1. 非法出牌率 `> 0`
2. P99 延迟连续 10 分钟 `> 180ms`
3. 1 小时窗口胜率相对 Champion 下降 `> 3%`
4. 明显错误率 `> baseline * 2` 且绝对值 `> 6%`

---

## 7. MetaController 玩家分层细化

### 7.1 分层输入特征

使用最近 40 局统计：

- 胜率
- 平均净分差
- 被毙率/毙牌成功率
- 决策稳定性（波动）

### 7.2 分层策略

输出四层：`novice / regular / skilled / expert`

建议阈值（可后续标定）：

- novice: `winrate < 42%`
- regular: `42% <= winrate < 52%`
- skilled: `52% <= winrate < 62%`
- expert: `>= 62%`

采用双阈值滞回避免频繁抖动：

- 升档阈值 +2%
- 降档阈值 -2%

---

## 8. 课程学习改为“自适应调度”

不再固定“第1-3代/4-8代/9+代”，改为性能驱动：

- 若当前对手池胜率连续2代 `> 58%`，提升对手难度
- 若连续2代 `< 45%`，降低一个难度档并增加稳定训练
- 维持目标区间：`48% ~ 55%`（防止过易或过难）

---

## 9. 资源与容量规划

### 9.1 推荐硬件

- 最低可用：8 vCPU / 16GB RAM / 100GB SSD（单机调试）
- 建议训练：32 vCPU / 64GB RAM / 500GB SSD（并行评估）
- 高吞吐：64 vCPU / 128GB RAM（多进程并行 + 分代缓存）

### 9.2 存储预算（经验值）

- 单局压缩日志约 4-8KB
- 若每代 5-10 万局：约 200-800MB/代
- 保留最近 30 代原始日志需 6-24GB
- 策略文件体积小（<1MB/版本），可全量保留

### 9.3 并行 worker 选择

- `workers = min(vCPU - 2, 48)` 作为起始值
- 每代自动压测 5 分钟，选择吞吐最高且 P99 不超限的 worker 数

---

## 10. 落地优先级（工程执行）

### P0（必须）

- 实现混合基因 + 变异 + 交叉 + Repair
- 实现分层评估漏斗（200/500/1500/3500+追加）
- 实现 GateKeeper 与 SLO 回滚阈值
- 实现 `generation_report` 与 `data_quality_report`

### P1（应做）

- 实现 MetaController 分层与滞回
- 实现自适应课程学习调度
- 实现统计检验（bootstrap + 多重比较）

### P2（可选）

- 引入局部 RL 微调
- 多目标优化升级到 Stage C

---

## 11. 对评审问题的直接回应

1. 关于“基因并非全连续”的疑问：
   - 已改为混合基因，`LeadThrowMinAdvantage` 按离散变量处理，并加入 Repair 约束。

2. 关于统计严谨性：
   - 明确了 CI 与多重比较修正，并增加“3500 灰区 -> 自动追加到 12000 局”的机制。

3. 关于运营落地：
   - 灰度阶段、最短窗口、最小样本与回滚阈值都给出具体数值。

---

**文档版本**: v1.2  
**创建日期**: 2026-03-14  
**上一版本**: `/Users/kalment/projects/tractor/cardgames/doc/自主进化AI设计方案_v1.1.md`
