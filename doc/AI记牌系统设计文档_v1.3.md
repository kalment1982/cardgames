# AI记牌系统与智能甩牌设计文档 v1.3

## 更新日期
2026-03-13

## 概述
实现了AI记牌系统（CardMemory），使AI能够记录已出的牌、推断对手手牌情况，并基于记牌信息做出智能的甩牌决策。

## 核心功能

### 1. CardMemory 类

#### 1.1 记录已出的牌
```csharp
public void RecordTrick(List<TrickPlay> plays)
```

**功能**：
- 记录每墩牌中所有玩家出的牌
- 统计每种牌已出的数量
- 识别玩家的缺门情况

**记录内容**：
- `_playedCards`: 每种牌已出的数量
- `_playerVoidSuits`: 每个玩家缺哪些花色

**缺门判断逻辑**：
- 如果跟牌者没有跟首引花色，记录为缺门
- 区分缺副牌花色和缺主牌

#### 1.2 查询牌的数量
```csharp
public int GetPlayedCount(Card card)      // 已出数量
public int GetRemainingCount(Card card)   // 剩余数量
```

**用途**：
- 计算某张牌还剩多少张在外面
- 评估大牌被对手持有的概率

#### 1.3 查询缺门信息
```csharp
public bool IsPlayerVoid(int playerPosition, Suit suit)      // 是否缺某花色
public bool IsPlayerVoidTrump(int playerPosition)            // 是否缺主牌
```

**用途**：
- 判断对手是否能跟某个花色
- 评估甩牌被压制的风险

#### 1.4 评估甩牌成功概率
```csharp
public double EvaluateThrowSuccessProbability(
    List<Card> throwCards,           // 要甩的牌
    List<Card> hand,                 // 当前手牌
    int myPosition,                  // 我的位置
    List<int> opponentPositions)     // 对手位置列表
```

**核心算法**：

1. **牌型分析**
   - 将甩牌分解为：拖拉机、对子、单张
   - 对每个牌型组分别评估风险

2. **风险评估**
   - 计算比最大牌更大的牌还剩多少张
   - 检查对手是否缺门
   - 估算对手持有大牌的概率

3. **概率计算**
   ```
   总成功概率 = ∏(1 - 每个牌型组的风险)
   ```

**风险模型**：
```csharp
// 简化概率模型
double riskPerOpponent = Math.Min(0.8, biggerCardsRemaining * 0.15);
double totalRisk = 1.0 - Math.Pow(1.0 - riskPerOpponent, opponentsWithSuit);
```

**考虑因素**：
- 剩余大牌数量
- 对手缺门情况
- 对手数量
- 牌型结构（拖拉机 > 对子 > 单张）

### 2. AIPlayer 集成

#### 2.1 新增方法

```csharp
// 记录一墩牌
public void RecordTrick(List<TrickPlay> plays)

// 重置记牌（新局开始）
public void ResetMemory()
```

#### 2.2 Lead 方法更新

**新签名**：
```csharp
public List<Card> Lead(List<Card> hand, AIRole role = AIRole.Opponent,
    int myPosition = -1, List<int> opponentPositions = null)
```

**新增参数**：
- `myPosition`: 我的位置（用于记牌评估）
- `opponentPositions`: 对手位置列表（用于甩牌评估）

#### 2.3 智能甩牌决策

```csharp
private bool CanSafelyThrow(List<Card> throwCards, List<Card> hand,
    int myPosition, List<int> opponentPositions)
```

**决策流程**：
1. 简单难度：随机决定（50%概率）
2. 没有对手信息：保守策略（只允许甩2张以下）
3. 有对手信息：使用记牌系统评估

**成功率阈值**（根据难度）：
- Medium: 60%
- Hard: 70%
- Expert: 80%

**甩牌策略**：
```csharp
if (sorted.Count >= 3)
{
    bool canThrow = CanSafelyThrow(sorted, hand, myPosition, opponentPositions);

    if (canThrow)
    {
        // 甩牌成功率高，可以尝试
        candidates.Add(sorted);
    }
    else if (!isDealerSide && sorted.Count >= 5)
    {
        // 闲家且牌很多，即使风险也可以尝试（激进策略）
        candidates.Add(sorted);
    }
}
```

## 使用示例

### 示例1：基本使用

```csharp
var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
var ai = new AIPlayer(config, AIDifficulty.Hard);

// 新局开始
ai.ResetMemory();

// 每墩牌后记录
var plays = new List<TrickPlay>
{
    new TrickPlay(0, new List<Card> { new Card(Suit.Spade, Rank.Ace) }),
    new TrickPlay(1, new List<Card> { new Card(Suit.Spade, Rank.King) }),
    new TrickPlay(2, new List<Card> { new Card(Suit.Spade, Rank.Queen) }),
    new TrickPlay(3, new List<Card> { new Card(Suit.Heart, Rank.Two) }) // 缺黑桃
};
ai.RecordTrick(plays);

// 首家出牌（提供位置信息）
var hand = GetMyHand();
var leadCards = ai.Lead(hand, AIRole.Opponent,
    myPosition: 0,
    opponentPositions: new List<int> { 1, 3 });
```

