#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "$0")/.." && pwd)"
DOTNET_BIN="${DOTNET_BIN:-$HOME/.dotnet/dotnet}"
TZ_NAME="${TRAIN_TZ:-Asia/Hong_Kong}"
RUN_LOG_DIR="$ROOT_DIR/data/evolution"
RUN_LOG="$RUN_LOG_DIR/train_until_7am.log"
mkdir -p "$RUN_LOG_DIR"

if [[ ! -x "$DOTNET_BIN" ]]; then
  echo "dotnet not found at $DOTNET_BIN" | tee -a "$RUN_LOG"
  exit 1
fi

# Default deadline: tomorrow 07:00 in Asia/Hong_Kong.
if [[ $# -ge 1 ]]; then
  DEADLINE_LOCAL="$1"
else
  DEADLINE_LOCAL="$(python3 - <<'PY'
from datetime import datetime, timedelta
from zoneinfo import ZoneInfo
now = datetime.now(ZoneInfo('Asia/Hong_Kong'))
tomorrow = now + timedelta(days=1)
print(tomorrow.replace(hour=7, minute=0, second=0, microsecond=0).isoformat())
PY
)"
fi

DEADLINE_EPOCH="$(python3 - <<PY
from datetime import datetime
from zoneinfo import ZoneInfo
raw = "$DEADLINE_LOCAL"
if raw.endswith('Z'):
    dt = datetime.fromisoformat(raw.replace('Z', '+00:00'))
else:
    dt = datetime.fromisoformat(raw)
if dt.tzinfo is None:
    dt = dt.replace(tzinfo=ZoneInfo('$TZ_NAME'))
print(int(dt.timestamp()))
PY
)"

{
  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] training loop start"
  echo "deadline_local=$DEADLINE_LOCAL"
  echo "deadline_epoch=$DEADLINE_EPOCH"
} >> "$RUN_LOG"

ITER=0
while true; do
  NOW_EPOCH="$(python3 - <<PY
import time
print(int(time.time()))
PY
)"

  if (( NOW_EPOCH >= DEADLINE_EPOCH )); then
    break
  fi

  ITER=$((ITER+1))
  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] round=$ITER start" >> "$RUN_LOG"

  if ! "$DOTNET_BIN" test "$ROOT_DIR/TractorGame.csproj" \
      --filter "FullyQualifiedName=TractorGame.Tests.Evolution.TrainingHarnessTests.TrainOneGeneration_Harness" \
      --logger "console;verbosity=minimal" >> "$RUN_LOG" 2>&1; then
    echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] round=$ITER failed, sleep 30s" >> "$RUN_LOG"
    sleep 30
    continue
  fi

  echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] round=$ITER done" >> "$RUN_LOG"
  sleep 3
 done

SUMMARY_PATH="$ROOT_DIR/data/evolution/reports/train_until_summary_$(date +%Y%m%d_%H%M%S).md"
python3 - <<PY
from pathlib import Path
import re
root = Path('$ROOT_DIR')
reports = sorted((root / 'data' / 'evolution' / 'reports').glob('generation_*_report.md'))
summary = Path('$SUMMARY_PATH')
if not reports:
    summary.write_text('# Training Summary\n\nNo generation report found.\n', encoding='utf-8')
    raise SystemExit(0)

latest = reports[-1]
text = latest.read_text(encoding='utf-8-sig')
items = {}
for key in ['Candidate count', 'Promoted', 'Reason', 'Champion before', 'Champion after']:
    m = re.search(rf'- {re.escape(key)}: (.+)', text)
    items[key] = m.group(1) if m else 'N/A'
best = 'N/A'
if '## Layer3' in text:
    for line in text.split('## Layer3', 1)[1].splitlines():
        if line.startswith('| gen_'):
            best = line
            break

lines = [
    '# Training Summary',
    '',
    f'- Latest report: {latest}',
    f"- Candidate count: {items['Candidate count']}",
    f"- Promoted: {items['Promoted']}",
    f"- Reason: {items['Reason']}",
    f"- Champion before: {items['Champion before']}",
    f"- Champion after: {items['Champion after']}",
    '',
    '## Best Layer3 Row',
    '',
    best,
    ''
]
summary.write_text('\n'.join(lines), encoding='utf-8')
PY

echo "[$(date -u +%Y-%m-%dT%H:%M:%SZ)] training loop finished, summary=$SUMMARY_PATH" >> "$RUN_LOG"
echo "$SUMMARY_PATH"
