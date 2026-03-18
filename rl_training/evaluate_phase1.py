"""
Standalone evaluation script for Phase 1 PPO checkpoints.

Usage:
    python evaluate_phase1.py --checkpoint checkpoints/phase1/best_model.pt
"""
import argparse
import logging
import os
import sys
from pathlib import Path

import numpy as np
import torch
import yaml

sys.path.insert(0, os.path.dirname(__file__))

from ppo_agent import PPOAgent
from mvp_env import TractorEnv

ACTION_DIM = 384
DEFAULT_CONFIG = str(Path(__file__).with_name("phase1_config.yaml"))


def load_config(path: str) -> dict:
    with open(path) as f:
        return yaml.safe_load(f)


def load_eval_seeds(path: str) -> list[int]:
    with open(path) as f:
        return [int(line.strip()) for line in f if line.strip()]


def evaluate(
    env: TractorEnv,
    agent: PPOAgent,
    seeds: list[int],
    max_steps: int = 500,
    verbose: bool = False,
) -> dict:
    """Evaluate agent deterministically on fixed seeds."""
    wins = 0
    total_reward = 0.0
    illegal_count = 0
    total_actions = 0
    game_rewards = []

    for i, seed in enumerate(seeds):
        obs, mask = env.reset(seed=seed)
        game_reward = 0.0
        game_illegal = 0

        for _step in range(max_steps):
            action, _, _ = agent.select_action(obs, mask.astype(bool), deterministic=True)
            total_actions += 1

            if mask[action] < 0.5:
                illegal_count += 1
                game_illegal += 1

            next_obs, reward, done, info = env.step(action)
            next_mask = info.get("legal_mask", np.zeros(ACTION_DIM, dtype=np.float32))

            if done:
                game_reward = reward
                total_reward += reward
                if reward > 0:
                    wins += 1
                break

            obs, mask = next_obs, next_mask

        game_rewards.append(game_reward)

        if verbose:
            result = "WIN" if game_reward > 0 else "LOSS"
            print(f"  Game {i+1:3d} seed={seed:10d} -> {result} "
                  f"reward={game_reward:+.2f} illegal={game_illegal}")

    n = len(seeds)
    rewards_arr = np.array(game_rewards)
    return {
        "win_rate": wins / max(n, 1),
        "avg_reward": total_reward / max(n, 1),
        "reward_std": float(rewards_arr.std()) if n > 1 else 0.0,
        "illegal_rate": illegal_count / max(total_actions, 1),
        "wins": wins,
        "total_games": n,
        "illegal_count": illegal_count,
        "total_actions": total_actions,
    }


def main():
    parser = argparse.ArgumentParser(description="Evaluate Phase 1 PPO checkpoint")
    parser.add_argument(
        "--checkpoint", required=True,
        help="Path to .pt checkpoint file",
    )
    parser.add_argument(
        "--config", default=DEFAULT_CONFIG,
        help="Path to YAML config file",
    )
    parser.add_argument(
        "--num_games", type=int, default=None,
        help="Override number of eval games (default: use all seeds)",
    )
    parser.add_argument(
        "--verbose", action="store_true",
        help="Print per-game results",
    )
    args = parser.parse_args()

    logging.basicConfig(level=logging.WARNING)

    cfg = load_config(args.config)
    p_cfg = cfg["ppo"]
    e_cfg = cfg["environment"]
    ev_cfg = cfg["evaluation"]

    project_root = Path(__file__).resolve().parent.parent
    host_path = str(project_root / e_cfg["host_path"])

    # Load eval seeds
    seeds_path = str(project_root / ev_cfg["eval_seeds_file"])
    if not os.path.exists(seeds_path):
        print(f"Eval seeds not found at {seeds_path}, generating...")
        from generate_eval_seeds import main as gen_seeds
        gen_seeds()
    eval_seeds = load_eval_seeds(seeds_path)

    if args.num_games:
        eval_seeds = eval_seeds[:args.num_games]

    # Device
    if torch.backends.mps.is_available():
        device = "mps"
    elif torch.cuda.is_available():
        device = "cuda"
    else:
        device = "cpu"

    # Load agent
    agent = PPOAgent(
        state_dim=p_cfg["state_dim"],
        action_dim=p_cfg["action_dim"],
        device=device,
    )

    ckpt = torch.load(args.checkpoint, map_location=device)
    agent.network.load_state_dict(ckpt["network_state_dict"])
    iteration = ckpt.get("iteration", "?")
    print(f"Loaded checkpoint: {args.checkpoint} (iteration {iteration})")
    print(f"Device: {device}")
    print(f"Evaluating on {len(eval_seeds)} games...\n")

    # Environment
    env = TractorEnv(
        host_path=host_path,
        ppo_seats=e_cfg["ppo_seats"],
        rule_ai_seats=e_cfg["rule_ai_seats"],
    )

    try:
        results = evaluate(env, agent, eval_seeds, verbose=args.verbose)
    finally:
        env.close()

    print(f"\n{'='*50}")
    print(f"Evaluation Results (iteration {iteration})")
    print(f"{'='*50}")
    print(f"Win rate:      {results['win_rate']:.2%} ({results['wins']}/{results['total_games']})")
    print(f"Avg reward:    {results['avg_reward']:+.4f} (std={results['reward_std']:.4f})")
    print(f"Illegal rate:  {results['illegal_rate']:.2%} ({results['illegal_count']}/{results['total_actions']})")
    print(f"{'='*50}")

    # Check Phase 1 targets
    print("\nPhase 1 Targets:")
    ok_illegal = results["illegal_rate"] < 0.05
    ok_winrate = results["win_rate"] > 0.20
    print(f"  Illegal rate < 5%:    {'PASS' if ok_illegal else 'FAIL'} ({results['illegal_rate']:.2%})")
    print(f"  Win rate vs RuleAI > 20%: {'PASS' if ok_winrate else 'FAIL'} ({results['win_rate']:.2%})")


if __name__ == "__main__":
    main()
