# 待修复清单（UI自动测试）

更新时间：2026-03-13
来源：`unittest/ui` 30局自动对战回归（seed 12000-12029）
报告：`/Users/kalment/projects/tractor/cardgames/unittest/ui/reports/ui_campaign_2026-03-13T03-33-11-167Z/summary.md`

## FIX-001（高）
问题：自动选牌在重复牌（对子/连对）场景会丢牌，导致选牌数量不足。

- 影响：`play_rejected` 高频出现，流程卡死，无法结束对局。
- 现象：对子跟牌场景中只选出单张（例如应出2张，实际只选1张）。
- 代码位置：`WebUI/Application/UiTestActionService.cs`
- 根因：`IndexOf + Distinct` 以“牌值等价”映射索引，重复牌被去重。
- 验收标准：
  - 对子/拖拉机场景自动选牌数量与AI返回数量一致；
  - 不再出现因“少选牌”导致的持续 `play_rejected`。

## FIX-002（高）
问题：UI自动化主循环在出牌被拒后没有降级策略，重复同一非法操作导致死循环。

- 影响：大量局超时，缺少 `game_finished` 事件。
- 代码位置：`unittest/ui/run-ui-campaign.mjs`
- 根因：`play_rejected` 后仍执行同一选牌与点击流程，未触发 fallback。
- 建议修复：
  - 连续拒绝达到阈值后执行兜底策略（最小合法牌/强制重选）；
  - 对同一 `trickIndex + player` 加防抖与熔断。
- 验收标准：
  - 30局自动测试无死循环；
  - 超时率显著下降（目标 < 5%）。

## FIX-003（中）
问题：`play_rejected` 事件缺失 `reason_code`，无法快速定位规则失败原因。

- 影响：分析成本高，无法区分“缺门未跟”“结构不符”“非当前玩家”等原因。
- 代码位置：`WebUI/Application/TurnPlayService.cs`
- 建议修复：
  - 将 `PlayCardsEx` 返回的 `OperationResult.ReasonCode` 写入 `play_rejected` 事件。
- 验收标准：
  - `play_rejected` 事件中包含 `reason_code`；
  - 报告可按 reason 聚合统计。

## 回归建议

1. 单seed复现：`12028`（可稳定复现死循环）。
2. 小样本回归：`--games 5 --start-seed 12026`。
3. 全量回归：`--games 30 --start-seed 12000 --timeout-ms 45000`。

## 所有已完成墩分析（本轮30局）

- 样本：`ui_campaign_2026-03-13T03-33-11-167Z`
- 总局数：30
- 正常结束：5
- 超时未结束：25
- 已完成并进入分析的墩（`trick_end`）：172

在这 172 墩内，现有分析器未报告以下错误：

- 墩赢家判定错误
- 墩分值合计错误
- 闲家分增量错误
- 闲家分累计前后不一致
- 终局分与抠底推导不一致（仅对正常结束局）

当前主要失败根因仍是“自动玩家选牌+重试逻辑”导致流程超时（见 FIX-001/FIX-002）。

## 自动化测试后验证清单（新增）

以下项目纳入每次 UI 自动化回归后的必检清单：

1. 跟牌数量和牌型是否正确
2. 甩牌判定是否正确
3. 是否正确判断了毙牌（重点关注对子与拖拉机场景）
4. 毙牌判断是否正确（主牌压制、副牌同花比较、结构约束）
5. 分数计算是否正确（本墩分、抠底分、庄/闲归属）
6. 一墩牌赢家判断是否正确

## 清单落地建议（脚本侧）

1. 在 `run-ui-campaign.mjs` 的 `analyzeGame` 中新增“跟牌结构一致性”检查。
2. 基于 `play_rejected.reason_code` 统计甩牌失败与毙牌失败类型（依赖 FIX-003）。
3. 新增对子/拖拉机专项种子池，单独输出“毙牌判定专项报告”。
4. 报告中增加“每墩判定依据”字段，便于与回放 UI 一键对照。
