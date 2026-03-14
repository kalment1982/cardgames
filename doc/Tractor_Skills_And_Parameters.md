# 拖拉机80分 - 完整技巧体系与参数映射

## 一、基础技巧（必须掌握）

### 1. 跟牌基本原则

#### 1.1 对家赢牌时
```
技巧：
- 优先送分牌（5/10/K）
- 垫小牌（3/4/6），保留大牌（A/K/Q）
- 避免垫主牌
- 避免垫对子和拖拉机

对应参数：
PartnerWinning_GivePointsPriority: 0.85      # 送分优先级
PartnerWinning_DiscardSmallPriority: 0.80    # 垫小牌优先级
PartnerWinning_AvoidTrumpPriority: 0.70      # 避免垫主牌
PartnerWinning_KeepPairsPriority: 0.60       # 保留对子
```

#### 1.2 对手赢牌时（能赢）
```
技巧：
- 用最小的能赢的牌（保留大牌控制下轮）
- 大起来后可以先手出牌
- 考虑下轮出牌权的价值

对应参数：
WinAttempt_UseMinimalCardsPriority: 0.75     # 用最小牌赢
WinAttempt_PreserveControlPriority: 0.70     # 保留控制力
WinAttempt_NextLeadValueWeight: 0.65         # 下轮出牌权价值
```

#### 1.3 对手赢牌时（无法赢）
```
技巧：
- 垫小牌（3/4/6）
- 避免送分牌
- 避免垫主牌
- 优先垫短门（保留长门）

对应参数：
CannotWin_DiscardSmallPriority: 0.85         # 垫小牌
CannotWin_AvoidPointsPriority: 0.80          # 避免送分
CannotWin_AvoidTrumpPriority: 0.75           # 避免垫主牌
CannotWin_PreserveLongSuitPriority: 0.60     # 保留长门
```

---

## 二、庄家技巧

### 2.1 扣底策略
```
技巧：
- 不扣分牌（5/10/K）
- 不扣主牌
- 优先扣短门小牌
- 扣底后要记住扣了什么

对应参数：
BuryPointRisk: 0.30                          # 扣分牌风险
BuryTrumpProtection: 0.80                    # 保护主牌
（已有参数，需要优化实现）
```

### 2.2 保底策略
```
技巧：
- 收官时（最后3-5墩）警觉保底
- 避免出短门（容易被抠底）
- 优先出主牌（相对安全）
- 队友也要帮忙保底

对应参数：
BottomProtection_Alertness: 0.70             # 保底警觉度
BottomProtection_AvoidShortSuitPriority: 0.75 # 避免出短门
BottomProtection_PreferTrumpPriority: 0.65   # 优先出主牌
BottomProtection_HelpPartnerPriority: 0.60   # 帮队友保底
```

### 2.3 庄家先手策略
```
技巧：
- 出大牌控制局面
- 清理短门（避免被抠底）
- 试探对手缺门
- 领先时出安全牌

对应参数：
Lead_BigCardControlPriority: 0.60            # 大牌控制
Lead_ClearShortSuitPriority: 0.55            # 清理短门
Lead_ProbeOpponentPriority: 0.50             # 试探缺门
Lead_LeadingSafetyBias: 0.65                 # 领先时保守
```

---

## 三、闲家技巧

### 3.1 毙牌策略
```
技巧：
- 缺门时用主牌毙牌
- 判断是否值得毙（看分牌多少）
- 毙牌后要能控制下轮

对应参数：
FollowTrumpCutPriority: 0.65                 # 毙牌优先级（已有）
WinAttempt_UseMinimalCardsPriority: 0.75     # 用最小主牌毙
```

### 3.2 抠底策略
```
技巧：
- 识别庄家短门
- 最后几墩出庄家短门
- 配合队友一起抠底
- 抠底成功翻倍得分

对应参数：
需要新增：
Defender_AttackBottomPriority: 0.70          # 抠底积极性
Defender_IdentifyShortSuitSkill: 0.65        # 识别短门能力
Defender_CoordinateAttackPriority: 0.60      # 配合队友
```

