"""
EngineBridge — C# PpoEngineHost subprocess bridge.

Communicates with the C# host via stdin/stdout JSON lines.
"""
import json
import logging
import subprocess
import uuid
from typing import Optional

logger = logging.getLogger(__name__)


class EngineBridgeError(Exception):
    """Raised when the C# host returns ok=false."""

    def __init__(self, error_code: str, error_message: str):
        self.error_code = error_code
        self.error_message = error_message
        super().__init__(f"[{error_code}] {error_message}")


class EngineBridge:
    """Manages a C# PpoEngineHost subprocess and provides a JSON-RPC-like API."""

    def __init__(self, host_path: str):
        """Start the C# PpoEngineHost process.

        Args:
            host_path: Path to the PpoEngineHost executable (or ``dotnet run``
                       project directory).
        """
        self._host_path = host_path
        self._proc: Optional[subprocess.Popen] = None
        self._start_process()

    # ── lifecycle ──

    def _start_process(self):
        cmd = self._build_command()
        logger.info("Starting PpoEngineHost: %s", " ".join(cmd))
        self._proc = subprocess.Popen(
            cmd,
            stdin=subprocess.PIPE,
            stdout=subprocess.PIPE,
            stderr=subprocess.PIPE,
            text=True,
            bufsize=1,  # line-buffered
        )

    def _build_command(self) -> list[str]:
        path = self._host_path
        if path.endswith(".dll"):
            return ["dotnet", path]
        if path.endswith(".csproj") or path.endswith("/") or path.endswith("\\"):
            return ["dotnet", "run", "--project", path]
        # Assume it is a native executable
        return [path]

    # ── public API ──

    def reset(self, seed: int, ppo_seats: list[int], rule_ai_seats: list[int]) -> dict:
        """Send a reset request. Returns the full response dict."""
        req = {
            "type": "reset",
            "request_id": self._next_id(),
            "seed": seed,
            "ppo_seats": ppo_seats,
            "rule_ai_seats": rule_ai_seats,
        }
        return self._send(req)

    def step(self, env_id: str, action_slot: int) -> dict:
        """Send a step request. Returns the full response dict."""
        req = {
            "type": "step",
            "request_id": self._next_id(),
            "env_id": env_id,
            "action_slot": action_slot,
        }
        return self._send(req)

    def get_legal_actions(self, env_id: str) -> dict:
        """Fetch legal actions for the current player."""
        req = {
            "type": "get_legal_actions",
            "request_id": self._next_id(),
            "env_id": env_id,
        }
        return self._send(req)

    def get_teacher_action(self, env_id: str) -> dict:
        """Fetch the RuleAI-selected action for the current PPO player."""
        req = {
            "type": "get_teacher_action",
            "request_id": self._next_id(),
            "env_id": env_id,
        }
        return self._send(req)

    def get_state_snapshot(self, env_id: str) -> dict:
        """Fetch the state snapshot for the current player."""
        req = {
            "type": "get_state_snapshot",
            "request_id": self._next_id(),
            "env_id": env_id,
        }
        return self._send(req)

    def close(self, env_id: str | None = None, scope: str | None = None):
        """Close a specific environment or the entire host.

        Args:
            env_id: If provided, close only this environment.
            scope: If ``"host"``, shut down the whole process.
        """
        req: dict = {
            "type": "close",
            "request_id": self._next_id(),
        }
        if env_id is not None:
            req["env_id"] = env_id
        if scope is not None:
            req["scope"] = scope
        try:
            return self._send(req)
        except (BrokenPipeError, OSError):
            # Process may have already exited after scope=host
            pass
        finally:
            if scope == "host":
                self._cleanup()

    # ── context manager ──

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close(scope="host")
        return False

    # ── internal ──

    def _next_id(self) -> str:
        return uuid.uuid4().hex[:12]

    def _send(self, request: dict) -> dict:
        """Write one JSON line to stdin, read one JSON line from stdout."""
        proc = self._proc
        if proc is None or proc.poll() is not None:
            raise RuntimeError("PpoEngineHost process is not running.")

        line = json.dumps(request, ensure_ascii=False) + "\n"
        try:
            proc.stdin.write(line)
            proc.stdin.flush()
        except (BrokenPipeError, OSError) as exc:
            raise RuntimeError(f"Failed to write to PpoEngineHost stdin: {exc}") from exc

        resp_line = proc.stdout.readline()
        if not resp_line:
            stderr_tail = ""
            if proc.stderr:
                stderr_tail = proc.stderr.read(4096)
            raise RuntimeError(
                f"PpoEngineHost returned empty response (process exited?). "
                f"stderr: {stderr_tail}"
            )

        try:
            resp = json.loads(resp_line)
        except json.JSONDecodeError as exc:
            raise RuntimeError(
                f"Invalid JSON from PpoEngineHost: {resp_line!r}"
            ) from exc

        if not resp.get("ok", False):
            raise EngineBridgeError(
                resp.get("error_code", "UNKNOWN"),
                resp.get("error_message", resp_line.strip()),
            )

        return resp

    def _cleanup(self):
        proc = self._proc
        if proc is None:
            return
        try:
            proc.stdin.close()
        except OSError:
            pass
        try:
            proc.wait(timeout=5)
        except subprocess.TimeoutExpired:
            proc.kill()
            proc.wait(timeout=3)
        self._proc = None
