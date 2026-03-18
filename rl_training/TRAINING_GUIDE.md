# Tractor PPO AI Phase 1 Training Guide

## Current Standard

This repository now uses only the `Phase 1 PPO` workflow for active PPO AI training.

- Standard date: `2026-03-18`
- Active script: `rl_training/train_phase1.py`
- Active config: `rl_training/phase1_config.yaml`
- Active evaluation: `rl_training/evaluate_phase1.py`
- Legacy workflow: archived, CLI disabled

## Preconditions

Run from the project root:

```bash
pip3 install -r rl_training/requirements.txt
python3 rl_training/generate_eval_seeds.py
```

The C# host executable must exist at:

```bash
tools/PpoEngineHost/bin/Release/net6.0/PpoEngineHost
```

## Smoke Test

Use this first:

```bash
python3 rl_training/train_phase1.py --max_iterations 1
```

Expected result:

- one PPO update completes
- `logs/phase1/training_log.csv` is written
- `checkpoints/phase1/final_model.pt` is written

## Full Training

```bash
python3 rl_training/train_phase1.py
```

One-click visual training:

```bash
bash rl_training/start_phase2_visual_training.sh
```

Smoke test with dashboard:

```bash
bash rl_training/start_phase2_visual_training.sh --max_iterations 1
```

Default values come from `phase1_config.yaml`:

- `max_iterations: 2000`
- `games_per_iteration: 10`
- `eval_interval: 10`
- `save_interval: 50`

## Resume Training

```bash
python3 rl_training/train_phase1.py --resume checkpoints/phase1/checkpoint_500.pt
```

## Warm Start

Generate the default RuleAI warm-start dataset:

```bash
python3 rl_training/generate_warm_start_data.py
```

Run policy-only behavior cloning pretrain:

```bash
python3 rl_training/pretrain_bc.py
```

Start PPO from pretrained weights:

```bash
python3 rl_training/train_phase1.py --init_checkpoint checkpoints/phase1_warm_start/pretrained_ruleai_v21.pt
```

Run the full default warm-start pipeline:

```bash
bash rl_training/start_warm_start_default.sh
```

## Evaluate a Checkpoint

```bash
python3 rl_training/evaluate_phase1.py --checkpoint checkpoints/phase1/best_model.pt
```

Verbose evaluation:

```bash
python3 rl_training/evaluate_phase1.py --checkpoint checkpoints/phase1/best_model.pt --num_games 10 --verbose
```

## Output Locations

- CSV log: `logs/phase1/training_log.csv`
- Eval summary: `logs/phase1/eval_summary.csv`
- Eval match list: `logs/phase1/eval_match_results.jsonl`
- TensorBoard: `logs/phase1/tb/`
- Periodic checkpoints: `checkpoints/phase1/checkpoint_*.pt`
- Best checkpoint: `checkpoints/phase1/best_model.pt`
- Final checkpoint: `checkpoints/phase1/final_model.pt`

## Visualization

TensorBoard:

```bash
tensorboard --logdir logs/phase1/tb
```

Single-page Streamlit dashboard:

```bash
streamlit run rl_training/streamlit_phase1.py
```

Or start both services plus training together:

```bash
bash rl_training/start_phase2_visual_training.sh
```

## Runtime Semantics

`Phase 1 PPO` currently means:

- observation dim `382`
- action dim `384`
- terminal-only reward
- PPO seats `[0, 2]`
- RuleAI seats `[1, 3]`

Current warm-start defaults:

- data source: `RuleAI V2.1`
- scope: `PlayTricks`
- dataset size: `2000` games
- pretrain: `policy-only behavior cloning`
- BC epochs: `3`

Device selection is automatic:

- `mps` if available
- else `cuda`
- else `cpu`

## Legacy Workflow Status

The following old path is preserved but not usable as the standard workflow:

- `llm_teacher.py`
- `pretrain.py`
- `train_enhanced.py`
- `config.yaml`
- `GMN_SETUP.md`

Reason:

- it is not the repository standard anymore
- it depends on a separate LLM-teacher pipeline
- it should not be used to represent current PPO AI training status

If historical investigation is needed, read the files directly, but do not use them as the active training entrypoint.
