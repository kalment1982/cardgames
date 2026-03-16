# Agent Notes

This repository writes two default game logs.  
All coding agents (Codex/Claude/etc.) should check these paths first when debugging gameplay flow:

## Log Paths

1. Machine-readable audit logs (JSONL):
- `/Users/karmy/Projects/CardGame/tractor/logs/raw`
- File pattern: `tractor-YYYY-MM-DD-HH.jsonl`

2. Human-readable replay logs (Markdown):
- `/Users/karmy/Projects/CardGame/tractor/logs/replay`

## Startup Recommendation

To avoid writing logs into unexpected locations, set:

```bash
export TRACTOR_LOG_ROOT=/Users/karmy/Projects/CardGame/tractor/logs/raw
```

Then start WebUI.

## Runtime Logging Note

For browser gameplay, use:

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/run_webui.sh --kill-existing
```

This starts the integrated host (`WebUIHost`) on the same WebUI port and accepts in-game log relay at:
- `POST /api/log-entry`

## Decision Bundle To Regression

When a gameplay bug is found in AI behavior, prefer converting the decision bundle into a stable regression fixture instead of only keeping the raw log.

Reference:

- `/Users/karmy/Projects/CardGame/tractor/doc/规则验证器场景沉淀_v1.0.md`

Key entry points:

- Fixture copier: `/Users/karmy/Projects/CardGame/tractor/scripts/add_decision_bundle_fixture.sh`
- Scenario factory: `/Users/karmy/Projects/CardGame/tractor/tests/V21/DecisionBundleScenarioFactory.cs`
- Example regression: `/Users/karmy/Projects/CardGame/tractor/tests/V21/DecisionBundleScenarioFactoryTests.cs`

## Rule Regression Gate

This repo now has a dedicated regression suite for "首发多张牌型 / 跟牌 / 甩牌 / 毙牌 / 赢家判定".

All coding agents (Codex/Claude/etc.) must run the rule regression suite before finishing any change that touches one or more of:

- `src/Core/Rules/FollowValidator.cs`
- `src/Core/Rules/TrickJudge.cs`
- `src/Core/Rules/ThrowValidator.cs`
- `src/Core/Rules/PlayValidator.cs`
- `src/Core/GameFlow/Game.cs`
- `src/Core/AI/AIPlayer.cs`
- `tests/LeadPatternMatrixTests.cs`
- `tests/FollowValidatorRegressionTests.cs`
- `doc/80分_拖拉机游戏规则文档_v1.4.md`
- any change explicitly related to: 跟牌、甩牌、毙牌、主牌压制、首发多张牌型、赢家判定

### Required command

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/test_rule_regression.sh
```

### Covered suites

- `LeadPatternMatrixTests`
- `FollowValidatorRegressionTests`
- `TrickJudgeRegressionTests`
- `ThrowRulesTests`
- `MixedThrowApiTests`

### Human-readable checklist

If the change affects rule behavior, test data, or expected outcomes, agents should also regenerate the human-readable matrix checklist:

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/export_lead_pattern_checklist.sh
```

Generated file:

- `/Users/karmy/Projects/CardGame/tractor/unittest/self/lead_pattern_matrix_checklist.md`

### Reporting expectation

When finishing a rule-related change, agents should report:

- whether `test_rule_regression.sh` was run
- whether it passed or was blocked by an existing compile/runtime issue
- whether the checklist was regenerated

## Test Tiers

Use the lightweight tier by default during normal debugging and feature work:

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/test_fast.sh
```

Standard regression for broader non-UI coverage:

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/test_standard.sh
```

Heavy suites are tagged and should be run deliberately, not by default:

- `Category=SelfPlay`
- `Category=Benchmark`
- `Category=Campaign`
- `Category=LongRunning`
- `Category=UI`

Examples:

```bash
dotnet test --filter "Category=SelfPlay"
dotnet test --filter "Category=Benchmark"
```
