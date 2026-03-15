#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
PROJECT_PATH="${REPO_ROOT}/WebUI/WebUI.csproj"
HOST_PROJECT_PATH="${REPO_ROOT}/WebUIHost/WebUIHost.csproj"

HOST="${WEBUI_HOST:-127.0.0.1}"
PORT="${WEBUI_PORT:-5167}"
WATCH_MODE=0
KILL_EXISTING=0

usage() {
  cat <<EOF
Usage: $(basename "$0") [--watch] [--host HOST] [--port PORT] [--kill-existing]

Start Tractor WebUI.

Options:
  --watch       Start with dotnet watch run
  --host HOST   Bind host (default: ${HOST})
  --port PORT   Bind port (default: ${PORT})
  --kill-existing  Kill process already listening on target port
  -h, --help    Show help

Environment:
  WEBUI_HOST    Default host if --host is not provided
  WEBUI_PORT    Default port if --port is not provided
EOF
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --watch)
      WATCH_MODE=1
      shift
      ;;
    --host)
      HOST="${2:-}"
      shift 2
      ;;
    --port)
      PORT="${2:-}"
      shift 2
      ;;
    --kill-existing)
      KILL_EXISTING=1
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      echo "Unknown option: $1" >&2
      usage
      exit 1
      ;;
  esac
done

if [[ -z "${HOST}" || -z "${PORT}" ]]; then
  echo "Host and port must not be empty." >&2
  exit 1
fi

if [[ ! -f "${PROJECT_PATH}" ]]; then
  echo "WebUI project not found: ${PROJECT_PATH}" >&2
  exit 1
fi

if [[ ! -f "${HOST_PROJECT_PATH}" ]]; then
  echo "WebUI host project not found: ${HOST_PROJECT_PATH}" >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "dotnet SDK is not installed or not in PATH." >&2
  exit 1
fi

URL="http://${HOST}:${PORT}"

find_port_pid() {
  lsof -nP -tiTCP:"${PORT}" -sTCP:LISTEN 2>/dev/null | head -n 1 || true
}

EXISTING_PID="$(find_port_pid)"
if [[ -n "${EXISTING_PID}" ]]; then
  if [[ "${KILL_EXISTING}" -eq 1 ]]; then
    echo "Port ${PORT} is in use by PID ${EXISTING_PID}, killing it..."
    kill "${EXISTING_PID}" || true
    sleep 0.3
    # hard kill if still alive
    if kill -0 "${EXISTING_PID}" 2>/dev/null; then
      kill -9 "${EXISTING_PID}" || true
    fi
  else
    echo "Port ${PORT} is already in use (PID ${EXISTING_PID})." >&2
    echo "Use one of:" >&2
    echo "  1) Kill it: kill ${EXISTING_PID}" >&2
    echo "  2) Run with another port: $(basename "$0") --port 5168" >&2
    echo "  3) Auto kill and start: $(basename "$0") --kill-existing" >&2
    exit 1
  fi
fi

echo "Starting WebUI..."
echo "Project: ${PROJECT_PATH}"
echo "Host: ${HOST_PROJECT_PATH}"
echo "URL: ${URL}"

echo "Building WebUI static assets..."
dotnet build "${PROJECT_PATH}"

if [[ "${WATCH_MODE}" -eq 1 ]]; then
  exec dotnet watch --project "${HOST_PROJECT_PATH}" run --urls "${URL}"
else
  exec dotnet run --project "${HOST_PROJECT_PATH}" --urls "${URL}"
fi
