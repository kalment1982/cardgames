"""
主训练脚本 - PPO强化学习训练
"""
import torch
import numpy as np
import time
import os
from typing import List, Dict, Tuple
from concurrent.futures import ProcessPoolExecutor
from tqdm import tqdm
import psutil

from game_engine import TractorGame, GamePhase
from state_encoder import StateEncoder, ActionEncoder
from ppo_agent import PPOAgent


# ============ 配置 ============
CONFIG = {
    'device': 'mps' if torch.backends.mps.is_available() else 'cpu',
    'num_workers': 8,  # 并行进程数
    'batch_size': 256,  # 训练批次大小
    'learning_rate': 3e-4,
    'gamma': 0.99,  # 折扣因子
    'gae_lambda': 0.95,  # GAE参数
    'clip_epsilon': 0.2,  # PPO裁剪
    'epochs_per_iteration': 10,
    'games_per_iteration': 1000,
    'total_iterations': 1000,
    'eval_interval': 10,  # 每10次迭代评估一次
    'save_interval': 100,  # 每100次迭代保存一次
    'checkpoint_dir': 'checkpoints',
}


class RewardShaper:
    """奖励塑形"""

    @staticmethod
    def calculate_step_reward(game: TractorGame, player: int, won_trick: bool, points_won: int) -> float:
        """计算单步奖励"""
        reward = 0.0

        role = game.get_role(player)

        if won_trick:
            # 赢墩奖励
            reward += 0.5

            # 得分奖励
            reward += points_won * 0.05

            # 控制权奖励
            reward += 0.2

        return reward

    @staticmethod
    def calculate_final_reward(game: TractorGame, player: int) -> float:
        """计算最终奖励"""
        role = game.get_role(player)

        # 判断胜负
        dealer_won = game.state.dealer_score > game.state.defender_score

        if role.value <= 1:  # 庄家方
            return 10.0 if dealer_won else -10.0
        else:  # 闲家方
            return 10.0 if not dealer_won else -10.0


def simulate_game_worker(model_state_dict: dict, seed: int) -> List[Dict]:
    """工作进程：模拟一局游戏

    Args:
        model_state_dict: 模型参数
        seed: 随机种子

    Returns:
        trajectory: 游戏轨迹
    """
    # 重建agent（每个进程独立）
    agent = PPOAgent(device='cpu')  # 工作进程使用CPU
    agent.load_state_dict(model_state_dict)

    # 创建游戏
    game = TractorGame(seed=seed)

    # 简化：直接进入出牌阶段
    game.state.phase = GamePhase.PLAYING
    game.state.current_player = 0

    # 编码器
    state_encoder = StateEncoder()
    action_encoder = ActionEncoder()

    trajectory = []

    while not game.is_terminal():
        current_player = game.state.current_player

        # 编码状态
        state = state_encoder.encode(game.state, current_player)

        # 获取合法动作
        legal_actions = game.get_legal_actions(current_player)
        if not legal_actions:
            break

        # 创建动作掩码（简化：用动作索引）
        action_mask = np.zeros(108, dtype=bool)
        for i in range(min(len(legal_actions), 108)):
            action_mask[i] = True

        # 选择动作
        action_index, log_prob = agent.select_action(state, action_mask)

        # 解码动作
        action_index = min(action_index, len(legal_actions) - 1)
        action = legal_actions[action_index]

        # 执行动作
        try:
            new_state, step_reward, done = game.step(current_player, action)

            # 记录轨迹
            trajectory.append({
                'player': current_player,
                'state': state,
                'action': action_index,
                'log_prob': log_prob,
                'action_mask': action_mask,
                'reward': step_reward,
                'done': done,
            })

            if done:
                # 添加最终奖励
                final_reward = RewardShaper.calculate_final_reward(game, current_player)
                trajectory[-1]['reward'] += final_reward
                break

        except Exception as e:
            # 出错则结束
            print(f"游戏执行出错: {e}")
            break

    return trajectory


