"""
TractorEnv — Gym-like environment wrapping the C# PpoEngineHost.

Provides ``reset()`` / ``step()`` / ``close()`` with observation vectors
and action masks ready for PPO training.
"""
import logging
from typing import Optional

import numpy as np

from engine_bridge import EngineBridge
from state_encoder import StateEncoder
from action_mask import build_action_mask, ACTION_DIM

logger = logging.getLogger(__name__)


class TractorEnv:
    """Single-environment wrapper around the C# engine bridge."""

    def __init__(
        self,
        host_path: str,
        ppo_seats: list[int] | None = None,
        rule_ai_seats: list[int] | None = None,
    ):
        if ppo_seats is None:
            ppo_seats = [0, 2]
        if rule_ai_seats is None:
            rule_ai_seats = [1, 3]

        self.ppo_seats = ppo_seats
        self.rule_ai_seats = rule_ai_seats
        self.bridge = EngineBridge(host_path)
        self.encoder = StateEncoder()

        self._env_id: Optional[str] = None
        self._done = False
        self._seed_counter = 0
        self._last_legal_actions: list[dict] = []
        self._last_snapshot: dict = {}

    @property
    def observation_dim(self) -> int:
        return StateEncoder.OBS_DIM

    @property
    def action_dim(self) -> int:
        return ACTION_DIM

    def reset(self, seed: int | None = None) -> tuple[np.ndarray, np.ndarray]:
        """Reset the environment.

        Returns:
            (observation, legal_mask) — both numpy arrays.
        """
        if seed is None:
            seed = self._seed_counter
            self._seed_counter += 1

        # Close previous environment if any
        if self._env_id is not None:
            try:
                self.bridge.close(env_id=self._env_id)
            except Exception:
                pass
            self._env_id = None

        resp = self.bridge.reset(seed, self.ppo_seats, self.rule_ai_seats)
        self._env_id = resp["env_id"]
        self._done = resp.get("done", False)
        self._last_legal_actions = resp.get("legal_actions", [])
        self._last_snapshot = resp.get("state_snapshot", {})

        obs = self.encoder.encode(self._last_snapshot, self._last_legal_actions)
        mask = build_action_mask(self._last_legal_actions)
        return obs, mask

    def step(self, action_slot: int) -> tuple[np.ndarray, float, bool, dict]:
        """Take one action in the environment.

        Args:
            action_slot: Index into the 384-dim action space.

        Returns:
            (observation, reward, done, info)
        """
        if self._done:
            raise RuntimeError("Episode already finished. Call reset().")
        if self._env_id is None:
            raise RuntimeError("No active environment. Call reset() first.")

        resp = self.bridge.step(self._env_id, action_slot)
        done = resp.get("done", False)
        self._done = done
        self._last_legal_actions = resp.get("legal_actions", [])
        self._last_snapshot = resp.get("state_snapshot", {})

        info: dict = {}
        reward = 0.0

        if done:
            terminal = resp.get("terminal_result")
            if terminal is not None:
                info["terminal_result"] = terminal
                reward = self._compute_reward(terminal)
            # Return a zero observation when done
            obs = np.zeros(self.encoder.OBS_DIM, dtype=np.float32)
            mask = np.zeros(ACTION_DIM, dtype=np.float32)
        else:
            obs = self.encoder.encode(self._last_snapshot, self._last_legal_actions)
            mask = build_action_mask(self._last_legal_actions)

        info["legal_mask"] = mask
        return obs, reward, done, info

    def get_teacher_action(self) -> dict:
        """Return the RuleAI teacher action for the current PPO seat."""
        if self._done:
            raise RuntimeError("Episode already finished. Call reset().")
        if self._env_id is None:
            raise RuntimeError("No active environment. Call reset() first.")
        resp = self.bridge.get_teacher_action(self._env_id)
        teacher = resp.get("teacher_action")
        if not teacher:
            raise RuntimeError("Teacher action unavailable for current environment state.")
        return teacher

    def close(self):
        """Shut down the C# host process."""
        try:
            self.bridge.close(scope="host")
        except Exception:
            pass

    def __enter__(self):
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.close()
        return False

    # ── reward shaping ──

    @staticmethod
    def _compute_reward(terminal: dict) -> float:
        """Compute reward from terminal result.

        R = (+10 if win else -10) + 2 * level_gain + 0.02 * final_score
        """
        won = terminal.get("my_team_won", False)
        level_gain = terminal.get("my_team_level_gain", 0)
        final_score = terminal.get("my_team_final_score", 0)

        r = 10.0 if won else -10.0
        r += 2.0 * level_gain
        r += 0.02 * final_score
        return r
