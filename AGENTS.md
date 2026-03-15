# Agent Notes

This repository writes two default game logs.  
All coding agents (Codex/Claude/etc.) should check these paths first when debugging gameplay flow:

## Log Paths

1. Machine-readable audit logs (JSONL):
- `/Users/karmy/Projects/CardGame/tractor/logs/raw`
- File pattern: `tractor-YYYY-MM-DD-HH.jsonl`

2. Human-readable replay logs (Markdown):
- `/Users/karmy/Projects/CardGame/tractor/logs/replay`

## Startup Recommendation

To avoid writing logs into unexpected locations, set:

```bash
export TRACTOR_LOG_ROOT=/Users/karmy/Projects/CardGame/tractor/logs/raw
```

Then start WebUI.

## Runtime Logging Note

For browser gameplay, use:

```bash
/Users/karmy/Projects/CardGame/tractor/scripts/run_webui.sh --kill-existing
```

This starts the integrated host (`WebUIHost`) on the same WebUI port and accepts in-game log relay at:
- `POST /api/log-entry`