class PPOTrainer:
    """PPO训练器"""

    def __init__(self, config: dict):
        self.config = config
        self.device = torch.device(config['device'])

        # 创建检查点目录
        os.makedirs(config['checkpoint_dir'], exist_ok=True)

        # Agent
        self.agent = PPOAgent(device=config['device'], lr=config['learning_rate'])

        # 并行执行器
        self.executor = ProcessPoolExecutor(max_workers=config['num_workers'])

        # 统计
        self.iteration = 0
        self.total_games = 0

        print(f"🚀 PPO训练器初始化完成")
        print(f"   设备: {self.device}")
        print(f"   并行进程: {config['num_workers']}")
        print(f"   批次大小: {config['batch_size']}")

    def train(self):
        """主训练循环"""
        print(f"\n{'='*60}")
        print(f"开始训练 - 目标: {self.config['total_iterations']} 次迭代")
        print(f"{'='*60}\n")

        for iteration in range(self.config['total_iterations']):
            iter_start = time.time()

            # 1. 收集数据
            print(f"\n[Iter {iteration}] 收集数据...")
            trajectories = self.collect_trajectories()

            if not trajectories:
                print(f"[Iter {iteration}] ⚠️  没有收集到数据，跳过")
                continue

            # 2. 计算优势
            print(f"[Iter {iteration}] 计算优势...")
            advantages, returns = self.compute_advantages(trajectories)

            # 3. 更新策略
            print(f"[Iter {iteration}] 更新策略...")
            policy_loss, value_loss = self.update_policy(trajectories, advantages, returns)

            # 4. 评估
            if iteration % self.config['eval_interval'] == 0:
                print(f"[Iter {iteration}] 评估...")
                metrics = self.evaluate()
                self.log_metrics(iteration, metrics, policy_loss, value_loss)

            # 5. 保存
            if iteration % self.config['save_interval'] == 0:
                self.save_checkpoint(iteration)

            iter_time = time.time() - iter_start
            print(f"[Iter {iteration}] ✅ 完成 - 耗时: {iter_time:.1f}s")

            # 显示系统状态
            if iteration % 10 == 0:
                self.log_system_status()

            self.iteration += 1

        print(f"\n{'='*60}")
        print(f"训练完成！总游戏数: {self.total_games}")
        print(f"{'='*60}\n")

    def collect_trajectories(self) -> List[Dict]:
        """并行收集游戏轨迹"""
        num_games = self.config['games_per_iteration']
        num_workers = self.config['num_workers']

        # 获取模型参数
        model_state = self.agent.get_state_dict()

        # 提交任务
        futures = []
        for i in range(num_games):
            seed = self.total_games + i
            future = self.executor.submit(simulate_game_worker, model_state, seed)
            futures.append(future)

        # 收集结果（带进度条）
        all_trajectories = []
        for future in tqdm(futures, desc="收集游戏", ncols=80):
            try:
                trajectory = future.result(timeout=30)
                all_trajectories.extend(trajectory)
            except Exception as e:
                print(f"⚠️  游戏模拟失败: {e}")

        self.total_games += num_games
        return all_trajectories

    def compute_advantages(self, trajectories: List[Dict]) -> Tuple[np.ndarray, np.ndarray]:
        """计算GAE优势函数"""
        if not trajectories:
            return np.array([]), np.array([])

        # 提取数据
        states = np.array([t['state'] for t in trajectories])
        rewards = np.array([t['reward'] for t in trajectories])

        # 计算价值
        states_tensor = torch.FloatTensor(states).to(self.device)
        with torch.no_grad():
            _, values = self.agent.network(states_tensor)
            values = values.squeeze().cpu().numpy()

        # GAE计算
        advantages = np.zeros_like(rewards)
        returns = np.zeros_like(rewards)

        gae = 0
        for t in reversed(range(len(trajectories))):
            if trajectories[t]['done']:
                next_value = 0
            elif t == len(trajectories) - 1:
                next_value = 0
            else:
                next_value = values[t + 1]

            delta = rewards[t] + self.config['gamma'] * next_value - values[t]
            gae = delta + self.config['gamma'] * self.config['gae_lambda'] * gae

            advantages[t] = gae
            returns[t] = gae + values[t]

        return advantages, returns

    def update_policy(self, trajectories: List[Dict], advantages: np.ndarray, returns: np.ndarray) -> Tuple[float, float]:
        """更新策略"""
        if not trajectories:
            return 0.0, 0.0

        # 准备数据
        states = np.array([t['state'] for t in trajectories])
        actions = np.array([t['action'] for t in trajectories])
        old_log_probs = np.array([t['log_prob'] for t in trajectories])
        action_masks = np.array([t['action_mask'] for t in trajectories])

        # PPO更新
        policy_loss, value_loss, _entropy = self.agent.update(
            states, actions, old_log_probs, advantages, returns, action_masks,
            epochs=self.config['epochs_per_iteration'],
            batch_size=self.config['batch_size']
        )

        return policy_loss, value_loss

    def evaluate(self, num_games=100) -> Dict:
        """评估当前策略"""
        # TODO: 实现完整评估
        # 简化版：返回模拟指标
        return {
            'win_rate': 0.5,
            'avg_score': 0,
            'dealer_win_rate': 0.5,
            'defender_win_rate': 0.5,
        }

    def log_metrics(self, iteration: int, metrics: Dict, policy_loss: float, value_loss: float):
        """记录指标"""
        print(f"\n{'='*60}")
        print(f"📊 Iteration {iteration} | 总游戏数: {self.total_games}")
        print(f"{'='*60}")
        print(f"Win Rate:     {metrics['win_rate']:.2%}")
        print(f"Dealer WR:    {metrics['dealer_win_rate']:.2%}")
        print(f"Defender WR:  {metrics['defender_win_rate']:.2%}")
        print(f"Policy Loss:  {policy_loss:.4f}")
        print(f"Value Loss:   {value_loss:.4f}")
        print(f"{'='*60}\n")

    def log_system_status(self):
        """记录系统状态"""
        cpu_percent = psutil.cpu_percent(interval=0.1)
        memory = psutil.virtual_memory()

        print(f"\n💻 系统状态:")
        print(f"   CPU: {cpu_percent:.1f}%")
        print(f"   内存: {memory.percent:.1f}% ({memory.used/1024**3:.1f}GB / {memory.total/1024**3:.1f}GB)")

    def save_checkpoint(self, iteration: int):
        """保存检查点"""
        checkpoint_path = os.path.join(
            self.config['checkpoint_dir'],
            f'checkpoint_iter{iteration}.pth'
        )
        self.agent.save(checkpoint_path)
        print(f"💾 保存检查点: {checkpoint_path}")

    def __del__(self):
        """清理资源"""
        if hasattr(self, 'executor'):
            self.executor.shutdown(wait=False)


if __name__ == '__main__':
    print("="*60)
    print("拖拉机 RL 训练 - PPO算法")
    print("="*60)

    # 检查设备
    if torch.backends.mps.is_available():
        print("✅ MPS (M4 GPU) 可用")
    else:
        print("⚠️  MPS不可用，使用CPU")
        CONFIG['device'] = 'cpu'

    # 创建训练器
    trainer = PPOTrainer(CONFIG)

    # 开始训练
    try:
        trainer.train()
    except KeyboardInterrupt:
        print("\n\n⚠️  训练被用户中断")
    except Exception as e:
        print(f"\n\n❌ 训练出错: {e}")
        import traceback
        traceback.print_exc()
    finally:
        print("\n训练结束")
