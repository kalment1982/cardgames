#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

CONFIG_PATH="${CONFIG_PATH:-$SCRIPT_DIR/phase1_config.yaml}"

echo "=========================================="
echo "Tractor PPO AI - Warm Start Default Flow"
echo "=========================================="
echo ""

cd "$PROJECT_ROOT"

echo "[1/4] Building PpoEngineHost..."
dotnet build tools/PpoEngineHost/PpoEngineHost.csproj -c Release >/tmp/tractor_ppo_warm_build.log
echo "      build log: /tmp/tractor_ppo_warm_build.log"
echo ""

echo "[2/4] Generating RuleAI warm-start dataset (default config)..."
python3 rl_training/generate_warm_start_data.py --config "$CONFIG_PATH"
echo ""

echo "[3/4] Running behavior cloning pretrain (default config)..."
python3 rl_training/pretrain_bc.py --config "$CONFIG_PATH"
echo ""

echo "[4/4] Starting PPO Phase 1 from warm-start checkpoint..."
python3 rl_training/train_phase1.py --config "$CONFIG_PATH" --init_checkpoint checkpoints/phase1_warm_start/pretrained_ruleai_v21.pt "$@"
