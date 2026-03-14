# 拖拉机自主进化AI设计方案 v1.1

## 1. 目标与边界

本方案在 `/Users/kalment/projects/tractor/cardgames/doc/AI_Training_Best_Practices.md` 的约束下扩展“自主进化”能力，并吸收 v1.0 评审意见做结构化修订。

- 保留硬约束：`非法出牌率=0`、`平均决策<100ms`、`P99<150ms`、`AI不可读取非公开信息`
- 持续优化软目标：胜率梯度、策略多样性、人性化程度
- 新增进化目标：模型可在无人值守下完成“采样 -> 训练 -> 验证 -> 晋升/回滚”闭环

> 设计原则：任何进化都不能突破规则合法性、公平性和性能预算。

---

## 2. 自主进化闭环（EvoLoop）

```
线上/离线对局数据
    -> 数据治理（筛选/去重/标注/分桶）
    -> 候选策略生成（变异 + 重组 + 可选局部RL）
    -> 分层评估（快速筛选 -> 精细验证 -> 晋升验证）
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
   - 核心能力：
     - 去重与异常局过滤（局面哈希 + 规则一致性检查）
     - 分层采样（按难度/角色/牌型覆盖）
     - 新鲜度窗口（最近 N 天权重更高）
     - 标签一致性（schema version + 字段完整率）
     - 泄漏防护（仅允许公开信息特征）

2. **PolicyFactory（候选生成器）**
   - 输入：当前冠军策略 `champion`
   - 输出：N 个候选 `challenger_i`
   - 生成方式：
     - 参数变异（对 `AIStrategyParameters` 做高斯扰动）
     - 组合交叉（合并高分策略片段）
     - 局部RL微调（仅在 Phase 4 启用）

3. **LeagueArena（联赛评估器）**
   - 运行 `challenger` vs `champion`、`challenger` vs 基线、`challenger` vs 各难度
   - 提供胜率、Elo、方差、稳定性指标
   - 支持并行评估与早停淘汰

4. **GateKeeper（晋升门禁）**
   - 先检查硬约束，再看收益
   - 支持动态晋升阈值（随系统成熟度提高）

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

每轮默认生成 `N=24` 个 challenger：
- `12` 个轻微变异（小步迭代，低风险）
- `8` 个中度变异（探索新策略）
- `4` 个重组候选（跨版本融合）

### 4.3 目标函数分阶段策略（替代固定7权重）

为降低早期复杂度，采用“先单目标、后多目标”的渐进方式：

1. **Stage A（MVP）**：单目标优化 `WinRate`
   - 硬约束不进奖励函数，直接作为门禁
   - 任何违反约束的候选直接淘汰

2. **Stage B（稳定期）**：主目标 `WinRate` + 轻量惩罚
   - 增加 `BlunderRate`、`LatencyPenalty` 两项惩罚
   - 权重固定为小值，仅做微调

3. **Stage C（高级）**：多目标优化（可选）
   - 再引入 `Diversity`、`PartnerCoordination`、`Predictability`
   - 这些指标先作为后验报告项，稳定后再进入训练目标

> 说明：`PartnerCoordination`、`Diversity`、`Predictability` 在 v1.1 中先定义为评估指标，不作为 Phase 1 的优化变量。

### 4.4 分层评估机制（解决训练耗时矛盾）

每代评估采用三层漏斗：

1. **Layer 0：规则与性能快检**
   - 每候选：`200` 个随机局面合法性检查 + `200` 次决策延迟抽样
   - 目的：立即淘汰非法或明显超时策略

2. **Layer 1：快速筛选**
   - 每候选：`500` 局
   - 24 候选并行评估后，保留前 `6` 名

3. **Layer 2：精细评估**
   - 前 6 名：各 `1500` 局（vs champion + vs baseline）
   - 保留前 `2` 名进入晋升赛

4. **Layer 3：晋升验证**
   - 前 2 名：各 `3500` 局 + `3` 个不同 seed 重复
   - 做置信区间与稳定性分析

该机制将“全量3500局”从 24 个候选减少到前 2 个候选，显著缩短代际时长。

### 4.5 吞吐预算公式（用于容量规划）

评估总时长近似：

```
TotalSeconds = Games * AvgDecisionsPerGame * AvgDecisionMs / (1000 * Workers)
```

经验值：
- `AvgDecisionsPerGame ~ 100`
- 如果 `AvgDecisionMs=20` 且 `Workers=32`，单代（分层评估）通常在 1-3 小时完成
- 若无并行，仅单机串行，时长会放大到天级

---

## 5. 晋升规则（Promotion Contract v1.1）

候选策略晋升为 Champion 必须满足以下条件：

1. **硬约束全通过**
   - 非法出牌率 `=0`
   - 平均延迟 `<100ms`，P99 `<150ms`
   - 资源约束达标（内存/CPU/配置文件大小）

2. **动态收益门槛（按阶段）**
   - **Bootstrapping（前5代）**：
     - 胜率提升 `>= +1.0%`，或
     - 95% 置信区间下界 `> -0.5%`
   - **Growth（第6代后）**：
     - 胜率提升 `>= +1.5%`
     - 且 95% 置信区间下界 `> 0`
   - **Mature（Elo稳定后）**：
     - 胜率提升 `>= +0.5%` 即可进入复核

3. **平局晋升机制（防停滞）**
   - 胜率持平（`-0.5% ~ +0.5%`）但满足以下任一：
     - 明显错误率下降 `>=10%`
     - P99 延迟下降 `>=10%`
     - 策略多样性提升 `>=8%`
   - 可晋升为 Champion（标记为“平局晋升”）

4. **稳定性通过**
   - 至少 3 次重复评估，且波动在阈值内

5. **停滞解锁**
   - 连续 `8` 代无晋升时：
     - 提高探索强度（变异方差上调）
     - 暂时放宽收益门槛到 Bootstrapping 档一代

---

## 6. 冷启动与课程学习（新增）

### 6.1 Phase 0：初始 Champion 建立

在自主进化前，先构建 `champion_seed`：
- 以当前最强 Hard/Expert 参数作为种子
- 通过 2000 局基线联赛选出首个 Champion
- 固化为 `champion_v0.json`

### 6.2 课程学习（Curriculum）

- 代 1-3：主要对手为 Easy/Medium（快速稳定基础策略）
- 代 4-8：混入 Hard 对手（提高对抗强度）
- 代 9+：以 Hard/Expert 为主（逼近高水平）

### 6.3 多分支 Champion

避免单一冠军覆盖所有用户：
- `champion_easy.json`
- `champion_medium.json`
- `champion_hard.json`
- `champion_expert.json`

由 `MetaController` 根据玩家段位与近期表现路由到对应分支。

---

## 7. 自主进化安全机制

- **规则保险丝**：任何训练出的策略决策前都走 `PlayValidator/FollowValidator`
- **防作弊保险丝**：训练特征白名单只允许公开信息字段
- **回归保险丝**：保留最近 `K=5` 个冠军快照，支持一键回滚
- **漂移监控**：检测胜率、延迟、明显错误率漂移，触发自动降级
- **可解释审计**：记录关键决策分解（为什么选择这手牌）

---

## 8. 落地路线图（修订）

### Phase 0（3-5天）：冷启动
- 建立初始 Champion（v0）
- 完成课程学习赛程配置
- 准备多分支策略文件

### Phase 1（1周）：可运行闭环
- 建立 `EvolutionRunner`，支持 generation 循环
- 打通日志读取、候选生成、分层评估
- 产出首版对比报告

### Phase 2（1-2周）：门禁与统计显著性
- 增加 GateKeeper 与动态晋升契约
- 引入置信区间/显著性检验
- 支持失败自动回滚

### Phase 3（2周）：线上灰度与玩家分层
- 接入 MetaController（按玩家段位/表现选择策略）
- 完成灰度发布与监控看板
- 校准“目标胜率区间”

### Phase 4（持续）：增强学习与人性化
- 加入局部强化学习微调
- 将多样性/协同指标从后验指标逐步升级为优化目标
- 持续降低明显错误率

---

## 9. 最小可行实现（MVP）建议

优先做“参数进化版”，先不引入复杂深度学习：

1. 对 `AIStrategyParameters` 做变异和归一化
2. 用现有 AI 决策逻辑进行自博弈评估
3. 使用“分层评估 + 动态门槛”晋升
4. 自动生成 `champion.json` 与报告

这样可以在当前架构下快速得到“可自主进化”的第一版，再逐步接入 RL。

---

## 10. 关键产物定义

- `policy_registry.json`：策略版本、父版本、生成方式、指标摘要
- `generation_report_*.md`：每代评估报告
- `promotion_log.md`：晋升/回滚历史
- `hard_cases.json`：关键局面集合（用于回归验证）
- `data_quality_report_*.md`：每代数据质量与采样覆盖报告

---

## 11. 验收标准（定义完成）

满足以下条件可判定“自主进化AI v1.1 可用”：

- 连续 `5` 代进化中至少 `2` 代成功晋升
- 无任何非法出牌回归
- 相对初始基线胜率提升 `>=8%`
- 人性化指标保持在目标区间（最优决策率 85%-95%）
- 单代评估在工程预算内（默认 1-3 小时，依并行资源而定）
- 线上灰度期间无重大性能告警

---

## 12. 本版修订点对照

- 修订 1：加入“分层评估 + 并行 + 吞吐公式”，解决 8.4 万局/代耗时矛盾
- 修订 2：将固定 7 权重改为分阶段目标函数，先单目标再多目标
- 修订 3：晋升条件改为动态门槛，并新增平局晋升与停滞解锁
- 修订 4：新增 Phase 0 冷启动、课程学习与多分支 Champion
- 修订 5：补全 DataEngine 关键细节（采样覆盖、数据新鲜度、schema、泄漏防护）

---

**文档版本**: v1.1  
**创建日期**: 2026-03-14  
**基线文档**: `/Users/kalment/projects/tractor/cardgames/doc/AI_Training_Best_Practices.md`  
**上一版本**: `/Users/kalment/projects/tractor/cardgames/doc/自主进化AI设计方案_v1.0.md`
