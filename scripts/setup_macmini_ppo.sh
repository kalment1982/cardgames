#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

VENV_DIR="${TRACTOR_PPO_VENV:-$PROJECT_ROOT/.venv_phase2}"
REQ_FILE="$PROJECT_ROOT/rl_training/requirements.txt"
HOST_PROJECT="$PROJECT_ROOT/tools/PpoEngineHost/PpoEngineHost.csproj"

activate_venv() {
  # shellcheck disable=SC1090
  source "$VENV_DIR/bin/activate"
}

ensure_prereqs() {
  command -v python3 >/dev/null 2>&1 || {
    echo "[error] python3 not found"
    exit 1
  }

  command -v dotnet >/dev/null 2>&1 || {
    echo "[error] dotnet not found"
    exit 1
  }
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

build_host() {
  echo "[setup] Building PpoEngineHost"
  dotnet build "$HOST_PROJECT" -c Release
}

ensure_eval_seeds() {
  echo "[setup] Ensuring evaluation seeds exist"
  activate_venv
  python "$PROJECT_ROOT/rl_training/generate_eval_seeds.py"
}

main() {
  cd "$PROJECT_ROOT"
  ensure_prereqs
  ensure_venv
  build_host
  ensure_eval_seeds

  cat <<EOF

==========================================
Mac mini PPO setup complete
==========================================
Project root: $PROJECT_ROOT
Virtual env:  $VENV_DIR
Host binary:  $PROJECT_ROOT/tools/PpoEngineHost/bin/Release/net6.0/PpoEngineHost
Eval seeds:   $PROJECT_ROOT/rl_training/eval_seeds.txt
==========================================

EOF
}

main "$@"
