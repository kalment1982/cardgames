"""
Phase 1 PPO Training for Tractor.

Sequential trajectory collection against RuleAI opponents.
Terminal-only reward: R = (+10 if win else -10) + 2*level_gain + 0.02*final_score
"""
import argparse
import csv
import json
import logging
import os
import sys
import time
from datetime import datetime, timezone
from pathlib import Path

import numpy as np
import torch
import yaml

try:
    from torch.utils.tensorboard import SummaryWriter
except ModuleNotFoundError:
    SummaryWriter = None

# Ensure rl_training is on the path
sys.path.insert(0, os.path.dirname(__file__))

from ppo_agent import PPOAgent
from mvp_env import TractorEnv

logger = logging.getLogger("phase1")


class _NoOpSummaryWriter:
    def add_scalar(self, *args, **kwargs):
        return None

    def flush(self):
        return None

    def close(self):
        return None

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
    collect_game_results: bool = False,
) -> dict:
    """Evaluate agent deterministically on fixed seeds."""
    wins = 0
    total_reward = 0.0
    illegal_count = 0
    total_actions = 0
    game_results: list[dict] = []

    for seed in seeds:
        obs, mask = env.reset(seed=seed)
        game_illegal = 0
        game_actions = 0
        terminal_result = None

        for _step in range(max_steps):
            action, _, _ = agent.select_action(obs, mask.astype(bool), deterministic=True)
            total_actions += 1
            game_actions += 1

            # Check legality
            if mask[action] < 0.5:
                illegal_count += 1
                game_illegal += 1

            next_obs, reward, done, info = env.step(action)
            next_mask = info.get("legal_mask", np.zeros(ACTION_DIM, dtype=np.float32))

            if done:
                terminal_result = info.get("terminal_result")
                total_reward += reward
                if reward > 0:
                    wins += 1
                break

            obs, mask = next_obs, next_mask

        if collect_game_results:
            game_results.append({
                "seed": seed,
                "reward": reward if "reward" in locals() else 0.0,
                "won": bool((reward if "reward" in locals() else 0.0) > 0),
                "illegal_count": game_illegal,
                "action_count": game_actions,
                "terminal_result": terminal_result or {},
            })

    n = len(seeds)
    results = {
        "win_rate": wins / max(n, 1),
        "avg_reward": total_reward / max(n, 1),
        "illegal_rate": illegal_count / max(total_actions, 1),
        "wins": wins,
        "total_games": n,
        "illegal_count": illegal_count,
        "total_actions": total_actions,
    }
    if collect_game_results:
        results["game_results"] = game_results
    return results


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


