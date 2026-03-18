# PPO AI 训练可视化方案 v1.0

## 1. 文档定位

本文档定义 `PPO AI` 第一版训练可视化方案。

本版采用：

- `TensorBoard` 负责训练过程曲线
- `Streamlit` 负责训练结果分析、专项指标与样本回放入口

本文档只回答以下问题：

1. 训练过程要记录哪些可视化指标
2. TensorBoard 和 Streamlit 分别承担什么职责
3. 训练日志、评估结果、样本回放文件如何组织
4. 第一版最小可实施范围是什么

本文档不负责描述：

1. PPO 网络结构
2. PPO 超参数
3. C# 训练引擎桥接协议细节
4. 共享输入字段语义与编码细节

这些内容以以下文档为准：

- [PPO训练架构设计_v1.0](./PPO训练架构设计_v1.0.md)
- [PPO AI_CSharp训练引擎桥接设计_v1.0](./PPO AI_CSharp训练引擎桥接设计_v1.0.md)
- [AI共享决策输入定义_v1.0](./AI共享决策输入定义_v1.0.md)
- [AI共享决策输入编码定义_v1.0](./AI共享决策输入编码定义_v1.0.md)

---

## 2. 方案结论

第一版训练可视化采用双层方案：

1. `TensorBoard`
   - 负责实时训练曲线
   - 负责多 run 对比
   - 负责 checkpoint 级评估标量展示
2. `Streamlit`
   - 负责训练业务总览
   - 负责拖拉机专项指标
   - 负责 checkpoint 对比
   - 负责样本回放入口与问题筛查

原因：

1. `TensorBoard` 接入最轻，适合训练内循环
2. `Streamlit` 更适合定制拖拉机专项页面
3. 两者组合可以同时覆盖“模型是否在学”和“牌是否打对了”

---

## 3. 总体目标

第一版可视化必须满足以下目标：

1. 能实时看到 PPO 是否在收敛
2. 能比较不同 run、不同 checkpoint 的效果
3. 能看到对 `RuleAI` 和弱基线的固定评估结果
4. 能看到拖拉机专项指标，而不只看 reward
5. 能快速跳转到代表性牌局样本做人工复盘

第一版不要求：

1. 浏览器内直接重放完整游戏 UI
2. 自动生成完整牌局动画
3. 自动用 LLM 分析所有失误
4. 多机训练集群统一监控

---

## 4. 职责分工

### 4.1 TensorBoard 职责

TensorBoard 负责记录和展示：

1. 训练过程标量
2. 评估阶段标量
3. 训练配置文本摘要
4. 关键图表快照

TensorBoard 不负责：

1. 大量表格筛选
2. 历史对局样本检索
3. 决策 bundle 逐局分析

### 4.2 Streamlit 职责

Streamlit 负责展示：

1. 训练 run 总览
2. checkpoint 排行
3. 拖拉机专项指标面板
4. 评估局结果表
5. 样本回放与日志入口
6. checkpoint 对比页

Streamlit 不负责：

1. 替代 TensorBoard 的实时训练曲线能力
2. 直接承担高频训练日志写入

---

## 5. 数据流设计

整体数据流如下：

```text
Python PPO Trainer
  ├─ 写 TensorBoard event files
  ├─ 写 train_metrics.jsonl
  ├─ 定期触发 fixed evaluation
  │    ├─ 写 eval_summary.csv
  │    ├─ 写 eval_match_results.jsonl
  │    └─ 写 eval_samples/
  └─ 保存 checkpoints/

TensorBoard
  └─ 读取 runs/<run_id>/tb/

Streamlit
  ├─ 读取 train_metrics.jsonl
  ├─ 读取 eval_summary.csv
  ├─ 读取 eval_match_results.jsonl
  ├─ 读取 eval_samples/
  └─ 读取 checkpoints/metadata.json
```

原则：

1. 训练写入与展示读取解耦
2. Streamlit 只读文件，不直接介入训练进程
3. 所有评估结果必须可落盘复用，不能只存在内存里

---

## 6. 指标体系

第一版可视化至少分 4 类指标。

### 6.1 训练基础指标

这些指标主要进入 TensorBoard。