### 3.3 闲家配合
```
技巧：
- 队友出牌时判断意图
- 不要互相抢牌
- 一人控制，一人送分
- 记住队友出过的牌

对应参数：
PartnerSupportPointBias: 0.75                # 支持队友（已有）
需要新增：
Defender_AvoidCompetePriority: 0.70          # 避免抢牌
Defender_RoleCoordinationSkill: 0.65         # 角色协调
```

---

## 四、高级技巧

### 4.1 记牌
```
技巧：
- 记住已出的分牌
- 记住已出的大牌（A/K）
- 推断对手缺门
- 推断对手手牌结构

对应参数：
（已有CardMemory系统，需要增强）
需要新增：
Memory_TrackPointCardsPriority: 0.80         # 记分牌
Memory_TrackBigCardsPriority: 0.75           # 记大牌
Memory_InferShortSuitSkill: 0.70             # 推断缺门
```

### 4.2 甩牌
```
技巧：
- 评估甩牌成功率
- 甩牌时机选择
- 甩牌失败的代价
- 领先时少甩，落后时多甩

对应参数：
LeadThrowAggressiveness: 0.50                # 甩牌激进度（已有）
LeadThrowMinAdvantage: 1                     # 最小优势（已有）
需要新增：
Throw_SuccessRateThreshold: 0.60             # 成功率阈值
Throw_LeadingConservativeBias: 0.70          # 领先时保守
```

### 4.3 局势判断
```
技巧：
- 判断当前领先/落后
- 根据局势调整策略
- 领先时保守，落后时激进
- 收官时精确计算分数

对应参数：
需要新增：
Situation_ScoreAwareness: 0.75               # 分数意识
Situation_LeadingConservativeBias: 0.70      # 领先保守
Situation_TrailingAggressiveBias: 0.75       # 落后激进
Situation_EndgameCalculationSkill: 0.80      # 收官计算
```

### 4.4 对子和拖拉机
```
技巧：
- 保留对子和拖拉机
- 对子比单张更有价值
- 拖拉机可以控制局面
- 不要轻易拆对子

对应参数：
PartnerWinning_KeepPairsPriority: 0.60       # 保留对子（已有）
LeadTractorPriority: 0.70                    # 拖拉机优先（已有）
需要新增：
Structure_PairValueWeight: 0.75              # 对子价值权重
Structure_TractorValueWeight: 0.85           # 拖拉机价值权重
Structure_AvoidBreakPairPriority: 0.70       # 避免拆对子
```

---

## 五、参数总结

### 已实现参数（v2新增20个）
```yaml
# 对家赢牌（4个）
PartnerWinning_GivePointsPriority: 0.85
PartnerWinning_DiscardSmallPriority: 0.80
PartnerWinning_AvoidTrumpPriority: 0.70
PartnerWinning_KeepPairsPriority: 0.60

# 争胜策略（4个）
WinAttempt_UseMinimalCardsPriority: 0.75
WinAttempt_PreserveControlPriority: 0.70
WinAttempt_NextLeadValueWeight: 0.65
WinAttempt_LeadingConservativeBias: 0.55

# 无法赢时（4个）
CannotWin_DiscardSmallPriority: 0.85
CannotWin_AvoidPointsPriority: 0.80
CannotWin_AvoidTrumpPriority: 0.75
CannotWin_PreserveLongSuitPriority: 0.60

# 保底策略（4个）
BottomProtection_Alertness: 0.70
BottomProtection_AvoidShortSuitPriority: 0.75
BottomProtection_PreferTrumpPriority: 0.65
BottomProtection_HelpPartnerPriority: 0.60

# 先手策略（4个）
Lead_BigCardControlPriority: 0.60
Lead_ClearShortSuitPriority: 0.55
Lead_ProbeOpponentPriority: 0.50
Lead_LeadingSafetyBias: 0.65
```

