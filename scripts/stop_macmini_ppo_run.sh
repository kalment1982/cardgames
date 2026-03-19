#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

TARGET="${1:-}"
if [[ -z "$TARGET" ]]; then
  echo "usage: $0 <run-tag-or-log-dir>"
  exit 1
fi

if [[ -d "$TARGET" ]]; then
  RUN_LOG_DIR="$TARGET"
else
  RUN_LOG_DIR="$PROJECT_ROOT/logs/$TARGET"
fi

[[ -d "$RUN_LOG_DIR" ]] || {
  echo "[error] run log directory not found: $RUN_LOG_DIR"
  exit 1
}

stop_from_pid_file() {
  local label="$1"
  local pid_file="$2"

  if [[ ! -f "$pid_file" ]]; then
    return
  fi

  local pid
  pid="$(cat "$pid_file")"
  if [[ -n "$pid" ]] && kill -0 "$pid" 2>/dev/null; then
    kill "$pid" 2>/dev/null || true
    sleep 1
    if kill -0 "$pid" 2>/dev/null; then
      kill -9 "$pid" 2>/dev/null || true
    fi
    echo "[stop] $label pid=$pid"
  fi
}

stop_from_pid_file "training" "$RUN_LOG_DIR/train.pid"
stop_from_pid_file "tensorboard" "$RUN_LOG_DIR/service_logs/tensorboard.pid"
stop_from_pid_file "streamlit" "$RUN_LOG_DIR/service_logs/streamlit.pid"
stop_from_pid_file "caffeinate" "$RUN_LOG_DIR/caffeinate.pid"

echo "[done] stop request processed for $RUN_LOG_DIR"
