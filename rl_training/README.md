# Tractor PPO AI Training

## Status

- Active training path: `Phase 1 PPO`
- Active entrypoint: `python3 rl_training/train_phase1.py`
- Active config: `rl_training/phase1_config.yaml`
- Legacy path: `llm_teacher.py + pretrain.py + train_enhanced.py`
- Legacy status: retained for reference, CLI disabled, not supported for current training

As of 2026-03-18, this directory is standardized on the `Phase 1 PPO` workflow only.

## Quick Start

From the project root:

```bash
pip3 install -r rl_training/requirements.txt
python3 rl_training/generate_eval_seeds.py
python3 rl_training/train_phase1.py --max_iterations 1
python3 rl_training/train_phase1.py
```

Or use:

```bash
./rl_training/quickstart.sh --max_iterations 1
./rl_training/quickstart.sh
```

## Training Flow

`Phase 1 PPO` uses:

- Observation dim: `382`
- Action dim: `384`
- PPO seats: `[0, 2]`
- RuleAI opponent seats: `[1, 3]`
- Reward: `(+10 if win else -10) + 2 * level_gain + 0.02 * final_score`
- Engine host: `tools/PpoEngineHost/bin/Release/net6.0/PpoEngineHost`

The training loop is:

1. Collect complete-game trajectories against RuleAI.
2. Compute GAE.
3. Run PPO updates.
4. Evaluate on fixed seeds every `eval_interval`.
5. Save checkpoints and best model.

## Key Commands

Generate eval seeds:

```bash
python3 rl_training/generate_eval_seeds.py
```

Smoke test:

```bash
python3 rl_training/train_phase1.py --max_iterations 1
```

Full training:

```bash
python3 rl_training/train_phase1.py
```

One-click visual training:

```bash
bash rl_training/start_phase2_visual_training.sh
```

Resume:

```bash
python3 rl_training/train_phase1.py --resume checkpoints/phase1/checkpoint_500.pt
```

Warm-start initialize:

```bash
python3 rl_training/train_phase1.py --init_checkpoint checkpoints/phase1_warm_start/pretrained_ruleai_v21.pt
```

Evaluate:

```bash
python3 rl_training/evaluate_phase1.py --checkpoint checkpoints/phase1/best_model.pt
```

Warm-start dataset + pretrain:

```bash
python3 rl_training/generate_warm_start_data.py
python3 rl_training/pretrain_bc.py
```

Default one-click warm-start flow:

```bash
bash rl_training/start_warm_start_default.sh
```

## Outputs

- Training log: `logs/phase1/training_log.csv`
- Eval summary: `logs/phase1/eval_summary.csv`
- Eval match list: `logs/phase1/eval_match_results.jsonl`
- TensorBoard logs: `logs/phase1/tb/`
- Checkpoints: `checkpoints/phase1/checkpoint_*.pt`
- Best model: `checkpoints/phase1/best_model.pt`
- Final model: `checkpoints/phase1/final_model.pt`
- Eval seeds: `rl_training/eval_seeds.txt`
- Warm-start dataset: `artifacts/ppo_warm_start/ruleai_v21_playtricks_2000g.npz`
- Warm-start checkpoint: `checkpoints/phase1_warm_start/pretrained_ruleai_v21.pt`

## Visualization

TensorBoard:

```bash
tensorboard --logdir logs/phase1/tb
```

Single-page Streamlit dashboard:

```bash
streamlit run rl_training/streamlit_phase1.py
```

Or start training + TensorBoard + Streamlit together:

```bash
bash rl_training/start_phase2_visual_training.sh
```

## Warm Start

Current warm-start path is:

- data source: `RuleAI V2.1`
- scope: `PlayTricks`
- seats: current PPO seats only
- demo format: `(state, legal_mask, teacher_slot)`
- pretrain objective: policy-only behavior cloning
- default dataset scale: `2000` games
- default BC epochs: `3`

The C# host exposes `get_teacher_action` so the exported PPO legal action pool
always includes the teacher-selected action for the current PPO seat.

## Success Targets

Phase 1 targets are:

- Illegal action rate `< 5%`
- Win rate vs RuleAI `> 20%`
- Win rate vs random baseline `> 70%`

Use `evaluate_phase1.py` to check the RuleAI targets directly.

## Legacy Workflow

The following files are preserved only as historical reference:

- `rl_training/config.yaml`
- `rl_training/llm_teacher.py`
- `rl_training/pretrain.py`
- `rl_training/train_enhanced.py`
- `rl_training/GMN_SETUP.md`

They are not the active training path for this repository. Their CLI entrypoints are disabled on purpose to avoid drifting back to the old flow.
