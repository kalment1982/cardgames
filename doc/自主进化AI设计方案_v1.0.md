# 拖拉机自主进化AI设计方案 v1.0

## 1. 目标与边界

本方案在 `/Users/kalment/projects/tractor/cardgames/doc/AI_Training_Best_Practices.md` 的约束下扩展“自主进化”能力：

- 保留硬约束：`非法出牌率=0`、`平均决策<100ms`、`最大决策<200ms`、`AI不可读取非公开信息`
- 持续优化软目标：胜率梯度、策略多样性、人性化程度
- 新增进化目标：模型可在无人值守下完成“采样 -> 训练 -> 验证 -> 晋升/回滚”闭环

> 设计原则：任何进化都不能突破规则合法性、公平性和性能预算。

---

## 2. 自主进化闭环（EvoLoop）

```
线上/离线对局数据
    -> 数据筛选与标注
    -> 候选策略生成（参数变异 + 自博弈 + 局部强化学习）
    -> 多阶段验证（核心指标/约束/次要指标）
    -> 统计显著性检验
    -> Champion/Challenger 晋升
    -> 灰度发布与监控
    -> 失败自动回滚
```

每个循环称为一个 `generation`，输出：
- 新策略参数 `policy_vX.Y.json`
- 验证报告 `report_vX.Y.md`
- 对比报告 `vs_champion_vX.Y.md`

---

## 3. 系统架构

### 3.1 模块划分

1. **DataEngine（数据引擎）**
   - 输入：自博弈日志、真人对局日志（匿名）
   - 输出：训练集（state/action/reward）、关键局面集（hard cases）
   - 关键能力：去重、异常局过滤、按难度分桶

2. **PolicyFactory（候选生成器）**
   - 输入：当前冠军策略 `champion`
   - 输出：N 个候选 `challenger_i`
   - 生成方式：
     - 参数变异（对 `AIStrategyParameters` 做高斯扰动）
     - 组合交叉（合并高分策略片段）
     - 局部RL微调（仅微调易退化参数）

3. **LeagueArena（联赛评估器）**
   - 运行 `challenger` vs `champion`、`challenger` vs 基线、`challenger` vs 各难度
   - 提供 Elo、胜率、方差、稳定性指标

4. **GateKeeper（晋升门禁）**
   - 先检查硬约束，再看收益
   - 必须通过：
     - 非法率=0
     - 延迟指标通过
     - 相比冠军在目标指标上显著提升（p<0.05 或置信区间下界>0）

5. **ReleaseManager（发布管理）**
   - Champion/Challenger 灰度：5% -> 25% -> 100%
   - 线上回滚触发：胜率突降、明显错误率上升、延迟超阈值

6. **MetaController（元策略控制器）**
   - 根据玩家分层（新手/普通/熟练/高手）选择对应策略分支
   - 保证用户体验目标胜率区间稳定

### 3.2 与现有代码映射

- 参数基因入口：`/Users/kalment/projects/tractor/cardgames/src/Core/AI/AIStrategyParameters.cs`
- 决策执行器：`/Users/kalment/projects/tractor/cardgames/src/Core/AI/AIPlayer.cs`
- 合法性约束：`/Users/kalment/projects/tractor/cardgames/src/Core/Rules/PlayValidator.cs`
- 建议新增目录：`/Users/kalment/projects/tractor/cardgames/src/Core/AI/Evolution/`

---

## 4. 进化算法设计

### 4.1 策略表示（Policy Genome）

将 `AIStrategyParameters` 视为连续向量：
- 随机性维度：Easy/Medium/Hard/Expert 随机率
- 先手维度：甩牌激进度、拖拉机优先级
- 跟牌维度：毙牌优先、保主倾向
- 分牌维度：保分/送分权重
- 收官维度：终局收束与稳定性

### 4.2 候选生成

每轮生成 `N=24` 个 challenger：
- `12` 个轻微变异（小步迭代，降风险）
- `8` 个中度变异（探索新策略）
- `4` 个重组候选（跨版本融合）

