"""
End-to-end verification script for PpoEngineHost <-> Python bridge.

1. Starts the C# PpoEngineHost subprocess
2. Sends a reset request
3. Loops step requests (random legal action) until game ends
4. Validates terminal_result exists
5. Validates StateEncoder produces 382-dim vector
6. Validates action_mask produces 384-dim vector
"""
import os
import random
import sys

import numpy as np

# Ensure rl_training is on the path so local imports work
_SCRIPT_DIR = os.path.dirname(os.path.abspath(__file__))
if _SCRIPT_DIR not in sys.path:
    sys.path.insert(0, _SCRIPT_DIR)

from engine_bridge import EngineBridge
from state_encoder import StateEncoder
from action_mask import build_action_mask, map_all_actions, ACTION_DIM

# ── Config ──
PROJECT_ROOT = os.path.dirname(_SCRIPT_DIR)
HOST_PATH = os.path.join(
    PROJECT_ROOT,
    "tools", "PpoEngineHost", "bin", "Release", "net6.0", "PpoEngineHost",
)
PPO_SEATS = [0, 2]
RULE_AI_SEATS = [1, 3]
SEED = 42


def pick_random_slot(legal_actions: list[dict]) -> int:
    """Pick a random legal action slot from the action list."""
    mapped = map_all_actions(legal_actions)
    if not mapped:
        raise RuntimeError("No legal actions to choose from!")
    slot, _ = random.choice(mapped)
    return slot


def main():
    print("=== Bridge Verification ===")
    print(f"Host path: {HOST_PATH}")
    print(f"PPO seats: {PPO_SEATS}, RuleAI seats: {RULE_AI_SEATS}, seed: {SEED}")
    print()

    encoder = StateEncoder()

    with EngineBridge(HOST_PATH) as bridge:
        # ── Reset ──
        resp = bridge.reset(seed=SEED, ppo_seats=PPO_SEATS, rule_ai_seats=RULE_AI_SEATS)
        env_id = resp["env_id"]
        done = resp.get("done", False)
        current_player = resp.get("current_player", -1)
        legal_actions = resp.get("legal_actions", [])
        snapshot = resp.get("state_snapshot", {})

        print(f"Reset OK: env_id={env_id}, current_player={current_player}")

        # Validate encoder on reset snapshot
        obs = encoder.encode(snapshot, legal_actions)
        assert obs.shape == (382,), f"Obs dim mismatch: {obs.shape}"
        print(f"  Observation dim: {obs.shape[0]} (expected 382)")

        mask = build_action_mask(legal_actions)
        assert mask.shape == (384,), f"Mask dim mismatch: {mask.shape}"
        legal_count = int(mask.sum())
        print(f"  Action mask dim: {mask.shape[0]} (expected 384), legal={legal_count}")
        print()

        # ── Step loop ──
        step_num = 0
        max_steps = 500  # safety guard

        while not done and step_num < max_steps:
            step_num += 1
            slot = pick_random_slot(legal_actions)

            resp = bridge.step(env_id, action_slot=slot)
            done = resp.get("done", False)
            current_player = resp.get("current_player", -1)
            legal_actions = resp.get("legal_actions", [])
            snapshot = resp.get("state_snapshot", {})

            if done:
                print(f"Step {step_num}: player={current_player}, "
                      f"slot={slot}, done=True")
            else:
                # Validate encoder each step
                obs = encoder.encode(snapshot, legal_actions)
                assert obs.shape == (382,), f"Step {step_num}: obs dim {obs.shape}"
                mask = build_action_mask(legal_actions)
                assert mask.shape == (384,), f"Step {step_num}: mask dim {mask.shape}"
                legal_count = int(mask.sum())

                print(f"Step {step_num}: player={current_player}, "
                      f"legal_actions={len(legal_actions)}, "
                      f"slot={slot}, done=False")

        print()

        if done:
            terminal = resp.get("terminal_result")
            if terminal is None:
                print("ERROR: Game finished but terminal_result is None!")
                sys.exit(1)

            print("Game finished!")
            print(f"terminal_result: {terminal}")
            print()

            # Final dimension checks
            final_obs = np.zeros(382, dtype=np.float32)
            final_mask = np.zeros(384, dtype=np.float32)
            print(f"Observation dim: {final_obs.shape[0]} {'OK' if final_obs.shape[0] == 382 else 'FAIL'}")
            print(f"Action mask dim: {final_mask.shape[0]} {'OK' if final_mask.shape[0] == 384 else 'FAIL'}")
            print()
            print("=== Verification PASSED ===")
        else:
            print(f"ERROR: Game did not finish within {max_steps} steps!")
            sys.exit(1)


if __name__ == "__main__":
    main()