### 示例2：完整牌局流程

```csharp
// 初始化
var ai = new AIPlayer(config, AIDifficulty.Expert);
ai.ResetMemory();

// 第1墩
var trick1 = PlayTrick();
ai.RecordTrick(trick1);

// 第2墩
var trick2 = PlayTrick();
ai.RecordTrick(trick2);

// 第3墩 - AI首家出牌
var myHand = GetMyHand();
var opponentPositions = new List<int> { 1, 3 }; // 对手在位置1和3

var leadCards = ai.Lead(myHand, AIRole.Opponent,
    myPosition: 0,
    opponentPositions: opponentPositions);

// AI会基于前两墩的记牌信息，智能决定是否甩牌
```

## 难度差异

### Easy（简单）
- **不记牌**：RecordTrick 直接返回
- **甩牌决策**：随机（50%概率）
- **特点**：快速决策，不消耗计算资源

### Medium（中等）
- **记牌**：记录所有出牌和缺门
- **甩牌阈值**：60%成功率
- **特点**：基础记牌，保守甩牌

### Hard（困难）
- **记牌**：完整记牌系统
- **甩牌阈值**：70%成功率
- **特点**：精确记牌，谨慎甩牌

### Expert（专家）
- **记牌**：完整记牌系统
- **甩牌阈值**：80%成功率
- **特点**：完美记牌，极度谨慎甩牌

## 性能考虑

### 时间复杂度
- `RecordTrick`: O(n)，n为出牌数量
- `EvaluateThrowSuccessProbability`: O(m × k)
  - m: 甩牌中的牌型组数量
  - k: 需要检查的牌的种类数量
- 总体决策时间：< 100ms（远低于1.5秒要求）

### 空间复杂度
- `_playedCards`: O(108)，最多108种牌
- `_playerVoidSuits`: O(4 × 5)，4个玩家×最多5个花色
- 总内存占用：< 10KB

## 算法优化

### 1. 缺门快速判断
```csharp
if (opponentsWithSuit == 0)
    return 0; // 所有对手都缺门，安全
```

### 2. 大牌数量缓存
- 只计算比最大牌更大的牌
- 避免遍历所有可能的牌

### 3. 概率简化模型
- 使用简化的概率估算
- 避免复杂的组合计算

## 测试用例

### 新增测试

1. **RecordTrick_TracksPlayedCards**
   - 验证记牌功能正常工作

2. **Lead_EvaluatesThrowSafety_WithMemory**
   - 验证基于记牌的甩牌决策

### 测试覆盖
- 记牌功能
- 缺门识别
- 甩牌评估
- 不同难度的行为差异

## 后续优化方向

### 1. 高级记牌
- 记录对子和拖拉机的出现
- 推断对手的牌型分布
- 估算对手的牌力

### 2. 动态阈值
- 根据局势调整甩牌阈值
- 领先时保守，落后时激进

### 3. 对手建模
- 记录对手的出牌习惯
- 预测对手的决策

### 4. 多步预判
- 评估甩牌后的后续局势
- 考虑甩牌失败的代价

## API变更总结

### 新增类
- `CardMemory`: 记牌系统

### AIPlayer 新增方法
- `RecordTrick(List<TrickPlay> plays)`: 记录一墩牌
- `ResetMemory()`: 重置记牌

### Lead 方法签名变更
```csharp
// 旧版本
Lead(hand, role)

// 新版本
Lead(hand, role, myPosition, opponentPositions)
```

**向后兼容**：新参数都有默认值

## 迁移指南

### 最小改动（不使用记牌）
```csharp
// 旧代码
var result = ai.Lead(hand, AIRole.Opponent);

// 新代码（完全兼容）
var result = ai.Lead(hand, AIRole.Opponent);
```

### 完整功能（使用记牌）
```csharp
// 新局开始
ai.ResetMemory();

// 每墩后记录
ai.RecordTrick(plays);

// 出牌时提供位置信息
var result = ai.Lead(hand, AIRole.Opponent,
    myPosition: 0,
    opponentPositions: new List<int> { 1, 3 });
```

## 版本历史

- v1.0 (2026-03-11): 初始AI实现
- v1.1 (2026-03-12): 增加角色感知、难度等级、对家配合
- v1.2 (2026-03-13): 修复P1/P2问题，优化性能和API
- v1.3 (2026-03-13): 增加记牌系统和智能甩牌决策
