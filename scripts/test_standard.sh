#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/TractorGame.csproj"

FILTER='Category!=SelfPlay&Category!=Campaign&Category!=LongRunning&Category!=UI'

dotnet test "${PROJECT_PATH}" --no-restore --filter "${FILTER}" -v minimal