| 指标 | 含义 | 作用 |
|---|---|---|
| `train/episode_reward_mean` | 平均 episode 奖励 | 看整体学习趋势 |
| `train/policy_loss` | policy loss | 看策略更新是否异常 |
| `train/value_loss` | value loss | 看 value 头是否发散 |
| `train/entropy` | 策略熵 | 看是否过早塌缩 |
| `train/kl_divergence` | KL 散度 | 看 PPO 更新步长是否过大 |
| `train/explained_variance` | value 拟合解释度 | 看 value 学得是否稳定 |
| `train/illegal_action_rate` | 非法动作率 | 规则正确性的第一报警项 |
| `train/action_mask_density` | 合法动作密度 | 看动作空间活跃度 |
| `train/steps_per_second` | 训练吞吐 | 便于定位性能问题 |

### 6.2 固定评估指标

这些指标同时进入 TensorBoard 和 Streamlit。

| 指标 | 含义 | 作用 |
|---|---|---|
| `eval_random/win_rate` | 对随机合法 AI 胜率 | 阶段 1 基础能力判断 |
| `eval_rule_ai/win_rate` | 对 RuleAI 胜率 | 核心效果指标 |
| `eval_rule_ai/avg_team_final_score` | PPO 队平均最终得分 | 看得分能力 |
| `eval_rule_ai/avg_level_gain` | PPO 队平均升级数 | 看终局收益 |
| `eval_rule_ai/avg_reward` | 平均终局奖励 | 和训练 reward 对齐 |
| `eval_rule_ai/match_count` | 评估局数 | 防止样本量不足误判 |

### 6.3 拖拉机专项指标

这些指标主要进入 Streamlit，也建议同步写入 TensorBoard。

| 指标 | 含义 | 作用 |
|---|---|---|
| `skill/bottom_protect_success_rate` | 庄家方保底成功率 | 评估保底策略 |
| `skill/bottom_capture_success_rate` | 闲家方抠底成功率 | 评估抠底策略 |
| `skill/high_score_trick_hold_rate` | 高分墩守住率 | 评估关键墩处理 |
| `skill/mate_support_rate` | 给队友送分/配合成功率 | 评估协作质量 |
| `skill/burn_high_trump_rate` | 非必要烧高主率 | 评估是否乱烧大牌 |
| `skill/cheap_win_selection_rate` | 该用小赢时是否选最便宜可赢牌 | 评估资源控制 |
| `skill/throw_success_rate` | 甩牌成功率 | 评估复杂首发能力 |
| `skill/stable_lead_conversion_rate` | 已建立优势后能否稳住胜墩 | 评估收口能力 |

说明：

1. 第一版不要求一次把所有专项指标全部做完
2. 第一版优先做能直接从终局和 trick 日志稳定提取的指标

### 6.4 运行与质量指标

| 指标 | 含义 | 作用 |
|---|---|---|
| `sys/train_loop_crash_count` | 训练过程异常次数 | 判断训练稳定性 |
| `sys/eval_timeout_count` | 评估超时次数 | 判断评估链路稳定性 |
| `sys/action_space_overflow_count` | 动作空间溢出次数 | 规则/动作空间一致性报警 |
| `sys/bridge_error_count` | C# bridge 错误次数 | 桥接健康度 |

---

## 7. TensorBoard 设计

### 7.1 目录结构

建议每个 run 使用独立目录：

```text
artifacts/ppo_runs/<run_id>/
  ├─ tb/
  ├─ checkpoints/
  ├─ train_metrics.jsonl
  ├─ eval_summary.csv
  ├─ eval_match_results.jsonl
  ├─ config.json
  └─ eval_samples/
```

### 7.2 run_id 规则

建议格式：

```text
ppo_YYYYMMDD_HHMMSS_<short_tag>
```

例如：

```text
ppo_20260318_210500_stage1_baseline
```

### 7.3 TensorBoard tag 规范

推荐统一前缀，避免后期混乱：

| 前缀 | 用途 |
|---|---|
| `train/` | 训练过程标量 |
| `eval_random/` | 对随机合法 AI 评估 |
| `eval_rule_ai/` | 对 RuleAI 评估 |
| `skill/` | 拖拉机专项指标 |
| `sys/` | 系统稳定性和性能指标 |
| `text/` | 配置摘要和说明 |

### 7.4 TensorBoard 页面目标