### 4.3 奖励函数（多目标）

```
R = w1 * WinRate
  + w2 * ScoreControl
  + w3 * PartnerCoordination
  + w4 * Diversity
  - w5 * BlunderRate
  - w6 * LatencyPenalty
  - w7 * PredictabilityPenalty
```

建议初始权重：
- `w1=0.35, w2=0.20, w3=0.10, w4=0.10, w5=0.15, w6=0.05, w7=0.05`

### 4.4 联赛机制

- 每个候选至少进行：
  - vs Champion：`1000` 局
  - vs Baseline：`500` 局
  - 难度梯度验证：`2000` 局
- 总局数约 `3500/候选`，24 候选约 `8.4万局/代`

---

## 5. 晋升规则（Promotion Contract）

候选策略晋升为 Champion 必须同时满足：

1. **硬约束全通过**
   - 非法出牌率 `=0`
   - 平均延迟 `<100ms`，P99 `<150ms`
   - 资源约束达标（内存/CPU/配置文件大小）

2. **收益显著**
   - 对当前 Champion 胜率提升 `>= +2%`
   - 且 95% 置信区间下界 `>0`

3. **无体验回退**
   - 人性化指标不低于 Champion
   - 策略多样性不下降超过 `5%`

4. **稳定性通过**
   - 重复评估三次，波动在容忍范围内

---

## 6. 自主进化安全机制

- **规则保险丝**：任何训练出的策略决策前都走 `PlayValidator/FollowValidator`
- **防作弊保险丝**：训练特征白名单只允许公开信息字段
- **回归保险丝**：保留最近 `K=5` 个冠军快照，支持一键回滚
- **漂移监控**：检测胜率、延迟、明显错误率漂移，触发自动降级
- **可解释审计**：记录关键决策分解（为什么选择这手牌）

---

## 7. 落地路线图（4阶段）

### Phase 1（1周）：可运行闭环
- 建立 `EvolutionRunner`，支持 generation 循环
- 打通日志读取、候选生成、批量评估
- 产出首版对比报告

### Phase 2（1-2周）：门禁与统计显著性
- 增加 GateKeeper 与晋升契约
- 引入置信区间/显著性检验
- 支持失败自动回滚

### Phase 3（2周）：线上灰度与玩家分层
- 接入 MetaController（按玩家段位/表现选择策略）
- 完成灰度发布与监控看板
- 校准“目标胜率区间”

### Phase 4（持续）：增强学习与人性化
- 加入局部强化学习微调
- 优化策略多样性与风格一致性
- 持续降低明显错误率

---

## 8. 最小可行实现（MVP）建议

优先做“参数进化版”，先不引入复杂深度学习：

1. 对 `AIStrategyParameters` 做变异和归一化
2. 用现有 AI 决策逻辑进行自博弈评估
3. 通过文档既有指标体系做门禁
4. 自动生成 `champion.json` 与报告

这样可以在当前架构下快速得到“可自主进化”的第一版，再逐步接入 RL。

---

## 9. 关键产物定义

- `policy_registry.json`：策略版本、父版本、生成方式、指标摘要
- `generation_report_*.md`：每代评估报告
- `promotion_log.md`：晋升/回滚历史
- `hard_cases.json`：关键局面集合（用于回归验证）

---

## 10. 验收标准（定义完成）

满足以下条件可判定“自主进化AI v1 可用”：

- 连续 `5` 代进化中至少 `2` 代成功晋升
- 无任何非法出牌回归
- 相对初始基线胜率提升 `>=8%`
- 人性化指标保持在目标区间（最优决策率 85%-95%）
- 线上灰度期间无重大性能告警

---

**文档版本**: v1.0  
**创建日期**: 2026-03-14  
**基线文档**: `/Users/kalment/projects/tractor/cardgames/doc/AI_Training_Best_Practices.md`
