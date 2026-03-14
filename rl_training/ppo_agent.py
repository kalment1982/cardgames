"""
PPO Agent - 强化学习智能体
"""
import torch
import torch.nn as nn
import torch.nn.functional as F
from typing import Tuple, List
import numpy as np


class PolicyValueNetwork(nn.Module):
    """策略-价值网络（共享编码器）"""

    def __init__(self, state_dim=338, action_dim=108, hidden_dim=256):
        super().__init__()

        # 共享编码器
        self.encoder = nn.Sequential(
            nn.Linear(state_dim, 512),
            nn.ReLU(),
            nn.LayerNorm(512),
            nn.Dropout(0.1),

            nn.Linear(512, hidden_dim),
            nn.ReLU(),
            nn.LayerNorm(hidden_dim),
            nn.Dropout(0.1),
        )

        # 策略头（输出动作概率）
        self.policy_head = nn.Sequential(
            nn.Linear(hidden_dim, 128),
            nn.ReLU(),
            nn.Linear(128, action_dim),
        )

        # 价值头（输出状态价值）
        self.value_head = nn.Sequential(
            nn.Linear(hidden_dim, 128),
            nn.ReLU(),
            nn.Linear(128, 1),
        )

    def forward(self, state: torch.Tensor, action_mask: torch.Tensor = None) -> Tuple[torch.Tensor, torch.Tensor]:
        """前向传播

        Args:
            state: (batch, state_dim)
            action_mask: (batch, action_dim) 布尔掩码

        Returns:
            action_probs: (batch, action_dim)
            value: (batch, 1)
        """
        # 编码
        x = self.encoder(state)

        # 策略输出
        logits = self.policy_head(x)

        # 应用动作掩码
        if action_mask is not None:
            logits = logits.masked_fill(~action_mask, -1e9)

        action_probs = F.softmax(logits, dim=-1)

        # 价值输出
        value = self.value_head(x)

        return action_probs, value


