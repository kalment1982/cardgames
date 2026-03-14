#!/usr/bin/env python3
"""
系统测试脚本 - 验证所有组件是否正常工作
"""
import os
import sys

def test_imports():
    """测试所有模块导入"""
    print("Testing imports...")

    try:
        import torch
        print(f"  ✓ PyTorch {torch.__version__}")

        import numpy as np
        print(f"  ✓ NumPy {np.__version__}")

        import yaml
        print(f"  ✓ PyYAML")

        import openai
        print(f"  ✓ OpenAI")

        import anthropic
        print(f"  ✓ Anthropic")

        from game_engine import TractorGame
        print(f"  ✓ Game Engine")

        from enhanced_state_encoder import EnhancedStateEncoder
        print(f"  ✓ Enhanced State Encoder")

        from ppo_agent import PPOAgent
        print(f"  ✓ PPO Agent")

        print("\n✓ All imports successful!\n")
        return True

    except Exception as e:
        print(f"\n✗ Import failed: {e}\n")
        return False


def test_config():
    """测试配置文件"""
    print("Testing config...")

    try:
        import yaml
        with open('config.yaml', 'r') as f:
            config = yaml.safe_load(f)

        print(f"  ✓ Config loaded")
        print(f"  - API type: {config['llm']['api_type']}")
        print(f"  - State dim: {config['state_encoding']['base_dim'] + config['state_encoding']['opponent_belief_dim'] + config['state_encoding']['play_pattern_dim']}")
        print(f"  - Workers: {config['ppo']['num_workers']}")
        print(f"  - Device: {config['system']['device']}")

        print("\n✓ Config valid!\n")
        return True

    except Exception as e:
        print(f"\n✗ Config test failed: {e}\n")
        return False


def test_state_encoder():
    """测试状态编码器"""
    print("Testing state encoder...")

    try:
        from game_engine import TractorGame
        from enhanced_state_encoder import EnhancedStateEncoder

        game = TractorGame()
        game.reset()

        encoder = EnhancedStateEncoder()
        state_vector = encoder.encode_state(game.state, 0)

        print(f"  ✓ State encoded")
        print(f"  - Shape: {state_vector.shape}")
        print(f"  - Expected: (412,)")

        assert state_vector.shape == (412,), f"Wrong shape: {state_vector.shape}"

        print("\n✓ State encoder works!\n")
        return True

    except Exception as e:
        print(f"\n✗ State encoder test failed: {e}\n")
        return False


def test_ppo_agent():
    """测试PPO智能体"""
    print("Testing PPO agent...")

    try:
        import torch
        from ppo_agent import PPOAgent
        import numpy as np

        device = 'mps' if torch.backends.mps.is_available() else 'cpu'
        print(f"  - Using device: {device}")

        agent = PPOAgent(state_dim=412, action_dim=1000, device=device)

        # 测试前向传播
        state = np.random.randn(412)
        action_mask = np.ones(1000)

        action_idx, log_prob = agent.select_action(state, action_mask)

        print(f"  ✓ Agent created")
        print(f"  - Action: {action_idx}")
        print(f"  - Log prob: {log_prob:.4f}")

        print("\n✓ PPO agent works!\n")
        return True

    except Exception as e:
        print(f"\n✗ PPO agent test failed: {e}\n")
        return False


def test_game_engine():
    """测试游戏引擎"""
    print("Testing game engine...")

    try:
        from game_engine import TractorGame

        game = TractorGame()
        game.reset()

        print(f"  ✓ Game created")
        print(f"  - Players: 4")
        print(f"  - Cards per player: {len(game.state.hands[0])}")

        # 测试一步
        player = game.current_player
        legal_actions = game.get_legal_actions(player)

        print(f"  - Legal actions: {len(legal_actions)}")

        if legal_actions:
            action = legal_actions[0]
            _, reward, done = game.step(player, action)
            print(f"  ✓ Step executed")
            print(f"  - Reward: {reward}")
            print(f"  - Done: {done}")

        print("\n✓ Game engine works!\n")
        return True

    except Exception as e:
        print(f"\n✗ Game engine test failed: {e}\n")
        return False


def test_directories():
    """测试目录结构"""
    print("Testing directories...")

    dirs = ['data', 'checkpoints', 'logs', 'logs/tensorboard']

    for d in dirs:
        if os.path.exists(d):
            print(f"  ✓ {d}")
        else:
            print(f"  ✗ {d} missing")
            return False

    print("\n✓ All directories exist!\n")
    return True


def main():
    """运行所有测试"""
    print("=" * 60)
    print("RL Training System Test")
    print("=" * 60)
    print()

    tests = [
        ("Imports", test_imports),
        ("Config", test_config),
        ("Directories", test_directories),
        ("Game Engine", test_game_engine),
        ("State Encoder", test_state_encoder),
        ("PPO Agent", test_ppo_agent),
    ]

    results = []

    for name, test_func in tests:
        print(f"[{name}]")
        print("-" * 60)
        result = test_func()
        results.append((name, result))
        print()

    # 总结
    print("=" * 60)
    print("Test Summary")
    print("=" * 60)

    for name, result in results:
        status = "✓ PASS" if result else "✗ FAIL"
        print(f"  {status}: {name}")

    print()

    all_passed = all(r for _, r in results)

    if all_passed:
        print("✓ All tests passed! System is ready.")
        print()
        print("Next steps:")
        print("  1. Set environment variables:")
        print("     export CODEX_API_KEY='your-key'")
        print("     export CODEX_API_BASE='your-url'")
        print()
        print("  2. Generate expert data:")
        print("     python llm_teacher.py --generate --num-games 100")
        print()
        print("  3. Pretrain:")
        print("     python pretrain.py")
        print()
        print("  4. Train:")
        print("     python train_enhanced.py")
        return 0
    else:
        print("✗ Some tests failed. Please fix the issues above.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