第一版至少要能在 TensorBoard 中直接看到：

1. 最近 1 个 run 的训练曲线
2. 多个 run 的胜率对比
3. 非法动作率与 reward 的联动
4. `RuleAI` 胜率曲线是否持续上升

### 7.5 文本摘要

每个 run 建议记录一段文本摘要：

1. run_id
2. git commit
3. 训练阶段
4. PPO 控制座位配置
5. 对手配置
6. 奖励公式版本
7. observation 版本
8. action space 版本

---

## 8. Streamlit 页面设计

第一版建议分为 5 个页面。

### 8.1 页面 1：训练总览

展示内容：

1. 当前 run 基本信息
2. 最新 checkpoint
3. 最佳 checkpoint
4. 最近一次评估时间
5. 对 RuleAI 最新胜率
6. 非法动作率
7. 当前 best model 路径

建议组件：

1. 顶部 KPI 卡片
2. 最近 20 次评估结果折线图
3. 最新 checkpoint 表格

### 8.2 页面 2：专项指标

展示内容：

1. 保底成功率趋势
2. 抠底成功率趋势
3. 高分墩守住率趋势
4. 烧高主率趋势
5. 送队友成功率趋势
6. 甩牌成功率趋势

建议组件：

1. 多指标折线图
2. checkpoint 排行表
3. 指标阈值高亮

### 8.3 页面 3：checkpoint 对比

用途：

1. 对比 `checkpoint A` 和 `checkpoint B`
2. 对比当前模型与 best 模型
3. 对比 PPO 与 RuleAI 基线

展示字段建议：

1. 胜率差值
2. 平均得分差值
3. 平均升级数差值
4. 非法动作率差值
5. 专项指标差值

### 8.4 页面 4：评估局列表

展示内容：

1. 每一局评估的 seed
2. checkpoint 编号
3. 对手类型
4. 最终胜负
5. 最终得分
6. 升级数
7. 是否发生保底/抠底
8. 日志与回放入口

筛选条件建议：

1. 按 checkpoint
2. 按对手类型
3. 按是否失败
4. 按是否发生抠底
5. 按是否存在非法动作

### 8.5 页面 5：样本回放入口

第一版不做完整内嵌回放 UI，但必须能方便打开以下文件：

1. replay markdown
2. decision bundle
3. 评估摘要 JSON
4. 关键 trick 提取结果

建议支持三类样本：

1. 最好的一局
2. 最差的一局
3. 与上一个 checkpoint 分歧最大的一局

---

## 9. 文件落点设计

建议统一落到：

```text
artifacts/ppo_runs/<run_id>/
```

具体文件如下：

### 9.1 `config.json`

记录：

1. run_id
2. 启动时间
3. git commit
4. 训练超参数
5. 奖励公式版本
6. 输入编码版本
7. 动作空间版本
8. bridge 版本

### 9.2 `train_metrics.jsonl`

每个训练迭代一行，建议字段：

1. `iteration`
2. `env_steps`
3. `episode_reward_mean`
4. `policy_loss`
5. `value_loss`
6. `entropy`
7. `kl_divergence`
8. `illegal_action_rate`
9. `action_mask_density`
10. `steps_per_second`
11. `timestamp_utc`

### 9.3 `eval_summary.csv`

每次固定评估一行，建议字段：

1. `iteration`
2. `checkpoint_id`
3. `opponent_type`
4. `match_count`
5. `win_rate`
6. `avg_team_final_score`
7. `avg_level_gain`
8. `avg_reward`
9. `bottom_protect_success_rate`
10. `bottom_capture_success_rate`
11. `high_score_trick_hold_rate`
12. `mate_support_rate`
13. `burn_high_trump_rate`
14. `timestamp_utc`

### 9.4 `eval_match_results.jsonl`

每局评估一行，建议字段：

1. `iteration`
2. `checkpoint_id`
3. `seed`
4. `opponent_type`
5. `ppo_seats`
6. `result`
7. `my_team_final_score`
8. `my_team_level_gain`
9. `defender_score`
10. `next_dealer`
11. `has_bottom_event`
12. `bottom_outcome`
13. `illegal_action_count`
14. `sample_paths`

### 9.5 `eval_samples/`

目录建议：

