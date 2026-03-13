# WebUI拆分重构方案 v1.0

- 文档状态：待确认
- 范围：仅 WebUI 层（不改 Core 规则引擎）
- 目标文件：`WebUI/Pages/GamePage.razor`

---

## 1. 拆分依据

本次拆分按以下 5 条执行：

1. 单一职责  
- 页面只负责渲染与事件绑定；流程、AI、日志、测试钩子下沉到独立服务。

2. 变化频率分离  
- 高频改动（UI样式）与中低频改动（规则流程、AI）隔离，减少互相回归影响。

3. 可测试性  
- 把业务逻辑迁到纯 C# 类，支持单元测试，不依赖 Razor 生命周期。

4. 依赖方向清晰  
- `Page -> Application Services -> Core`，禁止页面直接持有过多 Core 细节。

5. 复杂度阈值  
- 单文件承载“UI + 流程 + AI + 自动化测试”风险过高，需按能力域拆分。

---

## 2. 现状问题（GamePage）

1. 页面职责过载  
- 同时处理：发牌、亮主、扣底、AI出牌、消息提示、测试事件、UI状态。

2. 逻辑耦合高  
- UI事件直接驱动核心流程，难以定位问题（UI问题还是流程问题）。

3. 测试成本高  
- 大量逻辑在 `.razor` 内，无法独立做细粒度单测。

4. 迭代风险高  
- 增加一个功能（如查看上一轮牌）可能影响多个阶段逻辑。

---

## 3. 目标架构（WebUI）

```text
GamePage.razor
  ├─ GamePageViewModel (状态聚合)
  ├─ GameSessionService (流程编排)
  ├─ PlayerActionService (玩家操作校验/执行)
  ├─ AITurnService (AI回合驱动)
  ├─ UiMessageService (提示消息与toast)
  └─ UiAutomationService (autotest事件桥接)
```

---

## 4. 文件拆分清单

建议新增目录：`WebUI/Application/`

1. `WebUI/Application/GameSessionService.cs`  
- 职责：创建游戏、阶段流转、统一刷新状态。  
- 输出：当前阶段、当前玩家、分数、主花色等快照。

2. `WebUI/Application/PlayerActionService.cs`  
- 职责：处理玩家点击行为（叫主、扣底、出牌、查看）。  
- 输出：`OperationResult` + 用户可读提示文案。

3. `WebUI/Application/AITurnService.cs`  
- 职责：AI首出/跟牌/扣底驱动与节奏控制（延时、循环、终局检查）。  
- 说明：仅编排，不实现 AI 策略本身（策略仍在 Core.AI）。

4. `WebUI/Application/UiAutomationService.cs`  
- 职责：`autotest` 模式事件上报、状态快照、测试按钮行为。

5. `WebUI/Application/UiMessageService.cs`  
- 职责：统一消息队列/覆盖策略，避免页面里散落 `ShowMessage`。

6. `WebUI/Application/GamePageViewModel.cs`  
- 职责：页面展示状态聚合（手牌、选中索引、按钮可用性、面板显隐）。

7. `WebUI/Pages/GamePage.razor`（保留）  
- 职责：渲染 + 事件转发，不直接包含业务编排代码。

---

## 5. 职责边界（必须遵守）

1. 页面禁止直接调用 `Game` 的复杂流程方法链。  
2. 页面不直接 new `AIPlayer`。  
3. 页面不直接写自动化事件结构。  
4. 页面仅调用 Application 层公开方法并绑定 ViewModel。  
5. Core 不反向依赖 WebUI。

---

## 6. 迁移顺序（低风险）

1. 第一步：抽 `UiMessageService`  
- 零规则风险，先把消息逻辑统一。

2. 第二步：抽 `UiAutomationService`  
- 与业务隔离，减少页面噪音。

3. 第三步：抽 `AITurnService`  
- 把 AI 回合循环从页面迁走。

4. 第四步：抽 `PlayerActionService`  
- 叫主/扣底/出牌入口统一。

5. 第五步：抽 `GameSessionService + ViewModel`  
- 页面最终瘦身为模板+绑定。

---

## 7. 验收标准（拆分完成判定）

1. `GamePage.razor` 代码行数下降 40% 以上。  
2. 页面中不再出现 AI 决策构造逻辑。  
3. 页面中不再出现测试事件结构拼装逻辑。  
4. 核心主流程仍可跑通：发牌 -> 亮主 -> 扣底 -> 出牌 -> 结束。  
5. UI 自动化测试入口参数（`autotest`/`seed`）行为不变。

---

## 8. 风险与控制

1. 风险：状态同步出错（页面显示与真实状态不一致）  
- 控制：所有变更经 `GameSessionService` 单点刷新。

2. 风险：异步 AI 回合与 UI 更新竞态  
- 控制：AI 服务串行执行，统一 await。

3. 风险：自动化测试脚本失效  
- 控制：保留现有 `data-testid` 与事件字段，先兼容后优化。

---

## 9. 后续动作建议

1. 先评审本方案并锁定目录与类名。  
2. 按第 6 章顺序分 2~3 次小提交完成。  
3. 每次提交后跑一次 UI 冒烟（至少 1 局流程）。

