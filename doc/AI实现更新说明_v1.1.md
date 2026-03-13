# AI实现更新说明 v1.1

## 更新日期
2026-03-12

## 更新概述
根据《AI策略设计文档 v1.1》的要求，对AIPlayer类进行了全面升级，增加了角色感知、难度等级、对家配合等高级功能。

## 主要改进

### 1. 新增枚举类型

#### AIRole（AI角色）
```csharp
public enum AIRole
{
    Dealer,          // 坐庄玩家（拥有特权）
    DealerPartner,   // 庄家队友
    Opponent         // 闲家
}
```

#### AIDifficulty（AI难度）
```csharp
public enum AIDifficulty
{
    Easy = 1,      // 简单（随机性30-40%）
    Medium = 3,    // 中等（随机性15-25%）
    Hard = 6,      // 困难（随机性5-10%）
    Expert = 9     // 专家（随机性0-5%）
}
```

### 2. 构造函数更新

**旧版本**：
```csharp
public AIPlayer(GameConfig config, int seed = 0)
```

**新版本**：
```csharp
public AIPlayer(GameConfig config, AIDifficulty difficulty = AIDifficulty.Medium, int seed = 0)
```

### 3. 方法签名更新

#### Lead方法（首家出牌）

**旧版本**：
```csharp
public List<Card> Lead(List<Card> hand)
```

**新版本**：
```csharp
public List<Card> Lead(List<Card> hand, AIRole role = AIRole.Opponent)
```

**改进点**：
- 增加角色参数，根据角色调整出牌策略
- 庄家方：优先控制（拖拉机/对子）
- 闲家方：优先多张（甩牌）
- 简单难度会使用随机决策

#### Follow方法（跟牌）

**旧版本**：
```csharp
public List<Card> Follow(List<Card> hand, List<Card> leadCards)
```

**新版本**：
```csharp
public List<Card> Follow(List<Card> hand, List<Card> leadCards, AIRole role = AIRole.Opponent,
    int currentWinnerPosition = -1, int myPosition = -1, int partnerPosition = -1)
```

**改进点**：
- 增加角色参数
- 增加位置参数，用于判断对家是否赢牌
- 对家赢牌时：优先送分牌
- 对手赢牌时：尝试用大牌争胜
- 缺门时根据局势选择毙牌或垫牌

#### BuryBottom方法（扣底）

**旧版本**：
```csharp
public List<Card> BuryBottom(List<Card> hand)  // 接受任意数量的牌
```

**新版本**：
```csharp
public List<Card> BuryBottom(List<Card> hand)  // 必须是33张牌
```

**改进点**：
- 严格验证输入必须是33张牌（25张手牌 + 8张底牌）
- 智能评估每张牌的埋底价值
- 优先埋小副牌，避免埋分牌和主牌
- 考虑短门优势（2-3张的花色优先埋）
- 对子比单张更不想埋

### 4. 新增私有方法

#### GetRandomnessRate()
根据难度等级返回随机决策概率：
- Easy: 35%
- Medium: 20%
- Hard: 7.5%
- Expert: 2.5%

#### ShouldUseRandomDecision()
判断当前是否应该使用随机决策

#### GetCardPoints(Card card)
获取牌的分值（K=10, 10=10, 5=5）

#### EvaluateCardForBurying(Card card, List<Card> hand, CardComparer comparer)
评估牌的埋底价值，分数越低越适合埋：
- 分牌惩罚：+1000 + 分值×100
- 主牌惩罚：+500
- 对子惩罚：+200
- 短门优先：-100
- 牌力考虑：+牌值/10

### 5. 策略改进

#### 首家出牌策略
- **庄家方**：优先牌型（拖拉机>对子>单张），其次多张
- **闲家方**：优先多张（甩牌），其次牌型
- 根据难度调整随机性

#### 跟牌策略
- **对家赢牌时**：
  - 优先送分牌
  - 出小牌保留实力

- **对手赢牌时**：
  - 有大牌：争取赢回
  - 无大牌：庄家保留实力，闲家避免送分

- **缺门时**：
  - 对家赢牌：送分牌
  - 对手赢牌：用主牌毙牌或送垃圾牌

#### 埋底策略
- 智能评估每张牌的价值
- 优先级：小副牌 > 短门副牌 > 对子小副牌 > 分牌/主牌
- 考虑花色平衡和抠底风险

## 测试更新

新增测试用例：
1. `BuryBottom_Returns8Cards_From33Cards` - 验证33张输入
2. `BuryBottom_AvoidsPointCards` - 验证避免埋分牌
3. `Follow_PartnerWinning_SendsPointCards` - 验证对家配合
4. `DifficultyEasy_UsesMoreRandomness` - 验证难度系统

更新现有测试：
- 所有测试方法都更新了方法签名
- 增加了角色参数
- 增加了难度参数

## 兼容性说明

### 破坏性变更
1. `AIPlayer`构造函数增加了`difficulty`参数（有默认值，向后兼容）
2. `Lead`方法增加了`role`参数（有默认值，向后兼容）
3. `Follow`方法增加了多个参数（有默认值，向后兼容）
4. `BuryBottom`方法现在严格要求33张牌（破坏性变更）

### 迁移指南

**旧代码**：
```csharp
var ai = new AIPlayer(config, seed);
var leadCards = ai.Lead(hand);
var followCards = ai.Follow(hand, leadCards);
var buryCards = ai.BuryBottom(hand);
```

**新代码（最小改动）**：
```csharp
var ai = new AIPlayer(config, AIDifficulty.Medium, seed);
var leadCards = ai.Lead(hand);  // 使用默认角色
var followCards = ai.Follow(hand, leadCards);  // 使用默认参数
var buryCards = ai.BuryBottom(hand);  // 确保hand有33张牌
```

**新代码（完整功能）**：
```csharp
var ai = new AIPlayer(config, AIDifficulty.Hard, seed);

// 首家出牌（闲家）
var leadCards = ai.Lead(hand, AIRole.Opponent);

// 跟牌（闲家，对家赢牌）
var followCards = ai.Follow(hand, leadCards, AIRole.Opponent,
    currentWinnerPosition: 3, myPosition: 1, partnerPosition: 3);

// 扣底（坐庄玩家，33张牌）
var buryCards = ai.BuryBottom(handWith33Cards);
```

## 性能考虑

1. **决策时间**：所有决策都在1.5秒内完成（符合规则要求）
2. **随机性**：根据难度动态调整，不影响性能
3. **评估算法**：埋底评估为O(n)，n=33，性能优秀

## 后续优化方向

1. **亮主决策**：增加发牌阶段的亮主/反主策略
2. **记牌系统**：实现中高难度的记牌功能
3. **多步预判**：专家难度增加多步预判
4. **机器学习**：使用强化学习训练更智能的AI

## 版本历史

- v1.0 (2026-03-11): 初始AI实现
- v1.1 (2026-03-12): 增加角色感知、难度等级、对家配合等功能
