"""
Phase 1 PPO Training for Tractor.

Sequential trajectory collection against RuleAI opponents.
Terminal-only reward: R = (+10 if win else -10) + 2*level_gain + 0.02*final_score
"""
import argparse
import csv
import logging
import os
import sys
import time
from pathlib import Path

import numpy as np
import torch
import yaml

# Ensure rl_training is on the path
sys.path.insert(0, os.path.dirname(__file__))

from ppo_agent import PPOAgent
from mvp_env import TractorEnv

logger = logging.getLogger("phase1")

# ---------------------------------------------------------------------------
# Defaults
# ---------------------------------------------------------------------------
OBS_DIM = 382
ACTION_DIM = 384
DEFAULT_CONFIG = str(Path(__file__).with_name("phase1_config.yaml"))


def load_config(path: str) -> dict:
    with open(path) as f:
        return yaml.safe_load(f)


def load_eval_seeds(path: str) -> list[int]:
    with open(path) as f:
        return [int(line.strip()) for line in f if line.strip()]


# ---------------------------------------------------------------------------
# Trajectory collection
# ---------------------------------------------------------------------------

def collect_trajectories(
    env: TractorEnv,
    agent: PPOAgent,
    num_games: int,
    max_steps: int = 500,
) -> list[dict]:
    """Collect transitions from complete games.

    Returns a flat list of transition dicts.
    """
    transitions: list[dict] = []
    wins = 0
    total_reward = 0.0

    for _ in range(num_games):
        obs, mask = env.reset()
        game_transitions: list[dict] = []

        for _step in range(max_steps):
            action, log_prob, value = agent.select_action(obs, mask.astype(bool))

            next_obs, reward, done, info = env.step(action)
            next_mask = info.get("legal_mask", np.zeros(ACTION_DIM, dtype=np.float32))

            game_transitions.append({
                "obs": obs,
                "action": action,
                "log_prob": log_prob,
                "value": value,
                "reward": reward,
                "done": done,
                "mask": mask,
            })

            if done:
                if reward > 0:
                    wins += 1
                total_reward += reward
                break

            obs, mask = next_obs, next_mask

        transitions.extend(game_transitions)

    stats = {
        "num_games": num_games,
        "wins": wins,
        "avg_reward": total_reward / max(num_games, 1),
        "num_transitions": len(transitions),
    }
    return transitions, stats


# ---------------------------------------------------------------------------
# GAE
# ---------------------------------------------------------------------------

def compute_gae(
    transitions: list[dict],
    gamma: float = 0.99,
    gae_lambda: float = 0.95,
) -> tuple[np.ndarray, np.ndarray]:
    """Compute Generalized Advantage Estimation.

    Returns (advantages, returns) arrays aligned with transitions.
    """
    n = len(transitions)
    advantages = np.zeros(n, dtype=np.float32)
    returns = np.zeros(n, dtype=np.float32)

    gae = 0.0
    next_value = 0.0

    for t in reversed(range(n)):
        tr = transitions[t]
        if tr["done"]:
            next_value = 0.0
            gae = 0.0

        delta = tr["reward"] + gamma * next_value - tr["value"]
        gae = delta + gamma * gae_lambda * gae

        advantages[t] = gae
        returns[t] = gae + tr["value"]
        next_value = tr["value"]

    return advantages, returns


# ---------------------------------------------------------------------------
# Evaluation
# ---------------------------------------------------------------------------

def evaluate(
    env: TractorEnv,
    agent: PPOAgent,
    seeds: list[int],
    max_steps: int = 500,
) -> dict:
    """Evaluate agent deterministically on fixed seeds."""
    wins = 0
    total_reward = 0.0
    illegal_count = 0
    total_actions = 0

    for seed in seeds:
        obs, mask = env.reset(seed=seed)

        for _step in range(max_steps):
            action, _, _ = agent.select_action(obs, mask.astype(bool), deterministic=True)
            total_actions += 1

            # Check legality
            if mask[action] < 0.5:
                illegal_count += 1

            next_obs, reward, done, info = env.step(action)
            next_mask = info.get("legal_mask", np.zeros(ACTION_DIM, dtype=np.float32))

            if done:
                total_reward += reward
                if reward > 0:
                    wins += 1
                break

            obs, mask = next_obs, next_mask

    n = len(seeds)
    return {
        "win_rate": wins / max(n, 1),
        "avg_reward": total_reward / max(n, 1),
        "illegal_rate": illegal_count / max(total_actions, 1),
        "wins": wins,
        "total_games": n,
        "illegal_count": illegal_count,
        "total_actions": total_actions,
    }


# ---------------------------------------------------------------------------
# Logging helpers
# ---------------------------------------------------------------------------

