# UI 自动化测试

该目录提供“真人式 UI 自动化 + 规则校验分析”能力：

- 使用 Playwright 模拟玩家在 WebUI 中完成亮主、扣底、出牌整局流程。
- 每局记录完整事件流（出牌、每墩结算、终局分数）。
- 自动分析：
  - 每一墩赢家是否符合 `TrickJudge` 规则
  - 每一墩得分增量是否符合规则
  - 终局分（含抠底）是否符合规则

## 目录结构

- `run-ui-campaign.mjs`：批量跑局脚本
- `package.json`：Playwright 依赖与脚本
- `reports/`：执行结果（Markdown + JSON + 截图）

## 使用方法

1. 安装依赖

```bash
cd /Users/karmy/Projects/CardGame/tractor/unittest/ui
npm install
npx playwright install chromium
```

2. 执行 5 局（默认）

```bash
npm run test:ui
```

3. 执行 50 局

```bash
npm run test:ui:50
```

也可自定义参数：

```bash
node run-ui-campaign.mjs --games 50 --start-seed 5000 --base-url http://127.0.0.1:5167
```

## 报告输出

每次执行会在 `reports/ui_campaign_<timestamp>/` 下生成：

- `summary.md`：汇总报告
- `summary.json`：结构化结果
- `raw/game_seed_*.json`：每局完整事件与分析
- `screenshots/game_seed_*.png`：每局结束截图
