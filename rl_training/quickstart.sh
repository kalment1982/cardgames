#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

echo "=========================================="
echo "Tractor PPO AI - Phase 1 Quickstart"
echo "=========================================="
echo ""
echo "Status:"
echo "  - Active training path: Phase 1 PPO"
echo "  - Legacy LLM/pretrain/enhanced flow: retained on disk, CLI disabled"
echo ""

cd "$PROJECT_ROOT"

echo "[1/3] Installing/validating Python dependencies is not automated here."
echo "      If needed, run: pip3 install -r rl_training/requirements.txt"
echo ""

echo "[2/3] Ensuring evaluation seeds exist..."
python3 rl_training/generate_eval_seeds.py
echo ""

echo "[3/3] Starting Phase 1 PPO training..."
echo "      Command: python3 rl_training/train_phase1.py $*"
echo ""
python3 rl_training/train_phase1.py "$@"