class PPOAgent:
    """PPO智能体"""

    def __init__(self, state_dim=338, action_dim=108, device='mps', lr=3e-4):
        self.device = torch.device(device)
        self.action_dim = action_dim

        # 网络
        self.network = PolicyValueNetwork(state_dim, action_dim).to(self.device)

        # 优化器
        self.optimizer = torch.optim.Adam(self.network.parameters(), lr=lr)

        # PPO参数
        self.clip_epsilon = 0.2
        self.value_coef = 0.5
        self.entropy_coef = 0.01

    def select_action(self, state: np.ndarray, action_mask: np.ndarray = None, deterministic=False) -> Tuple[int, float]:
        """选择动作

        Args:
            state: 状态向量
            action_mask: 合法动作掩码
            deterministic: 是否确定性选择（用于评估）

        Returns:
            action_index: 动作索引
            log_prob: 对数概率
        """
        self.network.eval()

        with torch.no_grad():
            state_tensor = torch.FloatTensor(state).unsqueeze(0).to(self.device)

            if action_mask is not None:
                mask_tensor = torch.BoolTensor(action_mask).unsqueeze(0).to(self.device)
            else:
                mask_tensor = None

            action_probs, _ = self.network(state_tensor, mask_tensor)
            action_probs = action_probs.squeeze(0)

            # 选择动作
            if deterministic:
                action_index = torch.argmax(action_probs).item()
            else:
                dist = torch.distributions.Categorical(action_probs)
                action_index = dist.sample().item()

            # 计算log概率
            log_prob = torch.log(action_probs[action_index] + 1e-8).item()

        return action_index, log_prob

    def evaluate_actions(self, states: torch.Tensor, actions: torch.Tensor,
                        action_masks: torch.Tensor = None) -> Tuple[torch.Tensor, torch.Tensor, torch.Tensor]:
        """评估动作

        Args:
            states: (batch, state_dim)
            actions: (batch,)
            action_masks: (batch, action_dim)

        Returns:
            log_probs: (batch,)
            values: (batch,)
            entropy: (batch,)
        """
        action_probs, values = self.network(states, action_masks)

        # 创建分布
        dist = torch.distributions.Categorical(action_probs)

        # 计算log概率
        log_probs = dist.log_prob(actions)

        # 计算熵（鼓励探索）
        entropy = dist.entropy()

        return log_probs, values.squeeze(-1), entropy

    def update(self, states: np.ndarray, actions: np.ndarray, old_log_probs: np.ndarray,
              advantages: np.ndarray, returns: np.ndarray, action_masks: np.ndarray = None,
              epochs=10, batch_size=256) -> Tuple[float, float]:
        """PPO更新

        Args:
            states: (N, state_dim)
            actions: (N,)
            old_log_probs: (N,)
            advantages: (N,)
            returns: (N,)
            action_masks: (N, action_dim)
            epochs: 更新轮数
            batch_size: 批次大小

        Returns:
            avg_policy_loss: 平均策略损失
            avg_value_loss: 平均价值损失
        """
        self.network.train()

        # 转换为tensor
        states = torch.FloatTensor(states).to(self.device)
        actions = torch.LongTensor(actions).to(self.device)
        old_log_probs = torch.FloatTensor(old_log_probs).to(self.device)
        advantages = torch.FloatTensor(advantages).to(self.device)
        returns = torch.FloatTensor(returns).to(self.device)

        if action_masks is not None:
            action_masks = torch.BoolTensor(action_masks).to(self.device)

        # 归一化优势
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        total_policy_loss = 0
        total_value_loss = 0
        num_updates = 0

        for epoch in range(epochs):
            # 随机打乱
            indices = torch.randperm(len(states))

            for start in range(0, len(states), batch_size):
                end = min(start + batch_size, len(states))
                batch_indices = indices[start:end]

                # 批次数据
                batch_states = states[batch_indices]
                batch_actions = actions[batch_indices]
                batch_old_log_probs = old_log_probs[batch_indices]
                batch_advantages = advantages[batch_indices]
                batch_returns = returns[batch_indices]

                batch_masks = action_masks[batch_indices] if action_masks is not None else None

                # 评估动作
                new_log_probs, values, entropy = self.evaluate_actions(
                    batch_states, batch_actions, batch_masks
                )

                # PPO策略损失
                ratio = torch.exp(new_log_probs - batch_old_log_probs)
                surr1 = ratio * batch_advantages
                surr2 = torch.clamp(ratio, 1 - self.clip_epsilon, 1 + self.clip_epsilon) * batch_advantages
                policy_loss = -torch.min(surr1, surr2).mean()

                # 价值损失
                value_loss = F.mse_loss(values, batch_returns)

                # 熵损失（鼓励探索）
                entropy_loss = -entropy.mean()

                # 总损失
                loss = policy_loss + self.value_coef * value_loss + self.entropy_coef * entropy_loss

                # 反向传播
                self.optimizer.zero_grad()
                loss.backward()
                torch.nn.utils.clip_grad_norm_(self.network.parameters(), 0.5)
                self.optimizer.step()

                total_policy_loss += policy_loss.item()
                total_value_loss += value_loss.item()
                num_updates += 1

        return total_policy_loss / num_updates, total_value_loss / num_updates

    def save(self, path: str):
        """保存模型"""
        torch.save({
            'network_state_dict': self.network.state_dict(),
            'optimizer_state_dict': self.optimizer.state_dict(),
        }, path)

    def load(self, path: str):
        """加载模型"""
        checkpoint = torch.load(path, map_location=self.device)
        self.network.load_state_dict(checkpoint['network_state_dict'])
        self.optimizer.load_state_dict(checkpoint['optimizer_state_dict'])

    def get_state_dict(self):
        """获取模型参数（用于多进程）"""
        return self.network.state_dict()

    def load_state_dict(self, state_dict):
        """加载模型参数（用于多进程）"""
        self.network.load_state_dict(state_dict)


if __name__ == '__main__':
    # 测试
    print("测试PPO Agent...")

    # 检查MPS可用性
    if torch.backends.mps.is_available():
        device = 'mps'
        print("✅ MPS (M4 GPU) 可用")
    else:
        device = 'cpu'
        print("⚠️  MPS不可用，使用CPU")

    # 创建agent
    agent = PPOAgent(device=device)
    print(f"Agent创建成功，设备: {device}")

    # 测试选择动作
    state = np.random.randn(338).astype(np.float32)
    action_mask = np.ones(108, dtype=bool)
    action_mask[50:] = False  # 只有前50个动作合法

    action, log_prob = agent.select_action(state, action_mask)
    print(f"选择动作: {action}, log_prob: {log_prob:.4f}")

    # 测试更新
    states = np.random.randn(100, 338).astype(np.float32)
    actions = np.random.randint(0, 50, 100)
    old_log_probs = np.random.randn(100).astype(np.float32)
    advantages = np.random.randn(100).astype(np.float32)
    returns = np.random.randn(100).astype(np.float32)

    policy_loss, value_loss = agent.update(
        states, actions, old_log_probs, advantages, returns,
        epochs=2, batch_size=32
    )
    print(f"更新完成 - Policy Loss: {policy_loss:.4f}, Value Loss: {value_loss:.4f}")

    print("\n✅ 所有测试通过！")