```text
eval_samples/
  └─ <checkpoint_id>/
      └─ seed_<seed>/
          ├─ summary.json
          ├─ replay.md
          ├─ decision_bundle.json
          └─ key_tricks.json
```

---

## 10. 固定评估策略

第一版建议固定评估，而不是完全依赖训练内 reward。

### 10.1 评估频率

建议：

1. 每 `N` 个 iteration 跑一次固定评估
2. `N` 在第一版可先取 `5` 或 `10`
3. 每次评估都使用固定 seeds 集合

### 10.2 对手池

第一版至少包含：

1. `随机合法出牌 AI`
2. `RuleAI V2.1`

### 10.3 评估局数

第一版建议：

1. 对随机合法 AI：`20` 局
2. 对 RuleAI：`50` 局

原因：

1. 随机基线波动小，可以少跑
2. RuleAI 对局更关键，需要更稳一点的样本量

### 10.4 种子策略

固定 seed 集合必须长期稳定，避免不同 checkpoint 不可横向比较。

要求：

1. 同一阶段默认使用同一组 seeds
2. 如果评估 seeds 变更，必须升级评估配置版本

---

## 11. 第一版最小实现清单

### 11.1 必做项

1. 训练过程写入 TensorBoard 标量
2. 每次评估输出 `eval_summary.csv`
3. 每次评估输出 `eval_match_results.jsonl`
4. 每次评估抽样保存代表局样本
5. Streamlit 能读取上述文件并展示总览页、专项页、对局列表页

### 11.2 可后置项

1. checkpoint 差分分析自动化
2. 样本优选逻辑更智能
3. 关键 trick 自动摘要
4. 失败对局自动聚类

---

## 12. 页面验收标准

第一版可视化完成后，至少应满足以下验收标准：

1. 训练过程中可以实时看到 TensorBoard 曲线
2. 能在 Streamlit 中看到最近一次对 RuleAI 的胜率
3. 能在 Streamlit 中筛出失败对局
4. 能从失败对局一键打开 replay 或 decision bundle
5. 能比较两个 checkpoint 的关键指标差异

---

## 13. 风险与约束

### 13.1 风险

1. 若训练日志字段频繁变化，Streamlit 页面容易漂移
2. 若专项指标定义不稳定，会导致历史 run 不可比
3. 若样本文件不规范，回放入口会失效

### 13.2 约束

1. 指标命名必须稳定
2. 评估脚本必须只追加数据，不要覆盖历史 run
3. 样本路径必须可从 `eval_match_results.jsonl` 直接定位

---

## 14. 训练进展解读指南

训练进展建议固定从 5 个维度观察，不要只看 `reward`。

### 14.1 维度一：训练稳定性

这一维度用于判断：

1. 训练是否正常进行
2. PPO 是否出现发散、塌缩或环境污染

重点指标：

| 指标 | 关注点 |
|---|---|
| `train/policy_loss` | 是否剧烈震荡或长期异常 |
| `train/value_loss` | 是否持续过高或突然发散 |
| `train/entropy` | 是否过早塌缩到极低 |
| `train/kl_divergence` | 是否更新步长过大 |
| `train/explained_variance` | value 是否逐步学到有效估计 |
| `train/illegal_action_rate` | 非法动作是否下降并稳定 |
| `sys/bridge_error_count` | C# bridge 是否稳定 |
| `sys/action_space_overflow_count` | 动作空间设计是否有溢出问题 |

解读原则：

1. 先保证训练稳定，再讨论策略提升
2. 若 `illegal_action_rate` 长期不降，优先查环境与动作空间，不要急着调奖励

### 14.2 维度二：实战结果

这一维度用于判断：

1. PPO 是否真的变强
2. 是否正在逼近 `RuleAI`

重点指标：

| 指标 | 关注点 |
|---|---|
| `eval_random/win_rate` | 是否已经明显超过随机合法 AI |
| `eval_rule_ai/win_rate` | 是否在逼近或超过阶段目标 |
| `eval_rule_ai/avg_team_final_score` | 是否具备更强得分能力 |
| `eval_rule_ai/avg_level_gain` | 是否体现在升级收益上 |
| `eval_rule_ai/avg_reward` | 是否与整体目标一致 |

解读原则：

1. `win_rate_vs_rule_ai` 是主指标
2. `avg_team_final_score` 和 `avg_level_gain` 用于解释胜率变化的原因

