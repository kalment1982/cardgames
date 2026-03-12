# 接口问题分析报告

## 要求
- 不修改游戏代码（仅编写/调整测试与报告）。
- 测试程序与报告统一放在 `unittest/` 目录。

## 分析范围
- 规则与模型接口：
  - `Card`, `GameConfig`, `CardComparer`, `CardPattern`
  - `PlayValidator`, `FollowValidator`, `ThrowValidator`, `TrickJudge`, `ScoreCalculator`
- 流程接口：
  - `Deck`, `DealingPhase`, `BottomBurying`, `TrumpBidding`, `GameState`, `Game`
- AI 接口：
  - `AIPlayer`
- 升级接口：
  - `LevelManager`, `LevelResult`

## 依据文档
- `doc/80分_拖拉机游戏规则文档_v1.4.md`
- `doc/测试用例设计_v1.0.md`

## 问题清单（按严重度）

### P0 / P1
1) **甩牌判定未比较大小，仅比较“是否存在结构”**
   - 位置：`src/Core/Rules/ThrowValidator.cs`（L57-L66）
   - 现状：只要跟牌方也有对子/拖拉机就视为“可管上”，未比较牌面大小。
   - 影响：会把实际“管不上”的跟牌判成“管上”，导致甩牌被错误判失败。
   - 规则依据：规则文档 2.6、混合甩牌判定规则需要比较同结构大小。

2) **甩牌花色一致性缺少校验**
   - 位置：`src/Core/Rules/ThrowValidator.cs`（L27-L52）
   - 现状：只取首张牌的花色/主副属性作为甩牌花色，未校验 `throwCards` 是否同花/同主。
   - 影响：混花甩牌可能被当成合法甩牌并参与比较。

3) **跟牌规则对“花色不足时的补牌”不支持**
   - 位置：`src/Core/Rules/FollowValidator.cs`（L40-L99）
   - 现状：只要手里“有首引花色”，就强制 `followCards` 全部同花/同主。
   - 影响：当手牌首引花色数量不足时，本应“先跟尽所有首引花色，余下垫牌/毙牌”，当前实现会判非法。
   - 规则依据：规则文档 2.5 跟牌硬约束（“有首引花色必须先跟该花色”）。

4) **拖拉机跟牌约束仅检查“全体同花牌是否构成拖拉机”**
   - 位置：`src/Core/Rules/FollowValidator.cs`（L159-L174）
   - 现状：若手里同花牌里包含拖拉机，但还有额外散牌，`IsTractor` 会失败，从而不强制出拖拉机。
   - 影响：可能允许违反“有拖拉机必须跟拖拉机”的跟牌。

5) **主牌拖拉机相邻判断缺少“主级牌/副级牌”区分**
   - 位置：`src/Core/Models/CardPattern.cs`（L98-L149）
   - 现状：主牌顺序仅按 `Rank` 记录，导致主级牌与副级牌被视为同一序位。
   - 影响：
     - 可能把“主级牌 + 副级牌”误判为不相邻（漏判有效拖拉机）。
     - 或把“主级牌 + 主花色A”误判为相邻（误判无效拖拉机）。

6) **出牌/跟牌校验参数顺序错误，导致正常出牌被拒**
   - 位置：`src/Core/GameFlow/Game.cs`（PlayCards：L64-L88）
   - 现状：调用 `IsValidPlay(cards, hand)` 和 `IsValidFollow(leadCards, cards, hand)`，参数顺序与接口定义相反。
   - 影响：除“出牌等于整手牌”的极端情况外，正常首引与跟牌几乎都会被判非法。

7) **抠底倍数计算使用已清空的最后一墩数据**
   - 位置：`src/Core/GameFlow/Game.cs`（FinishTrick/FinishGame）
   - 现状：`FinishTrick` 中先 `Clear()` 当前墩，再调用 `FinishGame`；`FinishGame` 取不到最后一墩牌型，默认使用单张计算倍率。
   - 影响：抠底倍数恒按“单张×2”，对子/拖拉机抠底被低估。

8) **无人亮主时默认黑桃，未实现“无主”**
   - 位置：`src/Core/GameFlow/Game.cs`（FinalizeTrump）
   - 现状：没有亮主时直接设置 `TrumpSuit = Spade`。
   - 规则依据：规则文档 9.4（无人亮主 → 无主），存在行为偏差。

9) **扣底校验对“底牌”可能重复计入**
   - 位置：`src/Core/GameFlow/BottomBurying.cs` 与 `Game.BuryBottom`
   - 现状：`Game.BuryBottom` 已把底牌加入手牌后，`BottomBurying.BuryCards` 又将底牌再次加入校验集合。
   - 影响：理论上允许“超出实际持有数量”的牌被扣底（重复卡）。

### P2
1) **出牌合法性未结合甩牌成功判定**
   - 位置：`src/Core/Rules/PlayValidator.cs`（L64-L84）
   - 现状：混合牌型只要同花就判合法，未结合 `ThrowValidator`。
   - 影响：首家“甩牌”合法性与“甩牌是否成功”未联动，易出现“允许不应允许的甩牌”。

2) **胜负判定未覆盖混合甩牌/毙牌的结构对比规则**
   - 位置：`src/Core/Rules/TrickJudge.cs`（L103-L143）
   - 现状：仅基于整体牌型优先级 + 最大牌比较，未实现“混合牌型只比较对应子结构”的规则。
   - 影响：与规则文档 2.6/2.9 中的混合判定/毙牌规则不一致。

3) **CardComparer 对不同花色副牌返回 0**
   - 位置：`src/Core/Models/CardComparer.cs`
   - 现状：不同花色副牌比较返回 0，无法形成全序。
   - 影响：排序结果不稳定，虽当前主要用于同花场景，但对混合输入存在不确定性。

## 覆盖缺口
- 规则文档中的“升级判定”“完整牌局胜负与级别迁移”仍无对应接口实现，暂无可测接口。
- 亮主/反主仅实现了基础 `TrumpBidding`，未覆盖“发牌中多轮反主、无人亮主为无主”等完整流程规则。
