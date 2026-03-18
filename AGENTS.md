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

## AI Lines: RuleAI vs PPO

This repository currently contains **two separate AI lines**.  
All coding agents must distinguish them before debugging or changing AI behavior.

### 1. RuleAI line (current runtime / production path)

This is the AI path actually used by current WebUI gameplay.

Current runtime chain:

- `WebUI` -> `AIPlayer`
- `AIPlayer` -> `Rule AI V2.1` by default
- runtime parameters -> loaded from `Champion` JSON

Key files:

- `/Users/karmy/Projects/CardGame/tractor/src/Core/AI/AIPlayer.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/AI/V21/`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/AI/AIStrategyParameters.cs`
- `/Users/karmy/Projects/CardGame/tractor/src/Core/AI/ChampionLoader.cs`
- `/Users/karmy/Projects/CardGame/tractor/WebUI/Application/AITurnService.cs`
- `/Users/karmy/Projects/CardGame/tractor/WebUI/wwwroot/appsettings.json`

Important facts:

- Current WebUI gameplay uses `Rule AI V2.1` by default.
- `Legacy Rule AI` still exists inside `AIPlayer` as fallback / compare path.
- `ChampionLoader` loads parameter JSON, not a neural-network checkpoint.
- Default champion file location is under:
  - `/Users/karmy/Projects/CardGame/tractor/data/evolution/champions/champion_current.json`

When debugging actual in-game AI behavior, agents should inspect this line first.

### 2. PPO AI line (separate Python RL project)

This is a separate RL / neural-network training project.  
It is **not** wired into the current C# / WebUI runtime path.

Key files:

- `/Users/karmy/Projects/CardGame/tractor/rl_training/README.md`
- `/Users/karmy/Projects/CardGame/tractor/rl_training/ppo_agent.py`
- `/Users/karmy/Projects/CardGame/tractor/rl_training/train_rl.py`
- `/Users/karmy/Projects/CardGame/tractor/rl_training/train_enhanced.py`
- `/Users/karmy/Projects/CardGame/tractor/rl_training/TRAINING_DESIGN.md`
- `/Users/karmy/Projects/CardGame/tractor/rl_training/TRAINING_GUIDE.md`

Important facts:

- This line contains PPO, state encoders, LLM teacher, and Python training scripts.
- It uses `.pt` / `.pth` style checkpoints on the Python side.
- There is currently no active C# / WebUI loader that consumes PPO checkpoints for live gameplay.
- Therefore, PPO changes do not affect current browser gameplay unless explicit integration work is added.
- PPO AI design documents should include the literal prefix `PPO AI` in the filename when creating new PPO-related docs.

### 3. Working rule for all agents

Before touching AI code, decide which line the task belongs to:

- If the task is about current gameplay, decision bundles, WebUI AI behavior, regression, or live play bugs:
  - work on the **RuleAI line**
- If the task is about RL training, PPO, neural-network checkpoints, LLM teacher, or Python training scripts:
  - work on the **PPO AI line**

Do not assume PPO is the source of current gameplay bugs unless the user explicitly asks about `rl_training` or a new integration task.

## Shared AI Input Schema

RuleAI and PPO now share a single input-definition source of truth:

- `/Users/karmy/Projects/CardGame/tractor/doc/AI共享决策输入定义_v1.0.md`
- `/Users/karmy/Projects/CardGame/tractor/doc/AI共享决策输入编码定义_v1.0.md`

All coding agents must use this document when working on:

- RuleAI decision context fields
- PPO observation design
- stage-1 / stage-2 AI input scope
- mapping from RuleAI context to PPO input

### Required working rule

If a task changes AI decision inputs, field meanings, stage boundaries, or mapping between RuleAI and PPO:

1. Update `/Users/karmy/Projects/CardGame/tractor/doc/AI共享决策输入定义_v1.0.md` first
2. Then update the AI-specific document:
   - `/Users/karmy/Projects/CardGame/tractor/doc/规则AI架构设计_v2.1.md`
   - `/Users/karmy/Projects/CardGame/tractor/doc/PPO训练架构设计_v1.0.md`
3. Then update code and tests

### Anti-drift rule

Agents must not maintain duplicate, conflicting field definitions across:

- `规则AI架构设计_v2.1.md`
- `PPO训练架构设计_v1.0.md`
- code comments

The shared input-definition document is the canonical place for:

- field names
- field meanings
- phase-1 / phase-2 inclusion
- RuleAI -> PPO mapping

The shared input-encoding document is the canonical place for:

- encoding form
- normalization rule
- observation concatenation order
- stage-1 / stage-2 observation expansion

### Shared PPO action space

PPO AI action-space semantics should use:

- `/Users/karmy/Projects/CardGame/tractor/doc/PPO AI_共享动作空间定义_v1.0.md`

PPO AI bridge protocol should use:

- `/Users/karmy/Projects/CardGame/tractor/doc/PPO AI_CSharp训练引擎桥接设计_v1.0.md`

When changing:

- fixed action dimension
- slot semantics
- legal-action to slot mapping
- overflow policy

agents should update that document before updating PPO bridge or training code.

### Maintenance expectation

When any of these change, agents should synchronize the shared document in the same task:

- `RuleAIContext`
- `HandProfile`
- `DecisionFrame`
- `MemorySnapshot`
- `InferenceSnapshot`
- PPO observation fields derived from them

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

## PPO Phase 1 训练方案

### 快速启动

生成评估种子（首次运行）：
```bash
python3 rl_training/generate_eval_seeds.py
```

启动完整训练（2000 次迭代，约 20,000 局）：
```bash
python3 rl_training/train_phase1.py
```

从检查点恢复训练：
```bash
python3 rl_training/train_phase1.py --resume checkpoints/phase1/checkpoint_500.pt
```

评估已保存的检查点：
```bash
python3 rl_training/evaluate_phase1.py --checkpoint checkpoints/phase1/best_model.pt
```

### 训练配置

配置文件：`rl_training/phase1_config.yaml`

关键参数：
- 状态维度：382（来自 StateEncoder）
- 动作维度：384（来自 ActionSlotMapper）
- 每次迭代游戏数：10
- PPO 更新轮数：10
- 批次大小：64
- 学习率：0.0003
- GAE lambda：0.95
- Clip epsilon：0.2

### Phase 1 成功指标

训练目标（2000 次迭代后）：
- ✅ 非法动作率 < 5%
- ✅ 对战 RuleAI 胜率 > 20%
- ✅ 对战随机基线胜率 > 70%

### 输出文件

训练日志：
- `logs/phase1/training_log.csv` - 包含每次迭代的指标

检查点：
- `checkpoints/phase1/checkpoint_N.pt` - 每 50 次迭代保存
- `checkpoints/phase1/best_model.pt` - 最佳评估胜率模型
- `checkpoints/phase1/final_model.pt` - 训练结束时的最终模型

评估种子：
- `rl_training/eval_seeds.txt` - 100 个固定种子用于可复现评估

### 训练架构

**环境设置：**
- PPO 座位：[0, 2]（南、北）
- RuleAI 座位：[1, 3]（东、西）
- C# 引擎：`tools/PpoEngineHost/bin/Release/net6.0/PpoEngineHost`

**奖励函数：**
```
R = (+10 if win else -10) + 2 * level_gain + 0.02 * final_score
```
仅在游戏结束时给予奖励（terminal-only reward）。

**训练流程：**
1. 顺序收集轨迹（10 局/次迭代）
2. 计算 GAE 优势函数
3. PPO 更新（10 轮，批次大小 64）
4. 每 10 次迭代评估（100 局固定种子）
5. 每 50 次迭代保存检查点

### 关键文件

实现文件：
- `rl_training/ppo_agent.py` - PPO 网络和智能体
- `rl_training/train_phase1.py` - 主训练脚本
- `rl_training/evaluate_phase1.py` - 评估脚本
- `rl_training/mvp_env.py` - Gym 风格环境包装器
- `rl_training/engine_bridge.py` - C# 引擎桥接
- `rl_training/state_encoder.py` - 状态编码器（382 维）
- `rl_training/action_mask.py` - 动作掩码构建器（384 维）

配置文件：
- `rl_training/phase1_config.yaml` - Phase 1 训练配置
- `rl_training/eval_seeds.txt` - 评估种子

### 验证步骤

运行单次迭代冒烟测试：
```bash
python3 rl_training/train_phase1.py --max_iterations 1
```

测试 PPO 智能体：
```bash
python3 rl_training/ppo_agent.py
```

评估未训练模型基线：
```bash
python3 rl_training/evaluate_phase1.py --checkpoint checkpoints/phase1/final_model.pt --num_games 10 --verbose
```

### 设备支持

自动检测并使用：
- MPS（Apple Silicon GPU）
- CUDA（NVIDIA GPU）
- CPU（回退选项）

训练脚本会自动选择最佳可用设备。
