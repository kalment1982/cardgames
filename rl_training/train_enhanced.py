"""
增强版训练脚本 - PPO + 增强状态编码 + LLM预训练
"""
import glob
import torch
import numpy as np
import time
import os
import re
import yaml
from typing import List, Dict, Optional, Tuple
from concurrent.futures import ProcessPoolExecutor
from tqdm import tqdm
import psutil
from torch.utils.tensorboard import SummaryWriter

from game_engine import TractorGame, GamePhase
from enhanced_state_encoder import EnhancedStateEncoder
from ppo_agent import PPOAgent


def player_team_sign(player: int) -> float:
    """玩家所属队伍相对庄家方的符号。"""
    return 1.0 if player in [0, 2] else -1.0


def same_team(player_a: int, player_b: int) -> bool:
    """判断两个玩家是否同队。"""
    return (player_a % 2) == (player_b % 2)


class EnhancedPPOTrainer:
    """增强版PPO训练器"""

    def __init__(self, config_path: str = "config.yaml", resume_path: Optional[str] = None):
        # 加载配置
        with open(config_path, 'r', encoding='utf-8') as f:
            self.config = yaml.safe_load(f)

        self.config_path = config_path
        self.ppo_config = self.config['ppo']
        self.system_config = self.config['system']
        self.checkpoint_dir = 'checkpoints'

        # 设备
        self.device = torch.device(self.system_config['device'])
        print(f"Using device: {self.device}")

        # 状态维度
        state_config = self.config['state_encoding']
        self.state_dim = state_config['base_dim'] + \
                        state_config['opponent_belief_dim'] + \
                        state_config['play_pattern_dim']

        print(f"State dimension: {self.state_dim}")

        # 创建agent
        self.agent = PPOAgent(
            state_dim=self.state_dim,
            action_dim=1000,
            device=self.device,
            lr=self.ppo_config['learning_rate']
        )

        # 统计
        self.iteration = 0
        self.start_iteration = 0
        self.completed_iterations = 0
        self.total_games = 0

        self._initialize_model_state(resume_path)

        # TensorBoard
        self.writer = SummaryWriter(
            self.system_config['tensorboard_dir'],
            purge_step=self.start_iteration
        )

    def _initialize_model_state(self, resume_path: Optional[str]):
        """优先恢复检查点，否则加载预训练权重。"""
        checkpoint_path = resume_path or self._find_latest_checkpoint()

        if checkpoint_path:
            self.load_checkpoint(checkpoint_path)
            return

        pretrained_path = self.config['pretrain']['save_path']
        if os.path.exists(pretrained_path):
            print(f"Loading pretrained model from: {pretrained_path}")
            self.agent.network.load_state_dict(torch.load(pretrained_path, map_location=self.device))
            print("✓ Pretrained model loaded! Starting RL fine-tuning...")
        else:
            print("No pretrained model found, training from scratch...")

    def _find_latest_checkpoint(self) -> Optional[str]:
        """查找最新检查点，优先使用 latest.pt。"""
        latest_path = os.path.join(self.checkpoint_dir, 'latest.pt')
        if os.path.exists(latest_path):
            return latest_path

        pattern = os.path.join(self.checkpoint_dir, 'checkpoint_*.pt')
        candidates = []
        for path in glob.glob(pattern):
            match = re.search(r'checkpoint_(\d+)\.pt$', os.path.basename(path))
            if match:
                candidates.append((int(match.group(1)), path))

        if not candidates:
            return None

        candidates.sort(key=lambda item: item[0])
        return candidates[-1][1]

    def _infer_completed_iterations(self, checkpoint: Dict, checkpoint_path: str) -> int:
        """兼容旧检查点格式，推断已完成的迭代数。"""
        if 'completed_iterations' in checkpoint:
            return int(checkpoint['completed_iterations'])

        filename_match = re.search(r'checkpoint_(\d+)\.pt$', os.path.basename(checkpoint_path))
        if filename_match:
            return int(filename_match.group(1))

        return int(checkpoint.get('iteration', 0))

    def load_checkpoint(self, checkpoint_path: str):
        """加载检查点并恢复训练进度。"""
        print(f"Loading checkpoint from: {checkpoint_path}")
        checkpoint = torch.load(checkpoint_path, map_location=self.device)

        self.agent.network.load_state_dict(checkpoint['model_state_dict'])

        optimizer_state = checkpoint.get('optimizer_state_dict')
        if optimizer_state is not None:
            self.agent.optimizer.load_state_dict(optimizer_state)

        self.completed_iterations = self._infer_completed_iterations(checkpoint, checkpoint_path)
        self.start_iteration = self.completed_iterations
        self.iteration = self.completed_iterations

        default_total_games = self.completed_iterations * self.ppo_config['games_per_iteration']
        self.total_games = int(checkpoint.get('total_games', default_total_games))

        print(f"✓ Checkpoint loaded! Completed iterations: {self.completed_iterations}")
        print(f"  Total games so far: {self.total_games}")

    def train(self):
        """主训练循环"""
        num_iterations = self.ppo_config['num_iterations']
        games_per_iteration = self.ppo_config['games_per_iteration']

        if self.start_iteration >= num_iterations:
            print("Configured iterations already completed. Nothing to do.")
            self.writer.close()
            return

        print(f"\nStarting PPO training...")
        print(f"  Total iterations: {num_iterations}")
        print(f"  Games per iteration: {games_per_iteration}")
        print(f"  Workers: {self.ppo_config['num_workers']}")
        print(f"  Starting from iteration: {self.start_iteration + 1}")
        print()

        try:
            for iteration in range(self.start_iteration, num_iterations):
                self.iteration = iteration
                start_time = time.time()

                # 1. 收集经验
                print(f"\n[Iteration {iteration+1}/{num_iterations}] Collecting experience...")
                trajectories = self.collect_trajectories(games_per_iteration)

                # 2. 计算优势和回报
                print("Computing advantages...")
                states, actions, old_log_probs, advantages, returns, action_masks = \
                    self.process_trajectories(trajectories)

                # 3. PPO更新
                print("Updating policy...")
                policy_loss, value_loss = self.update_policy(
                    states, actions, old_log_probs, advantages, returns, action_masks
                )

                # 4. 统计和日志
                elapsed = time.time() - start_time
                self.completed_iterations = iteration + 1
                self.log_iteration(iteration, trajectories, policy_loss, value_loss, elapsed)

                # 5. 评估
                if self.completed_iterations % self.system_config['eval_interval'] == 0:
                    self.evaluate()

                # 6. 保存
                if self.completed_iterations % self.system_config['save_interval'] == 0:
                    self.save_checkpoint(self.completed_iterations)

            print("\n✓ Training complete!")
            self.save_checkpoint(self.completed_iterations)
        except KeyboardInterrupt:
            print("\n⚠️ Training interrupted. Saving latest checkpoint...")
            self.save_checkpoint(self.completed_iterations, interrupted=True)
            print("✓ Latest checkpoint saved. You can resume later.")
        finally:
            self.writer.close()

    def collect_trajectories(self, num_games: int) -> List[Dict]:
        """并行收集游戏轨迹"""
        # 获取当前模型参数
        model_state_dict = self.agent.network.state_dict()

        # 并行模拟游戏
        trajectories = []
        num_workers = self.ppo_config['num_workers']

        with ProcessPoolExecutor(max_workers=num_workers) as executor:
            futures = []
            for i in range(num_games):
                seed = self.total_games + i
                future = executor.submit(simulate_game_worker, model_state_dict, seed, self.state_dim)
                futures.append(future)

            # 收集结果
            for future in tqdm(futures, desc="Simulating games"):
                trajectory = future.result()
                trajectories.append(trajectory)

        self.total_games += num_games
        return trajectories

    def process_trajectories(self, trajectories: List[Dict]) -> Tuple:
        """处理轨迹，计算优势和回报"""
        all_states = []
        all_actions = []
        all_old_log_probs = []
        all_advantages = []
        all_returns = []
        all_action_masks = []

        gamma = self.ppo_config['gamma']
        gae_lambda = self.ppo_config['gae_lambda']

        for traj in trajectories:
            states = traj['states']
            actions = traj['actions']
            rewards = traj['rewards']
            log_probs = traj['log_probs']
            action_masks = traj['action_masks']
            players = traj['players']

            # 计算values
            with torch.no_grad():
                states_array = np.asarray(states, dtype=np.float32)
                states_tensor = torch.as_tensor(states_array, device=self.device)
                _, values = self.agent.network(states_tensor)
                values = values.cpu().numpy().flatten()

            # GAE计算
            advantages = np.zeros(len(rewards))
            returns = np.zeros(len(rewards))

            gae = 0.0
            for t in reversed(range(len(rewards))):
                current_player = players[t]
                reward = player_team_sign(current_player) * rewards[t]

                if t == len(rewards) - 1:
                    perspective_factor = 0.0
                    next_value = 0.0
                else:
                    next_player = players[t + 1]
                    perspective_factor = 1.0 if same_team(current_player, next_player) else -1.0
                    next_value = perspective_factor * values[t + 1]

                delta = reward + gamma * next_value - values[t]
                gae = delta + gamma * gae_lambda * perspective_factor * gae
                advantages[t] = gae
                returns[t] = advantages[t] + values[t]

            all_states.extend(states_array)
            all_actions.extend(actions)
            all_old_log_probs.extend(log_probs)
            all_advantages.extend(advantages)
            all_returns.extend(returns)
            all_action_masks.extend(action_masks)

        # 转换为tensor
        states = torch.as_tensor(np.asarray(all_states, dtype=np.float32), device=self.device)
        actions = torch.as_tensor(np.asarray(all_actions, dtype=np.int64), device=self.device)
        old_log_probs = torch.as_tensor(np.asarray(all_old_log_probs, dtype=np.float32), device=self.device)
        advantages = torch.as_tensor(np.asarray(all_advantages, dtype=np.float32), device=self.device)
        returns = torch.as_tensor(np.asarray(all_returns, dtype=np.float32), device=self.device)
        action_masks = torch.as_tensor(np.asarray(all_action_masks, dtype=np.float32), device=self.device)

        # 标准化advantages
        advantages = (advantages - advantages.mean()) / (advantages.std() + 1e-8)

        return states, actions, old_log_probs, advantages, returns, action_masks

    def update_policy(self, states, actions, old_log_probs, advantages, returns, action_masks):
        """PPO策略更新"""
        batch_size = self.ppo_config['batch_size']
        ppo_epochs = self.ppo_config['ppo_epochs']
        clip_epsilon = self.ppo_config['clip_epsilon']

        dataset_size = states.size(0)
        indices = np.arange(dataset_size)

        total_policy_loss = 0
        total_value_loss = 0
        num_updates = 0

        for epoch in range(ppo_epochs):
            np.random.shuffle(indices)

            for start in range(0, dataset_size, batch_size):
                end = min(start + batch_size, dataset_size)
                batch_indices = indices[start:end]

                batch_states = states[batch_indices]
                batch_actions = actions[batch_indices]
                batch_old_log_probs = old_log_probs[batch_indices]
                batch_advantages = advantages[batch_indices]
                batch_returns = returns[batch_indices]
                batch_action_masks = action_masks[batch_indices]

                # 计算新的log_probs和values
                action_logits, values = self.agent.network(batch_states)

                # 应用action mask
                action_logits = action_logits + (batch_action_masks - 1) * 1e9

                # 计算log_probs
                action_probs = torch.softmax(action_logits, dim=-1)
                dist = torch.distributions.Categorical(action_probs)
                new_log_probs = dist.log_prob(batch_actions)
                entropy = dist.entropy().mean()

                # PPO loss
                ratio = torch.exp(new_log_probs - batch_old_log_probs)
                surr1 = ratio * batch_advantages
                surr2 = torch.clamp(ratio, 1 - clip_epsilon, 1 + clip_epsilon) * batch_advantages
                policy_loss = -torch.min(surr1, surr2).mean()

                # Value loss
                value_loss = 0.5 * (returns[batch_indices] - values.squeeze()).pow(2).mean()

                # Total loss
                loss = policy_loss + \
                       self.ppo_config['value_loss_coef'] * value_loss - \
                       self.ppo_config['entropy_coef'] * entropy

                # 更新
                self.agent.optimizer.zero_grad()
                loss.backward()
                torch.nn.utils.clip_grad_norm_(
                    self.agent.network.parameters(),
                    self.ppo_config['max_grad_norm']
                )
                self.agent.optimizer.step()

                total_policy_loss += policy_loss.item()
                total_value_loss += value_loss.item()
                num_updates += 1

        avg_policy_loss = total_policy_loss / num_updates
        avg_value_loss = total_value_loss / num_updates

        return avg_policy_loss, avg_value_loss

    def log_iteration(self, iteration, trajectories, policy_loss, value_loss, elapsed):
        """记录迭代统计"""
        # 计算平均奖励
        avg_reward = np.mean([sum(traj['rewards']) for traj in trajectories])
        avg_length = np.mean([len(traj['rewards']) for traj in trajectories])

        # 系统资源
        cpu_percent = psutil.cpu_percent()
        memory_percent = psutil.virtual_memory().percent

        # 打印
        print(f"\n[Iteration {iteration+1}] Summary:")
        print(f"  Avg Reward: {avg_reward:.2f}")
        print(f"  Avg Length: {avg_length:.1f}")
        print(f"  Policy Loss: {policy_loss:.4f}")
        print(f"  Value Loss: {value_loss:.4f}")
        print(f"  Time: {elapsed:.1f}s")
        print(f"  CPU: {cpu_percent:.1f}%, Memory: {memory_percent:.1f}%")

        # TensorBoard
        step = iteration + 1
        self.writer.add_scalar('Train/AvgReward', avg_reward, step)
        self.writer.add_scalar('Train/AvgLength', avg_length, step)
        self.writer.add_scalar('Train/PolicyLoss', policy_loss, step)
        self.writer.add_scalar('Train/ValueLoss', value_loss, step)
        self.writer.add_scalar('System/CPU', cpu_percent, step)
        self.writer.add_scalar('System/Memory', memory_percent, step)

    def evaluate(self):
        """评估当前策略"""
        print("\nEvaluating...")
        # TODO: 实现评估逻辑
        pass

    def save_checkpoint(self, completed_iterations: int, interrupted: bool = False):
        """保存检查点"""
        os.makedirs(self.checkpoint_dir, exist_ok=True)

        checkpoint_data = {
            'iteration': completed_iterations,
            'completed_iterations': completed_iterations,
            'total_games': self.total_games,
            'model_state_dict': self.agent.network.state_dict(),
            'optimizer_state_dict': self.agent.optimizer.state_dict(),
            'config_path': self.config_path,
        }

        latest_path = os.path.join(self.checkpoint_dir, 'latest.pt')
        torch.save(checkpoint_data, latest_path)

        checkpoint_path = os.path.join(self.checkpoint_dir, f'checkpoint_{completed_iterations}.pt')
        torch.save(checkpoint_data, checkpoint_path)

        suffix = " (interrupted)" if interrupted else ""
        print(f"✓ Checkpoint saved: {checkpoint_path}{suffix}")


