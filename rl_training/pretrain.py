"""
预训练脚本 - 使用LLM生成的专家数据预训练策略网络
"""
if __name__ == "__main__":
    import sys

    print("Legacy workflow disabled: pretrain.py is preserved for reference only.")
    print("Active PPO training entrypoint: python3 rl_training/train_phase1.py")
    sys.exit(1)

import torch
import torch.nn as nn
from torch.utils.data import Dataset, DataLoader
import json
import yaml
import os
from ppo_agent import PolicyValueNetwork
import numpy as np


class ExpertDataset(Dataset):
    """专家数据集"""

    def __init__(self, data_path: str):
        with open(data_path, 'r') as f:
            self.data = json.load(f)

        print(f"Loaded {len(self.data)} expert samples")

    def __len__(self):
        return len(self.data)

    def __getitem__(self, idx):
        sample = self.data[idx]
        state = torch.FloatTensor(sample['state'])
        action = torch.LongTensor([sample['action']])[0]
        return state, action


def pretrain_from_llm(config_path: str = "config.yaml"):
    """使用LLM数据预训练策略网络"""

    # 加载配置
    with open(config_path, 'r', encoding='utf-8') as f:
        config = yaml.safe_load(f)

    pretrain_config = config['pretrain']
    data_path = config['data_generation']['save_path']

    # 检查数据文件是否存在
    if not os.path.exists(data_path):
        raise FileNotFoundError(f"Expert dataset not found: {data_path}")

    # 加载数据
    dataset = ExpertDataset(data_path)

    # 划分训练集和验证集
    val_split = pretrain_config['validation_split']
    val_size = int(len(dataset) * val_split)
    train_size = len(dataset) - val_size

    train_dataset, val_dataset = torch.utils.data.random_split(
        dataset, [train_size, val_size]
    )

    train_loader = DataLoader(
        train_dataset,
        batch_size=pretrain_config['batch_size'],
        shuffle=True,
        num_workers=0
    )

    val_loader = DataLoader(
        val_dataset,
        batch_size=pretrain_config['batch_size'],
        shuffle=False,
        num_workers=0
    )

    # 创建网络
    device = torch.device(config['system']['device'] if torch.backends.mps.is_available() else "cpu")
    print(f"Using device: {device}")

    # 使用增强的状态维度
    state_dim = config['state_encoding']['base_dim'] + \
                config['state_encoding']['opponent_belief_dim'] + \
                config['state_encoding']['play_pattern_dim']

    network = PolicyValueNetwork(state_dim=state_dim, action_dim=1000).to(device)

    # 优化器
    optimizer = torch.optim.Adam(
        network.parameters(),
        lr=pretrain_config['learning_rate'],
        weight_decay=pretrain_config['weight_decay']
    )

    # 损失函数
    criterion = nn.CrossEntropyLoss()

    # 训练
    print(f"\nStarting pretraining...")
    print(f"  Epochs: {pretrain_config['epochs']}")
    print(f"  Batch size: {pretrain_config['batch_size']}")
    print(f"  Learning rate: {pretrain_config['learning_rate']}")
    print(f"  Train samples: {train_size}")
    print(f"  Val samples: {val_size}")
    print()

    best_val_acc = 0.0

    for epoch in range(pretrain_config['epochs']):
        # 训练阶段
        network.train()
        train_loss = 0.0
        train_correct = 0
        train_total = 0

        for batch_idx, (states, actions) in enumerate(train_loader):
            states = states.to(device)
            actions = actions.to(device)

            # 前向传播
            action_logits, _ = network(states)
            loss = criterion(action_logits, actions)

            # 反向传播
            optimizer.zero_grad()
            loss.backward()
            optimizer.step()

            # 统计
            train_loss += loss.item()
            _, predicted = action_logits.max(1)
            train_correct += (predicted == actions).sum().item()
            train_total += actions.size(0)

            if (batch_idx + 1) % 10 == 0:
                print(f"Epoch {epoch+1}/{pretrain_config['epochs']}, "
                      f"Batch {batch_idx+1}/{len(train_loader)}, "
                      f"Loss: {loss.item():.4f}")

        train_acc = 100 * train_correct / train_total
        avg_train_loss = train_loss / len(train_loader)

        # 验证阶段
        network.eval()
        val_loss = 0.0
        val_correct = 0
        val_total = 0

        with torch.no_grad():
            for states, actions in val_loader:
                states = states.to(device)
                actions = actions.to(device)

                action_logits, _ = network(states)
                loss = criterion(action_logits, actions)

                val_loss += loss.item()
                _, predicted = action_logits.max(1)
                val_correct += (predicted == actions).sum().item()
                val_total += actions.size(0)

        val_acc = 100 * val_correct / val_total
        avg_val_loss = val_loss / len(val_loader)

        print(f"\nEpoch {epoch+1}/{pretrain_config['epochs']} Summary:")
        print(f"  Train Loss: {avg_train_loss:.4f}, Train Acc: {train_acc:.2f}%")
        print(f"  Val Loss: {avg_val_loss:.4f}, Val Acc: {val_acc:.2f}%")
        print()

        # 保存最佳模型
        if val_acc > best_val_acc:
            best_val_acc = val_acc
            save_path = pretrain_config['save_path']
            os.makedirs(os.path.dirname(save_path), exist_ok=True)
            torch.save(network.state_dict(), save_path)
            print(f"  ✓ Best model saved! Val Acc: {val_acc:.2f}%")

    print(f"\nPretraining complete!")
    print(f"  Best validation accuracy: {best_val_acc:.2f}%")
    print(f"  Model saved to: {pretrain_config['save_path']}")

