#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

VENV_DIR="${TRACTOR_PPO_VENV:-$PROJECT_ROOT/.venv_phase2}"
TB_PORT="${TRACTOR_PPO_TB_PORT:-6007}"
STREAMLIT_PORT="${TRACTOR_PPO_STREAMLIT_PORT:-8502}"
SERVICE_LOG_DIR="$PROJECT_ROOT/logs/phase1/service_logs"
TRAINING_LOG_DIR="$PROJECT_ROOT/logs/phase1"
REQ_FILE="$PROJECT_ROOT/rl_training/requirements.txt"

mkdir -p "$SERVICE_LOG_DIR"
mkdir -p "$TRAINING_LOG_DIR"

activate_venv() {
  # shellcheck disable=SC1090
  source "$VENV_DIR/bin/activate"
}

ensure_venv() {
  if [[ ! -x "$VENV_DIR/bin/python" ]]; then
    echo "[setup] Creating virtual environment at $VENV_DIR"
    python3 -m venv "$VENV_DIR"
  fi

  activate_venv

  if [[ "${TRACTOR_PPO_SKIP_INSTALL:-0}" != "1" ]]; then
    echo "[setup] Installing Python dependencies"
    python -m pip install --upgrade pip
    python -m pip install -r "$REQ_FILE"
    python -m pip install "setuptools<81"
  else
    echo "[setup] Skipping dependency install because TRACTOR_PPO_SKIP_INSTALL=1"
  fi
}

ensure_eval_seeds() {
  echo "[setup] Ensuring evaluation seeds exist"
  python "$PROJECT_ROOT/rl_training/generate_eval_seeds.py"
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

start_tensorboard() {
  local status
  status="$(port_open "$TB_PORT")"
  if [[ "$status" == "OPEN" ]]; then
    echo "[service] TensorBoard already listening on http://127.0.0.1:$TB_PORT"
    return
  fi

  echo "[service] Starting TensorBoard on http://127.0.0.1:$TB_PORT"
  nohup python -m tensorboard.main \
    --logdir "$PROJECT_ROOT/logs/phase1/tb" \
    --host 127.0.0.1 \
    --port "$TB_PORT" \
    > "$SERVICE_LOG_DIR/tensorboard.log" 2>&1 &
  echo $! > "$SERVICE_LOG_DIR/tensorboard.pid"
}

start_streamlit() {
  local status
  status="$(port_open "$STREAMLIT_PORT")"
  if [[ "$status" == "OPEN" ]]; then
    echo "[service] Streamlit already listening on http://127.0.0.1:$STREAMLIT_PORT"
    return
  fi

  echo "[service] Starting Streamlit on http://127.0.0.1:$STREAMLIT_PORT"
  nohup python -m streamlit run "$PROJECT_ROOT/rl_training/streamlit_phase1.py" \
    --server.headless true \
    --server.address 127.0.0.1 \
    --server.port "$STREAMLIT_PORT" \
    > "$SERVICE_LOG_DIR/streamlit.log" 2>&1 &
  echo $! > "$SERVICE_LOG_DIR/streamlit.pid"
}

print_summary() {
  cat <<EOF

==========================================
Tractor PPO AI - Phase 2 Visual Training
==========================================
Virtual env:   $VENV_DIR
Training log:  $PROJECT_ROOT/logs/phase1/training_log.csv
Eval summary:  $PROJECT_ROOT/logs/phase1/eval_summary.csv
Eval matches:  $PROJECT_ROOT/logs/phase1/eval_match_results.jsonl
TensorBoard:   http://127.0.0.1:$TB_PORT
Streamlit:     http://127.0.0.1:$STREAMLIT_PORT
TB log file:   $SERVICE_LOG_DIR/tensorboard.log
ST log file:   $SERVICE_LOG_DIR/streamlit.log

Training command:
  python rl_training/train_phase1.py $*
==========================================

EOF
}

main() {
  cd "$PROJECT_ROOT"
  ensure_venv
  ensure_eval_seeds
  start_tensorboard
  start_streamlit
  print_summary "$@"

  echo "[train] Starting Phase 1 training..."
  python rl_training/train_phase1.py "$@"
}

main "$@"