def simulate_game_worker(model_state_dict: dict, seed: int, state_dim: int) -> Dict:
    """工作进程：模拟一局游戏"""
    # 重建agent
    agent = PPOAgent(state_dim=state_dim, action_dim=1000, device='cpu')
    agent.network.load_state_dict(model_state_dict)

    # 创建游戏
    game = TractorGame()
    game.reset()
    game.state.phase = GamePhase.PLAYING
    game.state.current_player = 0

    # 编码器
    encoder = EnhancedStateEncoder()

    trajectory = {
        'states': [],
        'actions': [],
        'rewards': [],
        'log_probs': [],
        'action_masks': [],
        'players': []
    }

    while not game.is_terminal():
        player = game.current_player

        # 编码状态
        state_vector = encoder.encode_state(game.state, player)

        # 获取合法动作
        legal_actions = game.get_legal_actions(player)
        action_mask = np.zeros(1000)
        for i in range(min(len(legal_actions), 1000)):
            action_mask[i] = 1.0

        # 选择动作
        action_idx, log_prob = agent.select_action(
            state_vector,
            action_mask,
            deterministic=False
        )

        # 执行动作
        if action_idx < len(legal_actions):
            action = legal_actions[action_idx]
        else:
            action = legal_actions[0]

        _, reward, _ = game.step(player, action)

        # 记录
        trajectory['states'].append(state_vector)
        trajectory['actions'].append(action_idx)
        trajectory['rewards'].append(reward)
        trajectory['log_probs'].append(log_prob)
        trajectory['action_masks'].append(action_mask)
        trajectory['players'].append(player)

    return trajectory


if __name__ == "__main__":
    import argparse

    parser = argparse.ArgumentParser(description="Enhanced PPO Training")
    parser.add_argument("--config", type=str, default="config.yaml", help="Config file path")
    parser.add_argument("--resume", type=str, default=None, help="Checkpoint path to resume from")

    args = parser.parse_args()

    trainer = EnhancedPPOTrainer(args.config, resume_path=args.resume)
    trainer.train()
