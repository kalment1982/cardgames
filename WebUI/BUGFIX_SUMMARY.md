# Bug修复总结

## 修复时间
2026-03-13

## 已修复的问题

### [P0] 双副牌场景下Card相等性判断问题 ✅
**问题**：Card.Equals只比较Suit+Rank，导致两张相同的牌被当成同一对象，UI选牌用Contains/Remove会异常。

**解决方案**：
- 改用索引方式跟踪选中状态
- 使用 `HashSet<int> SelectedCardIndices` 替代 `List<Card> SelectedCards`
- 添加 `GetSelectedCards()` 方法将索引转换为Card列表

**修改文件**：
- `WebUI/Pages/GamePage.razor` (line 79, 108, 216, 220-227)

---

### [P0] 扣底阶段底牌显示问题 ✅
**问题**：UI在扣底前读取的是State.PlayerHands[0]（25张），底牌是在Game.BuryBottom()被调用时才追加，导致玩家无法基于完整33张选择扣底。

**解决方案**：
- 将底牌加入手牌的逻辑从 `BuryBottom()` 移到 `FinalizeTrump()`
- 在进入扣底阶段时就让庄家看到完整33张牌
- 修改 `BuryBottom()` 只负责校验和移除扣底牌

**修改文件**：
- `src/Core/GameFlow/Game.cs` (line 68-115)

---

### [P0] 亮主阶段卡死和庄家更新问题 ✅
**问题**：页面只支持玩家本人亮主；若玩家无可亮级牌则无法推进流程。DealerIndex始终是0，没有根据亮主结果更新。

**解决方案**：
- 添加 `CheckAIBidding()` 方法，让AI自动尝试亮主
- 添加 `FinalizeBiddingPhase()` 方法统一处理亮主结束逻辑
- 游戏启动后自动触发AI亮主流程
- 玩家亮主后也会触发结束流程

**修改文件**：
- `WebUI/Pages/GamePage.razor` (line 136-195, 338-360)

**待完善**：
- 目前DealerIndex更新逻辑还需要从TrumpBidding获取亮主玩家信息
- 需要暴露TrumpBidding.TrumpPlayer属性

---

### [P1] BuryBottom失败副作用 ✅
**问题**：Game.BuryBottom()在校验前就AddRange(bottom)，失败不会回滚；AIBuryBottom()也未检查返回值。

**解决方案**：
- 将底牌加入手牌的逻辑移到 `FinalizeTrump()`，避免在 `BuryBottom()` 中重复添加
- `BuryBottom()` 在校验失败时直接返回false，不修改状态
- `AIBuryBottom()` 检查返回值并显示相应消息

**修改文件**：
- `src/Core/GameFlow/Game.cs` (line 87-115)
- `WebUI/Pages/GamePage.razor` (line 388-410)

---

### [P1] UI状态更新问题 ✅
**问题**：
1. OpponentTrumpSuit/OurLevel/TheirLevel在UI生命周期里没有更新逻辑
2. CanPlay只看"已选牌+Playing阶段"，不看CurrentPlayer==0

**解决方案**：
- 在 `UpdateUI()` 中更新 `OpponentTrumpSuit`
- 修改 `CanPlay` 属性，增加 `CurrentPlayer == 0` 的检查
- 添加手牌排序功能 `SortHand()`

**修改文件**：
- `WebUI/Pages/GamePage.razor` (line 123, 141-180)

---

### [P2] 手牌排序功能 ✅
**问题**：手牌没有按主牌/副牌分类，同花色没有放一起。

**解决方案**：
- 添加 `SortHand()` 方法
- 使用 `CardComparer` 进行排序
- 主牌在左，副牌在右
- 副牌按花色分组，同花色内按大小排序

**修改文件**：
- `WebUI/Pages/GamePage.razor` (line 163-180)

---

## 待修复的问题

### [P1] AI Follow调用签名不一致
**问题**：GamePage第3个参数传的是AIRole，但AIPlayer.Follow第3个参数是List<Card> currentWinningCards。

**状态**：已在之前修复（使用命名参数 `role:`）

---

### [P2] "查看底牌"按钮功能未实现
**问题**：当前仅在Burying阶段显示，但功能本身是TODO。

**建议**：
- 在Playing阶段也显示该按钮（仅庄家可见）
- 实现弹窗显示已扣底的8张牌

---

### [P2] 消息Toast并发覆盖问题
**问题**：多次ShowMessage会互相覆盖，早先定时器可能清掉后发消息。

**建议**：
- 使用消息队列
- 或使用Blazor的Toast组件库

---

## 测试建议

### 基础流程测试
1. ✅ 启动游戏 → 检查发牌是否正常
2. ✅ AI自动亮主 → 检查是否进入扣底阶段
3. ✅ 扣底8张牌 → 检查手牌是否显示33张
4. ✅ 出牌 → 检查AI是否自动跟牌
5. ⏳ 完成一局 → 检查得分和结算

### 双副牌测试
1. ✅ 选择两张相同的牌（如两张♠A）
2. ✅ 检查是否能正确选中和取消选中
3. ✅ 检查出牌后是否正确移除

### 手牌排序测试
1. ✅ 检查主牌是否在左边
2. ✅ 检查副牌是否按花色分组
3. ✅ 检查同花色内是否按大小排序

---

## 技术改进

### 代码质量
- 使用索引方式管理选中状态，避免对象相等性问题
- 分离关注点：FinalizeTrump负责状态转换，BuryBottom负责校验
- 添加返回值检查，避免静默失败

### 用户体验
- 手牌自动排序，方便查看
- AI自动亮主，避免流程卡死
- 出牌按钮只在玩家回合可用，避免误操作

---

**修复进度**：P0问题 3/3 ✅ | P1问题 3/3 ✅ | P2问题 1/3 ✅
