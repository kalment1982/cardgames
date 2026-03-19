#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

SETUP_SCRIPT="$SCRIPT_DIR/setup_macmini_ppo.sh"
VENV_DIR="${TRACTOR_PPO_VENV:-$PROJECT_ROOT/.venv_phase2}"

BASE_CONFIG="${TRACTOR_PPO_BASE_CONFIG:-$PROJECT_ROOT/rl_training/phase1_config.yaml}"
RESUME_CHECKPOINT="${TRACTOR_PPO_RESUME_CHECKPOINT:-$PROJECT_ROOT/checkpoints/phase1_overnight_20260319_005351/best_model.pt}"
MAX_ITERATIONS="${TRACTOR_PPO_MAX_ITERATIONS:-3000}"

TB_PORT="${TRACTOR_PPO_TB_PORT:-6010}"
STREAMLIT_PORT="${TRACTOR_PPO_STREAMLIT_PORT:-8503}"
RUN_TAG="${TRACTOR_PPO_RUN_TAG:-phase1_macmini_$(date +%Y%m%d_%H%M%S)}"

CONFIG_DIR="$PROJECT_ROOT/rl_training/generated_configs"
RUN_LOG_DIR_REL="logs/$RUN_TAG"
RUN_CKPT_DIR_REL="checkpoints/$RUN_TAG"
RUN_LOG_DIR="$PROJECT_ROOT/$RUN_LOG_DIR_REL"
RUN_CKPT_DIR="$PROJECT_ROOT/$RUN_CKPT_DIR_REL"
SERVICE_LOG_DIR="$RUN_LOG_DIR/service_logs"
CONFIG_PATH="$CONFIG_DIR/${RUN_TAG}.yaml"

activate_venv() {
  # shellcheck disable=SC1090
  source "$VENV_DIR/bin/activate"
}

port_open() {
  local port="$1"
  python - "$port" <<'PY'
import socket
import sys

port = int(sys.argv[1])
sock = socket.socket()
sock.settimeout(0.5)
try:
    sock.connect(("127.0.0.1", port))
    print("OPEN")
except OSError:
    print("CLOSED")
finally:
    sock.close()
PY
}

ensure_paths() {
  [[ -f "$BASE_CONFIG" ]] || {
    echo "[error] base config not found: $BASE_CONFIG"
    exit 1
  }

  [[ -f "$RESUME_CHECKPOINT" ]] || {
    echo "[error] resume checkpoint not found: $RESUME_CHECKPOINT"
    exit 1
  }

  mkdir -p "$CONFIG_DIR" "$RUN_LOG_DIR" "$RUN_CKPT_DIR" "$SERVICE_LOG_DIR"
}

write_run_config() {
  activate_venv
  python - "$BASE_CONFIG" "$CONFIG_PATH" "$RUN_LOG_DIR_REL" "$RUN_CKPT_DIR_REL" "$MAX_ITERATIONS" <<'PY'
import sys
import yaml

base_config, out_config, log_dir, ckpt_dir, max_iterations = sys.argv[1:]
with open(base_config, encoding="utf-8") as f:
    cfg = yaml.safe_load(f)

cfg["training"]["max_iterations"] = int(max_iterations)
cfg["logging"]["log_dir"] = log_dir
cfg["logging"]["checkpoint_dir"] = ckpt_dir
cfg["logging"]["tensorboard_dir"] = f"{log_dir}/tb"

with open(out_config, "w", encoding="utf-8") as f:
    yaml.safe_dump(cfg, f, allow_unicode=True, sort_keys=False)
PY
}

start_tensorboard() {
  local status
  status="$(port_open "$TB_PORT")"
  if [[ "$status" == "OPEN" ]]; then
    echo "[service] TensorBoard already listening on port $TB_PORT"
    return
  fi

  echo "[service] Starting TensorBoard on 0.0.0.0:$TB_PORT"
  nohup python -m tensorboard.main \
    --logdir "$RUN_LOG_DIR/tb" \
    --host 0.0.0.0 \
    --port "$TB_PORT" \
    > "$SERVICE_LOG_DIR/tensorboard.log" 2>&1 &
  echo $! > "$SERVICE_LOG_DIR/tensorboard.pid"
}

start_streamlit() {
  local status
  status="$(port_open "$STREAMLIT_PORT")"
  if [[ "$status" == "OPEN" ]]; then
    echo "[service] Streamlit already listening on port $STREAMLIT_PORT"
    return
  fi

  echo "[service] Starting Streamlit on 0.0.0.0:$STREAMLIT_PORT"
  TRACTOR_PPO_LOG_DIR="$RUN_LOG_DIR" nohup python -m streamlit run \
    "$PROJECT_ROOT/rl_training/streamlit_phase1.py" \
    --server.headless true \
    --server.address 0.0.0.0 \
    --server.port "$STREAMLIT_PORT" \
    > "$SERVICE_LOG_DIR/streamlit.log" 2>&1 &
  echo $! > "$SERVICE_LOG_DIR/streamlit.pid"
}

start_training() {
  echo "[train] Starting resumed PPO training from $RESUME_CHECKPOINT"
  nohup python -u "$PROJECT_ROOT/rl_training/train_phase1.py" \
    --config "$CONFIG_PATH" \
    --resume "$RESUME_CHECKPOINT" \
    > "$RUN_LOG_DIR/run.out" 2>&1 &
  local train_pid=$!
  echo "$train_pid" > "$RUN_LOG_DIR/train.pid"

  if command -v caffeinate >/dev/null 2>&1; then
    nohup caffeinate -dimsu -w "$train_pid" > "$RUN_LOG_DIR/caffeinate.log" 2>&1 &
    echo $! > "$RUN_LOG_DIR/caffeinate.pid"
  fi
}

print_summary() {
  cat <<EOF

==========================================
Mac mini PPO resumed training started
==========================================
Run tag:       $RUN_TAG
Config:        $CONFIG_PATH
Checkpoint:    $RESUME_CHECKPOINT
Training log:  $RUN_LOG_DIR/training_log.csv
Eval summary:  $RUN_LOG_DIR/eval_summary.csv
Run output:    $RUN_LOG_DIR/run.out
Checkpoints:   $RUN_CKPT_DIR
TensorBoard:   http://<macmini-ip>:$TB_PORT
Streamlit:     http://<macmini-ip>:$STREAMLIT_PORT
==========================================

EOF
}

main() {
  cd "$PROJECT_ROOT"
  "$SETUP_SCRIPT"
  activate_venv
  ensure_paths
  write_run_config
  start_tensorboard
  start_streamlit
  start_training
  print_summary
}

main "$@"