### 14.3 维度三：专项能力

这一维度用于判断 PPO 是否学到了拖拉机核心策略，而不只是局部碰运气。

重点指标：

| 指标 | 关注点 |
|---|---|
| `skill/bottom_protect_success_rate` | 庄家方是否会保底 |
| `skill/bottom_capture_success_rate` | 闲家方是否会争取抠底 |
| `skill/high_score_trick_hold_rate` | 高分墩能否守住 |
| `skill/mate_support_rate` | 是否会给队友送分或配合 |
| `skill/throw_success_rate` | 复杂首发能力是否在提升 |
| `skill/cheap_win_selection_rate` | 是否会选最便宜可赢牌 |
| `skill/burn_high_trump_rate` | 是否仍然乱烧高主 |

解读原则：

1. 胜率提升但专项能力恶化，不一定是好事
2. `burn_high_trump_rate` 是很重要的反向指标，越低通常越健康

### 14.4 维度四：行为质量

这一维度用于判断：

1. 决策是否越来越像“合理玩家”
2. 是否还有明显低级错误

这一维度不能只看标量，必须结合样本回放。

重点观察场景：

1. 该垫小牌时是否仍然垫大牌
2. 该用最便宜可赢牌时是否乱烧高牌
3. 该送分给队友时是否错过
4. 高分墩该保时是否放分
5. 有抠底机会时是否主动争取

建议每轮评估至少抽查：

1. 最好的一局
2. 最差的一局
3. 与上一个 checkpoint 分歧最大的一局

### 14.5 维度五：泛化能力

这一维度用于判断：

1. 模型是否只适应了固定评估 seeds
2. 是否在不同局型下都具备稳定表现

重点观察：

1. 固定 seeds 是否持续提升
2. 新 seeds 抽样是否也接近固定评估结果
3. 不同对手池下是否一致
4. 不同局型下是否存在明显短板

建议按局型拆看：

1. 庄家局
2. 闲家局
3. 强主局
4. 弱主局
5. 保底压力局
6. 抠底机会局

### 14.6 最小日看板

如果每天只看最关键的一组指标，建议优先看这 8 个：

1. `train/illegal_action_rate`
2. `eval_random/win_rate`
3. `eval_rule_ai/win_rate`
4. `eval_rule_ai/avg_team_final_score`
5. `eval_rule_ai/avg_level_gain`
6. `skill/bottom_protect_success_rate`
7. `skill/bottom_capture_success_rate`
8. `skill/burn_high_trump_rate`

### 14.7 典型健康信号

一个健康的训练过程通常表现为：

1. `illegal_action_rate` 先明显下降
2. `win_rate_vs_random` 先快速上升
3. `win_rate_vs_rule_ai` 再缓慢上升
4. 专项指标逐步改善
5. 回放中低级错误逐步减少

### 14.8 典型异常信号

以下情况需要重点警惕：

1. `reward` 上升，但 `win_rate_vs_rule_ai` 不涨
2. `win_rate` 上升，但 `burn_high_trump_rate` 恶化
3. `avg_team_final_score` 上升，但保底成功率下降
4. 固定 seeds 结果很好，但新 seeds 抽样明显变差

优先排查方向：

1. 奖励是否学歪
2. 评估 seeds 是否过拟合
3. 专项指标定义是否有误
4. 动作空间或 bridge 是否存在隐性问题

---

## 15. 推荐实施顺序

建议按以下顺序实施：

1. 先接入 TensorBoard 标量
2. 再补固定评估输出文件
3. 再做 Streamlit 训练总览页
4. 再做专项指标页
5. 最后补样本回放入口和 checkpoint 对比页

原因：

1. 先把数据产出来
2. 再把页面接上去
3. 避免先做页面但没有稳定数据源

---

## 16. 本版结论

`TensorBoard + Streamlit` 是当前项目第一版 `PPO AI` 训练可视化的最务实方案。

其中：

1. `TensorBoard` 解决“训练有没有在学”
2. `Streamlit` 解决“学出来的牌到底对不对”

第一版只要把：

1. 训练标量
2. 固定评估结果
3. 专项指标
4. 样本回放入口

这 4 块打通，就已经具备可用的训练可视化能力。