def init_eval_summary_log(path: str):
    with open(path, "w", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([
            "iteration", "games_played",
            "eval_win_rate", "eval_avg_reward", "eval_illegal_rate",
            "wins", "total_games", "illegal_count", "total_actions",
            "timestamp_utc",
        ])


def append_eval_summary_log(path: str, row: dict):
    with open(path, "a", newline="") as f:
        writer = csv.writer(f)
        writer.writerow([
            row.get("iteration", ""),
            row.get("games_played", ""),
            f"{row.get('eval_win_rate', 0):.6f}",
            f"{row.get('eval_avg_reward', 0):.6f}",
            f"{row.get('eval_illegal_rate', 0):.6f}",
            row.get("wins", ""),
            row.get("total_games", ""),
            row.get("illegal_count", ""),
            row.get("total_actions", ""),
            row.get("timestamp_utc", ""),
        ])


def append_eval_match_results(path: str, iteration: int, games_played: int, game_results: list[dict]):
    timestamp_utc = datetime.now(timezone.utc).isoformat()
    with open(path, "a", encoding="utf-8") as f:
        for game in game_results:
            terminal = game.get("terminal_result") or {}
            record = {
                "iteration": iteration,
                "games_played": games_played,
                "timestamp_utc": timestamp_utc,
                "seed": game.get("seed"),
                "reward": game.get("reward"),
                "won": game.get("won"),
                "illegal_count": game.get("illegal_count"),
                "action_count": game.get("action_count"),
                "my_team_final_score": terminal.get("my_team_final_score"),
                "my_team_level_gain": terminal.get("my_team_level_gain"),
                "defender_score": terminal.get("defender_score"),
                "next_dealer": terminal.get("next_dealer"),
            }
            f.write(json.dumps(record, ensure_ascii=False) + "\n")


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
    eval_summary_path = os.path.join(log_dir, "eval_summary.csv")
    eval_matches_path = os.path.join(log_dir, "eval_match_results.jsonl")
    tensorboard_dir = str(project_root / l_cfg.get("tensorboard_dir", os.path.join(l_cfg["log_dir"], "tb")))

    init_csv_log(csv_path)
    init_eval_summary_log(eval_summary_path)
    Path(eval_matches_path).touch()
    if SummaryWriter is None:
        print("tensorboard package not installed; TensorBoard logging disabled.")
        writer = _NoOpSummaryWriter()
    else:
        writer = SummaryWriter(log_dir=tensorboard_dir)

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

    if args.resume and args.init_checkpoint:
        raise ValueError("--resume and --init_checkpoint cannot be used together.")

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
    elif args.init_checkpoint:
        print(f"Initializing network from {args.init_checkpoint}")
        ckpt = torch.load(args.init_checkpoint, map_location=device)
        agent.network.load_state_dict(ckpt["network_state_dict"])
        start_iter = 0
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
    print(f"TensorBoard to {tensorboard_dir}")
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

            policy_loss, value_loss, entropy = agent.update(
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
                "entropy": entropy,
                "eval_win_rate": None,
                "eval_avg_reward": None,
                "eval_illegal_rate": None,
                "elapsed_sec": elapsed,
            }

            step = total_games
            writer.add_scalar("train/avg_reward", collect_stats["avg_reward"], step)
            writer.add_scalar("train/win_rate", collect_stats["wins"] / max(games_per_iter, 1), step)
            writer.add_scalar("train/num_transitions", collect_stats["num_transitions"], step)
            writer.add_scalar("train/policy_loss", policy_loss, step)
            writer.add_scalar("train/value_loss", value_loss, step)
            writer.add_scalar("train/entropy", row["entropy"], step)
            writer.add_scalar("train/elapsed_sec", elapsed, step)

            # 4. Evaluate
            if iteration % eval_interval == 0:
                eval_results = evaluate(
                    env,
                    agent,
                    eval_seeds,
                    max_steps,
                    collect_game_results=True,
                )
                row["eval_win_rate"] = eval_results["win_rate"]
                row["eval_avg_reward"] = eval_results["avg_reward"]
                row["eval_illegal_rate"] = eval_results["illegal_rate"]

                eval_summary_row = {
                    "iteration": iteration,
                    "games_played": total_games,
                    "eval_win_rate": eval_results["win_rate"],
                    "eval_avg_reward": eval_results["avg_reward"],
                    "eval_illegal_rate": eval_results["illegal_rate"],
                    "wins": eval_results["wins"],
                    "total_games": eval_results["total_games"],
                    "illegal_count": eval_results["illegal_count"],
                    "total_actions": eval_results["total_actions"],
                    "timestamp_utc": datetime.now(timezone.utc).isoformat(),
                }
                append_eval_summary_log(eval_summary_path, eval_summary_row)
                append_eval_match_results(
                    eval_matches_path,
                    iteration,
                    total_games,
                    eval_results.get("game_results", []),
                )

                writer.add_scalar("eval_rule_ai/win_rate", eval_results["win_rate"], step)
                writer.add_scalar("eval_rule_ai/avg_reward", eval_results["avg_reward"], step)
                writer.add_scalar("eval_rule_ai/illegal_rate", eval_results["illegal_rate"], step)
                writer.add_scalar("eval_rule_ai/wins", eval_results["wins"], step)
                writer.add_scalar("eval_rule_ai/total_games", eval_results["total_games"], step)

                print(
                    f"[{iteration:4d}] games={total_games:6d} | "
                    f"reward={collect_stats['avg_reward']:+.2f} "
                    f"win={collect_stats['wins']}/{games_per_iter} | "
                    f"eval_win={eval_results['win_rate']:.2%} "
                    f"eval_illegal={eval_results['illegal_rate']:.2%} | "
                    f"ploss={policy_loss:.4f} vloss={value_loss:.4f} entropy={entropy:.4f} | "
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
                    f"ploss={policy_loss:.4f} vloss={value_loss:.4f} entropy={entropy:.4f} | "
                    f"{elapsed:.1f}s"
                )

            append_csv_log(csv_path, row)
            writer.flush()

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
        writer.close()

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
    parser.add_argument(
        "--init_checkpoint", type=str, default=None,
        help="Path to a pretrained checkpoint used only to initialize network weights",
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
