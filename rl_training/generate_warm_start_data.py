#!/usr/bin/env python3
"""
Generate RuleAI V2.1 warm-start demonstrations for PPO.

Scope:
- data source: current C# RuleAI V2.1
- phase: PlayTricks only
- samples: current PPO seats only
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import yaml

from mvp_env import TractorEnv


DEFAULT_CONFIG = Path(__file__).with_name("phase1_config.yaml")


def load_config(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return yaml.safe_load(f)


def resolve_device_independent_host_path(cfg: dict) -> str:
    project_root = Path(__file__).resolve().parent.parent
    return str(project_root / cfg["environment"]["host_path"])


def main():
    parser = argparse.ArgumentParser(description="Generate PPO warm-start data from RuleAI V2.1")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG), help="Path to phase1_config.yaml")
    parser.add_argument("--num_games", type=int, default=None, help="Number of games to collect")
    parser.add_argument("--output", type=str, default=None, help="Output .npz dataset path")
    parser.add_argument("--seed_start", type=int, default=0, help="First environment seed")
    parser.add_argument("--max_steps", type=int, default=None, help="Max decisions per game")
    args = parser.parse_args()

    cfg = load_config(args.config)
    warm_cfg = cfg.get("warm_start", {})
    train_cfg = cfg["training"]
    env_cfg = cfg["environment"]

    num_games = args.num_games or warm_cfg.get("num_games", 2000)
    max_steps = args.max_steps or train_cfg.get("max_steps_per_game", 500)

    project_root = Path(__file__).resolve().parent.parent
    default_output = project_root / warm_cfg.get(
        "dataset_path",
        "artifacts/ppo_warm_start/ruleai_v21_playtricks_2000g.npz",
    )
    output_path = Path(args.output) if args.output else default_output
    output_path.parent.mkdir(parents=True, exist_ok=True)

    host_path = resolve_device_independent_host_path(cfg)
    ppo_seats = env_cfg["ppo_seats"]
    rule_ai_seats = env_cfg["rule_ai_seats"]

    states: list[np.ndarray] = []
    masks: list[np.ndarray] = []
    actions: list[int] = []
    seats: list[int] = []
    trick_indices: list[int] = []
    play_positions: list[int] = []
    seed_values: list[int] = []

    games_completed = 0
    sample_count = 0

    with TractorEnv(host_path=host_path, ppo_seats=ppo_seats, rule_ai_seats=rule_ai_seats) as env:
        for game_offset in range(num_games):
            seed = args.seed_start + game_offset
            obs, mask = env.reset(seed=seed)

            for _ in range(max_steps):
                teacher = env.get_teacher_action()
                slot = int(teacher["slot"])
                snapshot = env._last_snapshot  # intentional internal access for dataset metadata

                if slot < 0 or slot >= env.action_dim:
                    raise RuntimeError(f"Teacher slot out of range: {slot}")
                if mask[slot] < 0.5:
                    raise RuntimeError(f"Teacher slot {slot} not legal under current mask")

                states.append(obs.astype(np.float32, copy=True))
                masks.append(mask.astype(np.float32, copy=True))
                actions.append(slot)
                seats.append(int(snapshot.get("my_seat", -1)))
                trick_indices.append(int(snapshot.get("trick_index", -1)))
                play_positions.append(int(snapshot.get("play_position", -1)))
                seed_values.append(seed)
                sample_count += 1

                next_obs, _reward, done, info = env.step(slot)
                if done:
                    games_completed += 1
                    break

                obs = next_obs
                mask = info["legal_mask"]
            else:
                raise RuntimeError(f"Game seed {seed} exceeded max_steps={max_steps}")

            if (game_offset + 1) % 100 == 0 or game_offset + 1 == num_games:
                print(
                    f"Collected {game_offset + 1}/{num_games} games, "
                    f"{sample_count} samples"
                )

    np.savez_compressed(
        output_path,
        states=np.asarray(states, dtype=np.float32),
        masks=np.asarray(masks, dtype=np.float32),
        actions=np.asarray(actions, dtype=np.int64),
        seats=np.asarray(seats, dtype=np.int8),
        trick_indices=np.asarray(trick_indices, dtype=np.int16),
        play_positions=np.asarray(play_positions, dtype=np.int8),
        seeds=np.asarray(seed_values, dtype=np.int32),
    )

    metadata = {
        "dataset_path": str(output_path),
        "num_games": games_completed,
        "num_samples": sample_count,
        "seed_start": args.seed_start,
        "ppo_seats": ppo_seats,
        "rule_ai_seats": rule_ai_seats,
        "host_path": host_path,
        "state_dim": int(np.asarray(states[0]).shape[0]) if states else 0,
        "action_dim": int(np.asarray(masks[0]).shape[0]) if masks else 0,
        "source": "RuleAI V2.1",
        "phase": "PlayTricks",
    }

    metadata_path = output_path.with_suffix(".json")
    with open(metadata_path, "w", encoding="utf-8") as f:
        json.dump(metadata, f, ensure_ascii=False, indent=2)

    print(f"Saved dataset: {output_path}")
    print(f"Saved metadata: {metadata_path}")
    print(f"Games: {games_completed}, Samples: {sample_count}")


if __name__ == "__main__":
    main()
