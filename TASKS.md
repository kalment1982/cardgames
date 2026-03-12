# 拖拉机游戏开发任务清单

## 当前状态：Sprint 1 完成 ✅

---

## ✅ Sprint 1: 核心数据模型（已完成）

### Task 1.1: 核心数据结构 ✅
- [x] Enums.cs - 枚举定义
- [x] Card.cs - 卡牌类
- [x] GameConfig.cs - 游戏配置
- [x] CardComparer.cs - 卡牌比较器
- [x] CardPattern.cs - 牌型识别
- [x] 单元测试（15个测试用例全部通过）

**交付成果：**
- 能正确表示108张牌
- 能识别主牌/副牌
- 能比较牌的大小
- 能识别单张、对子、拖拉机
- 支持断档拖（级牌在中间时）

---

## 🔄 Sprint 2: 规则引擎（进行中）

### Task 2.1: 出牌合法性校验
**目标：** 验证玩家出牌是否合法

**需要实现：**
- `PlayValidator.cs`
  - `IsValidPlay()` - 检查出牌是否合法
  - `CanPlayCards()` - 检查手牌中是否能出这些牌
  - `ValidatePattern()` - 验证牌型是否有效

**验收标准：**
- 通过测试用例 3.1-3.4（出牌合法性测试）

---

### Task 2.2: 跟牌约束检查
**目标：** 验证跟牌是否符合规则

**需要实现：**
- `FollowValidator.cs`
  - `MustFollowSuit()` - 检查是否必须跟花色
  - `MustFollowPattern()` - 检查是否必须跟牌型
  - `GetValidFollows()` - 获取所有合法跟牌

**验收标准：**
- 通过测试用例 4.1-4.5（跟牌约束测试）

---

### Task 2.3: 胜负判定逻辑
**目标：** 判断每墩的获胜者

**需要实现：**
- `TrickJudge.cs`
  - `DetermineWinner()` - 判定获胜者
  - `CompareCards()` - 比较牌的大小
  - `CompareMixedPattern()` - 混合牌型比较

**验收标准：**
- 通过测试用例 5.1-5.5（胜负判定测试）

---

### Task 2.4: 甩牌判定
**目标：** 判断甩牌是否成功

**需要实现：**
- `ThrowValidator.cs`
  - `IsThrowSuccessful()` - 判断甩牌是否成功
  - `GetSmallestCard()` - 获取最小单牌（失败时用）

**验收标准：**
- 通过测试用例 6.1-6.3（甩牌测试）

---

### Task 2.5: 抠底计分
**目标：** 计算抠底分数

**需要实现：**
- `ScoreCalculator.cs`
  - `CalculateBottomScore()` - 计算抠底分数
  - `GetMultiplier()` - 获取倍数（单张/对子/拖拉机）
  - `CalculateTotalScore()` - 计算总分

**验收标准：**
- 通过测试用例 7.1-7.5（抠底计分测试）

---

## 📋 Sprint 3: 游戏流程（待开始）

### Task 3.1: 发牌逻辑
**目标：** 实现发牌流程

**需要实现：**
- `Deck.cs` - 牌堆（108张牌）
- `DealingPhase.cs` - 发牌阶段
  - `Shuffle()` - 洗牌
  - `Deal()` - 逐张发牌
  - `GetBottomCards()` - 获取底牌

**验收标准：**
- 通过测试用例 1.1（发牌正确性）

---

### Task 3.2: 亮主/反主
**目标：** 实现亮主和反主逻辑

**需要实现：**
- `TrumpBidding.cs`
  - `CanBid()` - 是否可以亮主
  - `CanCounter()` - 是否可以反主
  - `ProcessBid()` - 处理亮主

**验收标准：**
- 通过测试用例 9.1-9.5（亮主反主测试）

---

### Task 3.3: 扣底
**目标：** 实现扣底逻辑

**需要实现：**
- `BottomBurying.cs`
  - `AddBottomToHand()` - 底牌加入手牌
  - `BuryBottom()` - 扣底
  - `ValidateBury()` - 验证扣底合法性

**验收标准：**
- 通过测试用例 1.2（扣底流程）

---

### Task 3.4: 出牌流程
**目标：** 实现完整的出牌流程

**需要实现：**
- `PlayingPhase.cs`
  - `PlayTrick()` - 进行一墩
  - `GetNextPlayer()` - 获取下一个出牌者
  - `CollectTrick()` - 收集这墩牌

**验收标准：**
- 能完整跑通25墩出牌

---

### Task 3.5: 升级判定
**目标：** 实现升级规则

**需要实现：**
- `LevelManager.cs`
  - `CalculateLevel()` - 计算升级
  - `CheckMustPlayLevel()` - 检查必打关
  - `DetermineNextDealer()` - 确定下一个坐庄玩家

**验收标准：**
- 通过测试用例 8.1-8.7（升级规则测试）

---

## 🤖 Sprint 4: AI实现（待开始）

### Task 4.1: AI出牌策略
**目标：** AI能合法出牌

**需要实现：**
- `AIPlayer.cs`
- `LeadStrategy.cs` - 首家出牌策略
  - 基础策略：出最大的牌
  - 进阶策略：根据局势选择

---

### Task 4.2: AI跟牌策略
**目标：** AI能智能跟牌

**需要实现：**
- `FollowStrategy.cs` - 跟牌策略
  - 队友赢：垫小牌
  - 对手赢：尝试压制或垫分

---

### Task 4.3: AI扣底策略
**目标：** AI能合理扣底

**需要实现：**
- `BuryStrategy.cs` - 扣底策略
  - 扣分牌
  - 扣短门

---

### Task 4.4: AI难度调整
**目标：** 支持不同难度的AI

**需要实现：**
- `AIDifficulty.cs`
  - Easy: 随机合法出牌
  - Medium: 基础策略
  - Hard: 进阶策略

---

## 🎮 Sprint 5: Unity集成（待开始）

### Task 5.1: Unity项目创建
- 创建Unity项目（Unity 2021.3 LTS+）
- 配置目标平台（Android、iOS、macOS）

### Task 5.2: 代码迁移
- 将 `src/Core/` 复制到 `Assets/Scripts/Core/`
- 配置Unity Test Framework

### Task 5.3: UI实现
- CardView.cs - 卡牌显示
- HandView.cs - 手牌显示
- GameUI.cs - 游戏界面

### Task 5.4: 交互实现
- 拖拽出牌
- 点击选牌
- 按钮交互

---

## 📊 进度总览

- ✅ Sprint 1: 核心数据模型（100%）
- 🔄 Sprint 2: 规则引擎（0%）
- ⏳ Sprint 3: 游戏流程（0%）
- ⏳ Sprint 4: AI实现（0%）
- ⏳ Sprint 5: Unity集成（0%）

**总体进度：** 20%

---

## 🎯 下一步行动

**建议：开始 Task 2.1 - 出牌合法性校验**

这是规则引擎的基础，完成后可以继续其他规则模块。

**预计时间：** 2-3小时（我自主完成）

**你需要做的：**
1. 确认开始 Task 2.1
2. 我会自主开发和测试
3. 完成后我会通知你

---

## 📝 备注

- 所有代码使用纯C#，不依赖Unity API
- 每个Task完成后都会有对应的单元测试
- 测试通过后才算完成
- 代码遵循SOLID原则，易于后续扩展
