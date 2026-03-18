#!/usr/bin/env python3
"""
Policy-only behavior cloning warm start for PPO.

Consumes a demonstration dataset generated from RuleAI V2.1 and writes a
checkpoint compatible with ``train_phase1.py --init_checkpoint``.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

import numpy as np
import torch
import torch.nn.functional as F
import yaml
from torch.utils.data import DataLoader, TensorDataset, random_split

from ppo_agent import PPOAgent


DEFAULT_CONFIG = Path(__file__).with_name("phase1_config.yaml")


def load_config(path: str) -> dict:
    with open(path, encoding="utf-8") as f:
        return yaml.safe_load(f)


def resolve_device() -> str:
    if torch.backends.mps.is_available():
        return "mps"
    if torch.cuda.is_available():
        return "cuda"
    return "cpu"


def build_dataset(npz_path: Path) -> TensorDataset:
    data = np.load(npz_path)
    states = torch.from_numpy(data["states"]).float()
    masks = torch.from_numpy(data["masks"]).bool()
    actions = torch.from_numpy(data["actions"]).long()
    return TensorDataset(states, masks, actions)


def evaluate(agent: PPOAgent, loader: DataLoader) -> tuple[float, float]:
    agent.network.eval()
    total_loss = 0.0
    total_correct = 0
    total_samples = 0

    with torch.no_grad():
        for states, masks, actions in loader:
            states = states.to(agent.device)
            masks = masks.to(agent.device)
            actions = actions.to(agent.device)

            probs, _ = agent.network(states, masks)
            loss = F.nll_loss(torch.log(probs.clamp_min(1e-8)), actions)
            preds = probs.argmax(dim=1)

            batch_size = states.size(0)
            total_loss += loss.item() * batch_size
            total_correct += (preds == actions).sum().item()
            total_samples += batch_size

    if total_samples == 0:
        return 0.0, 0.0

    return total_loss / total_samples, total_correct / total_samples


def main():
    parser = argparse.ArgumentParser(description="Pretrain PPO policy with RuleAI demonstrations")
    parser.add_argument("--config", default=str(DEFAULT_CONFIG), help="Path to phase1_config.yaml")
    parser.add_argument("--dataset", type=str, default=None, help="Path to warm-start dataset (.npz)")
    parser.add_argument("--output", type=str, default=None, help="Output pretrained checkpoint path")
    parser.add_argument("--epochs", type=int, default=None, help="Number of BC epochs")
    parser.add_argument("--batch_size", type=int, default=None, help="BC batch size")
    parser.add_argument("--learning_rate", type=float, default=None, help="BC learning rate")
    parser.add_argument("--init_checkpoint", type=str, default=None, help="Optional checkpoint to initialize from")
    parser.add_argument("--val_split", type=float, default=0.1, help="Validation split ratio")
    args = parser.parse_args()

    cfg = load_config(args.config)
    warm_cfg = cfg.get("warm_start", {})
    ppo_cfg = cfg["ppo"]

    project_root = Path(__file__).resolve().parent.parent
    dataset_path = Path(args.dataset) if args.dataset else project_root / warm_cfg.get(
        "dataset_path", "artifacts/ppo_warm_start/ruleai_v21_playtricks_2000g.npz"
    )
    output_path = Path(args.output) if args.output else project_root / warm_cfg.get(
        "checkpoint_path", "checkpoints/phase1_warm_start/pretrained_ruleai_v21.pt"
    )
    output_path.parent.mkdir(parents=True, exist_ok=True)

    epochs = args.epochs or warm_cfg.get("bc_epochs", 3)
    batch_size = args.batch_size or warm_cfg.get("batch_size", 256)
    learning_rate = args.learning_rate or warm_cfg.get("learning_rate", ppo_cfg.get("learning_rate", 3e-4))

    if not dataset_path.exists():
        raise FileNotFoundError(f"Warm-start dataset not found: {dataset_path}")

    device = resolve_device()
    dataset = build_dataset(dataset_path)

    val_size = int(len(dataset) * args.val_split)
    train_size = len(dataset) - val_size
    if train_size <= 0:
        raise ValueError("Dataset too small for requested validation split.")

    if val_size > 0:
        train_dataset, val_dataset = random_split(
            dataset,
            [train_size, val_size],
            generator=torch.Generator().manual_seed(42),
        )
        val_loader = DataLoader(val_dataset, batch_size=batch_size, shuffle=False)
    else:
        train_dataset = dataset
        val_loader = None

    train_loader = DataLoader(train_dataset, batch_size=batch_size, shuffle=True)

    agent = PPOAgent(
        state_dim=ppo_cfg["state_dim"],
        action_dim=ppo_cfg["action_dim"],
        device=device,
        lr=learning_rate,
    )

    if args.init_checkpoint:
        checkpoint = torch.load(args.init_checkpoint, map_location=device)
        agent.network.load_state_dict(checkpoint["network_state_dict"])

    history: list[dict] = []

    for epoch in range(epochs):
        agent.network.train()
        epoch_loss = 0.0
        epoch_correct = 0
        epoch_samples = 0

        for states, masks, actions in train_loader:
            states = states.to(agent.device)
            masks = masks.to(agent.device)
            actions = actions.to(agent.device)

            probs, _ = agent.network(states, masks)
            loss = F.nll_loss(torch.log(probs.clamp_min(1e-8)), actions)

            agent.optimizer.zero_grad()
            loss.backward()
            torch.nn.utils.clip_grad_norm_(agent.network.parameters(), 0.5)
            agent.optimizer.step()

            preds = probs.argmax(dim=1)
            batch_size_actual = states.size(0)
            epoch_loss += loss.item() * batch_size_actual
            epoch_correct += (preds == actions).sum().item()
            epoch_samples += batch_size_actual

        train_loss = epoch_loss / max(epoch_samples, 1)
        train_acc = epoch_correct / max(epoch_samples, 1)

        if val_loader is not None:
            val_loss, val_acc = evaluate(agent, val_loader)
        else:
            val_loss, val_acc = 0.0, 0.0

        metrics = {
            "epoch": epoch + 1,
            "train_loss": train_loss,
            "train_accuracy": train_acc,
            "val_loss": val_loss,
            "val_accuracy": val_acc,
        }
        history.append(metrics)
        print(
            f"Epoch {epoch + 1}/{epochs} | "
            f"train_loss={train_loss:.4f} train_acc={train_acc:.2%} | "
            f"val_loss={val_loss:.4f} val_acc={val_acc:.2%}"
        )

    checkpoint = {
        "network_state_dict": agent.network.state_dict(),
        "optimizer_state_dict": agent.optimizer.state_dict(),
        "bc_metrics": history[-1] if history else {},
        "bc_history": history,
        "dataset_path": str(dataset_path),
        "epochs": epochs,
        "batch_size": batch_size,
        "learning_rate": learning_rate,
    }
    torch.save(checkpoint, output_path)

    metrics_path = output_path.with_suffix(".json")
    with open(metrics_path, "w", encoding="utf-8") as f:
        json.dump(
            {
                "checkpoint_path": str(output_path),
                "dataset_path": str(dataset_path),
                "epochs": epochs,
                "batch_size": batch_size,
                "learning_rate": learning_rate,
                "history": history,
            },
            f,
            ensure_ascii=False,
            indent=2,
        )

    print(f"Saved pretrained checkpoint: {output_path}")
    print(f"Saved metrics: {metrics_path}")


if __name__ == "__main__":
    main()
