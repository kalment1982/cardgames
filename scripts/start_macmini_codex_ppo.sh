#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"

command -v codex >/dev/null 2>&1 || {
  echo "[error] codex not found"
  exit 1
}

cat <<'EOF' | codex exec \
  -C "$PROJECT_ROOT" \
  --dangerously-bypass-approvals-and-sandbox \
  --search \
  -
Use the repository's prepared Mac mini PPO scripts to start a resumed PPO training run on this machine.

Requirements:
- Do not edit repository files.
- Do not stop unrelated running services.
- If the default resume checkpoint is missing, report that clearly and stop.

Tasks:
1. Inspect `scripts/setup_macmini_ppo.sh` and `scripts/start_macmini_ppo_resume.sh`.
2. Run the setup script if needed, then run the resume-training script.
3. Verify that the new run's `run.out` is actively growing.
4. Report the run tag, log directory, checkpoint directory, PID files, and the TensorBoard / Streamlit URLs.
EOF