def init_csv_log(path: str):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([
            "iteration", "games_played",
            "avg_reward", "win_rate",
            "num_transitions", "policy_loss", "value_loss", "entropy",
            "eval_win_rate", "eval_avg_reward", "eval_illegal_rate",
            "elapsed_sec",
        ])


def append_csv_log(path: str, row: dict):
    with open(path, "a", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([
            row.get("iteration", ""),
            row.get("games_played", ""),
            f"{row.get('avg_reward', 0):.4f}",
            f"{row.get('win_rate', 0):.4f}",
            row.get("num_transitions", ""),
            f"{row.get('policy_loss', 0):.6f}",
            f"{row.get('value_loss', 0):.6f}",
            f"{row.get('entropy', 0):.6f}",
            f"{row.get('eval_win_rate', '')}" if row.get("eval_win_rate") is not None else "",
            f"{row.get('eval_avg_reward', '')}" if row.get("eval_avg_reward") is not None else "",
            f"{row.get('eval_illegal_rate', '')}" if row.get("eval_illegal_rate") is not None else "",
            f"{row.get('elapsed_sec', 0):.1f}",
        ])


# ---------------------------------------------------------------------------
# Main training loop
# ---------------------------------------------------------------------------

def train(cfg: dict, args):
    t_cfg = cfg["training"]
    p_cfg = cfg["ppo"]
    e_cfg = cfg["environment"]
    l_cfg = cfg["logging"]
    ev_cfg = cfg["evaluation"]

    max_iterations = args.max_iterations or t_cfg["max_iterations"]
    games_per_iter = t_cfg["games_per_iteration"]
    eval_interval = t_cfg["eval_interval"]
    save_interval = t_cfg["save_interval"]
    max_steps = t_cfg["max_steps_per_game"]

    # Resolve host path relative to project root
    project_root = Path(__file__).resolve().parent.parent
    host_path = str(project_root / e_cfg["host_path"])

    # Directories
    log_dir = str(project_root / l_cfg["log_dir"])
    ckpt_dir = str(project_root / l_cfg["checkpoint_dir"])
    os.makedirs(log_dir, exist_ok=True)
    os.makedirs(ckpt_dir, exist_ok=True)

    csv_path = os.path.join(log_dir, "training_log.csv")
    init_csv_log(csv_path)

    # Eval seeds
    seeds_path = str(project_root / ev_cfg["eval_seeds_file"])
    if not os.path.exists(seeds_path):
        print(f"Eval seeds not found at {seeds_path}, generating...")
        from generate_eval_seeds import main as gen_seeds
        gen_seeds()
    eval_seeds = load_eval_seeds(seeds_path)

    # Device
    if torch.backends.mps.is_available():
        device = "mps"
    elif torch.cuda.is_available():
        device = "cuda"
    else:
        device = "cpu"
    print(f"Device: {device}")

    # Agent
    agent = PPOAgent(
        state_dim=p_cfg["state_dim"],
        action_dim=p_cfg["action_dim"],
        device=device,
        lr=p_cfg["learning_rate"],
    )
    agent.clip_epsilon = p_cfg["clip_epsilon"]
    agent.value_coef = p_cfg["value_coef"]
    agent.entropy_coef = p_cfg["entropy_coef"]

    # Load checkpoint if resuming
    if args.resume:
        print(f"Resuming from {args.resume}")
        ckpt = torch.load(args.resume, map_location=device)
        agent.network.load_state_dict(ckpt["network_state_dict"])
        agent.optimizer.load_state_dict(ckpt["optimizer_state_dict"])
        start_iter = ckpt.get("iteration", 0) + 1
    else:
        start_iter = 0

    # Environment
    env = TractorEnv(
        host_path=host_path,
        ppo_seats=e_cfg["ppo_seats"],
        rule_ai_seats=e_cfg["rule_ai_seats"],
    )

    best_eval_win_rate = -1.0
    total_games = 0

    print(f"\nStarting Phase 1 training: {max_iterations} iterations, "
          f"{games_per_iter} games/iter")
    print(f"Logging to {csv_path}")
    print(f"Checkpoints to {ckpt_dir}\n")

    try:
        for iteration in range(start_iter, max_iterations):
            iter_start = time.time()

            # 1. Collect trajectories
            transitions, collect_stats = collect_trajectories(
                env, agent, games_per_iter, max_steps
            )
            total_games += games_per_iter

            if not transitions:
                print(f"[{iteration}] No transitions collected, skipping")
                continue

            # 2. Compute GAE
            advantages, returns = compute_gae(
                transitions,
                gamma=p_cfg["gamma"],
                gae_lambda=p_cfg["gae_lambda"],
            )

            # 3. PPO update
            states = np.array([t["obs"] for t in transitions])
            actions = np.array([t["action"] for t in transitions])
            old_log_probs = np.array([t["log_prob"] for t in transitions])
            masks = np.array([t["mask"] for t in transitions])

            policy_loss, value_loss = agent.update(
                states, actions, old_log_probs, advantages, returns, masks,
                epochs=p_cfg["epochs_per_update"],
                batch_size=p_cfg["batch_size"],
            )

            elapsed = time.time() - iter_start

            # Build log row
            row = {
                "iteration": iteration,
                "games_played": total_games,
                "avg_reward": collect_stats["avg_reward"],
                "win_rate": collect_stats["wins"] / max(games_per_iter, 1),
                "num_transitions": collect_stats["num_transitions"],
                "policy_loss": policy_loss,
                "value_loss": value_loss,
                "entropy": 0.0,
                "eval_win_rate": None,
                "eval_avg_reward": None,
                "eval_illegal_rate": None,
                "elapsed_sec": elapsed,
            }

            # 4. Evaluate
            if iteration % eval_interval == 0:
                eval_results = evaluate(env, agent, eval_seeds, max_steps)
                row["eval_win_rate"] = eval_results["win_rate"]
                row["eval_avg_reward"] = eval_results["avg_reward"]
                row["eval_illegal_rate"] = eval_results["illegal_rate"]

                print(
                    f"[{iteration:4d}] games={total_games:6d} | "
                    f"reward={collect_stats['avg_reward']:+.2f} "
                    f"win={collect_stats['wins']}/{games_per_iter} | "
                    f"eval_win={eval_results['win_rate']:.2%} "
                    f"eval_illegal={eval_results['illegal_rate']:.2%} | "
                    f"ploss={policy_loss:.4f} vloss={value_loss:.4f} | "
                    f"{elapsed:.1f}s"
                )

                # Save best model
                if eval_results["win_rate"] > best_eval_win_rate:
                    best_eval_win_rate = eval_results["win_rate"]
                    best_path = os.path.join(ckpt_dir, "best_model.pt")
                    torch.save({
                        "network_state_dict": agent.network.state_dict(),
                        "optimizer_state_dict": agent.optimizer.state_dict(),
                        "iteration": iteration,
                        "eval_results": eval_results,
                    }, best_path)
            else:
                print(
                    f"[{iteration:4d}] games={total_games:6d} | "
                    f"reward={collect_stats['avg_reward']:+.2f} "
                    f"win={collect_stats['wins']}/{games_per_iter} | "
                    f"ploss={policy_loss:.4f} vloss={value_loss:.4f} | "
                    f"{elapsed:.1f}s"
                )

            append_csv_log(csv_path, row)

            # 5. Save checkpoint
            if iteration % save_interval == 0 and iteration > 0:
                ckpt_path = os.path.join(ckpt_dir, f"checkpoint_{iteration}.pt")
                torch.save({
                    "network_state_dict": agent.network.state_dict(),
                    "optimizer_state_dict": agent.optimizer.state_dict(),
                    "iteration": iteration,
                }, ckpt_path)
                print(f"  -> Saved checkpoint: {ckpt_path}")

    except KeyboardInterrupt:
        print("\nTraining interrupted by user.")
    finally:
        # Save final checkpoint
        final_path = os.path.join(ckpt_dir, "final_model.pt")
        torch.save({
            "network_state_dict": agent.network.state_dict(),
            "optimizer_state_dict": agent.optimizer.state_dict(),
            "iteration": iteration if "iteration" in dir() else 0,
        }, final_path)
        print(f"Saved final model: {final_path}")
        env.close()

    print(f"\nTraining complete. Total games: {total_games}")
    print(f"Best eval win rate: {best_eval_win_rate:.2%}")


# ---------------------------------------------------------------------------
# CLI
# ---------------------------------------------------------------------------

def main():
    parser = argparse.ArgumentParser(description="Phase 1 PPO Training for Tractor")
    parser.add_argument(
        "--config", default=DEFAULT_CONFIG,
        help="Path to YAML config file",
    )
    parser.add_argument(
        "--max_iterations", type=int, default=None,
        help="Override max iterations from config",
    )
    parser.add_argument(
        "--resume", type=str, default=None,
        help="Path to checkpoint to resume from",
    )
    args = parser.parse_args()

    logging.basicConfig(
        level=logging.WARNING,
        format="%(asctime)s %(name)s %(levelname)s %(message)s",
    )

    cfg = load_config(args.config)
    train(cfg, args)


if __name__ == "__main__":
    main()