### 建议新增参数（15个）

```yaml
# 闲家抠底（3个）
Defender_AttackBottomPriority: 0.70
Defender_IdentifyShortSuitSkill: 0.65
Defender_CoordinateAttackPriority: 0.60

# 闲家配合（2个）
Defender_AvoidCompetePriority: 0.70
Defender_RoleCoordinationSkill: 0.65

# 记牌能力（3个）
Memory_TrackPointCardsPriority: 0.80
Memory_TrackBigCardsPriority: 0.75
Memory_InferShortSuitSkill: 0.70

# 甩牌优化（2个）
Throw_SuccessRateThreshold: 0.60
Throw_LeadingConservativeBias: 0.70

# 局势判断（4个）
Situation_ScoreAwareness: 0.75
Situation_LeadingConservativeBias: 0.70
Situation_TrailingAggressiveBias: 0.75
Situation_EndgameCalculationSkill: 0.80

# 牌型结构（3个）
Structure_PairValueWeight: 0.75
Structure_TractorValueWeight: 0.85
Structure_AvoidBreakPairPriority: 0.70
```

---

## 六、参数训练优先级

### P0（立即训练）- 解决"弱智"问题
```
已实现的20个v2参数：
- 对家赢牌垫牌策略（4个）
- 争胜用最小牌（4个）
- 无法赢时垫牌（4个）
- 保底策略（4个）
- 先手策略（4个）

预期效果：
- Defender胜率：40% → 48%
- 整体胜率：55% → 58%
- 消除明显的"弱智"行为
```

### P1（1周内）- 闲家强化
```
新增闲家参数（5个）：
- 抠底策略（3个）
- 配合策略（2个）

预期效果：
- Defender胜率：48% → 52%
- 整体胜率：58% → 60%
```

### P2（2周内）- 高级技巧
```
新增高级参数（10个）：
- 记牌能力（3个）
- 甩牌优化（2个）
- 局势判断（4个）
- 牌型结构（3个）

预期效果：
- Defender胜率：52% → 55%
- 整体胜率：60% → 63%
- 接近人类高手水平
```

---

## 七、训练方案

### 方案A：参数进化（推荐）
```
1. 使用已实现的20个v2参数
2. 运行Generation 24（200局）
3. 对比Champion v19
4. 预计Defender提升8%

优点：
- 立即见效（今天）
- 不需要改架构
- 可以持续迭代

缺点：
- 仍依赖人工规则
- 上限受限
```

### 方案B：强化学习（长期）
```
1. 保留参数作为特征
2. 训练神经网络学习权重
3. 自动发现最优策略

优点：
- 理论上限更高
- 可以发现新策略

缺点：
- 需要2-3周
- 需要大量数据
```

---

## 八、实施建议

**今天（立即）**：
1. ✅ 已实现20个v2参数
2. ✅ 已修改AIPlayer.cs使用这些参数
3. 🔄 重新编译并测试

**明天**：
1. 运行Generation 24训练
2. 验证Defender胜率提升
3. 如果有效，继续训练10代

**下周**：
1. 新增15个高级参数
2. 继续参数进化
3. 目标：Defender 52%，整体60%

---

**参考来源**：
- [升级免费版 - App Store](https://apps.apple.com/us/app/%E5%8D%87%E7%BA%A7%E5%85%8D%E8%B4%B9%E7%89%88/id517442234)
- [双升80分棋牌游戏合集 - App Store](https://apps.apple.com/gb/app/%E5%8D%87%E7%BA%A7-%E6%8B%96%E6%8B%89%E6%9C%BA-%E5%8F%8C%E5%8D%8780%E5%88%86%E6%A3%8B%E7%89%8C%E6%B8%B8%E6%88%8F%E5%90%88%E9%9B%86/id1445654696)
- 基于拖拉机游戏常识和实战经验总结
