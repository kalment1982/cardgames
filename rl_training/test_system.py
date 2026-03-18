#!/usr/bin/env python3
"""
Phase 1 PPO system test script.
"""
import os
import sys
from pathlib import Path


SCRIPT_DIR = Path(__file__).resolve().parent
PROJECT_ROOT = SCRIPT_DIR.parent

def test_imports():
    """测试 Phase 1 相关模块导入"""
    print("Testing imports...")

    try:
        import torch
        print(f"  ✓ PyTorch {torch.__version__}")

        import numpy as np
        print(f"  ✓ NumPy {np.__version__}")

        import yaml
        print(f"  ✓ PyYAML")

        from state_encoder import StateEncoder
        print(f"  ✓ State Encoder")

        from action_mask import ACTION_DIM
        print(f"  ✓ Action Mask ({ACTION_DIM})")

        from engine_bridge import EngineBridge
        print(f"  ✓ Engine Bridge")

        from ppo_agent import PPOAgent
        print(f"  ✓ PPO Agent")

        from generate_warm_start_data import load_config as load_warm_start_config
        _ = load_warm_start_config
        print(f"  ✓ Warm-start data generator")

        from pretrain_bc import build_dataset
        _ = build_dataset
        print(f"  ✓ Warm-start BC pretrain")

        print("\n✓ All imports successful!\n")
        return True

    except Exception as e:
        print(f"\n✗ Import failed: {e}\n")
        return False


def test_config():
    """测试 Phase 1 配置文件"""
    print("Testing config...")

    try:
        import yaml
        with open(SCRIPT_DIR / 'phase1_config.yaml', 'r') as f:
            config = yaml.safe_load(f)

        print(f"  ✓ Config loaded")
        print(f"  - State dim: {config['ppo']['state_dim']}")
        print(f"  - Action dim: {config['ppo']['action_dim']}")
        print(f"  - Games/iter: {config['training']['games_per_iteration']}")
        print(f"  - Host path: {config['environment']['host_path']}")

        print("\n✓ Config valid!\n")
        return True

    except Exception as e:
        print(f"\n✗ Config test failed: {e}\n")
        return False


def test_state_encoder():
    """测试 Phase 1 状态编码器"""
    print("Testing state encoder...")

    try:
        from state_encoder import StateEncoder

        encoder = StateEncoder()
        state_vector = encoder.encode({}, [])

        print(f"  ✓ State encoded")
        print(f"  - Shape: {state_vector.shape}")
        print(f"  - Expected: (382,)")

        assert state_vector.shape == (382,), f"Wrong shape: {state_vector.shape}"

        print("\n✓ State encoder works!\n")
        return True

    except Exception as e:
        print(f"\n✗ State encoder test failed: {e}\n")
        return False


def test_ppo_agent():
    """测试 Phase 1 PPO 智能体"""
    print("Testing PPO agent...")

    try:
        import torch
        from ppo_agent import PPOAgent
        import numpy as np

        device = 'mps' if torch.backends.mps.is_available() else 'cpu'
        print(f"  - Using device: {device}")

        agent = PPOAgent(state_dim=382, action_dim=384, device=device)

        # 测试前向传播
        state = np.random.randn(382)
        action_mask = np.ones(384)

        action_idx, log_prob, value = agent.select_action(state, action_mask)

        print(f"  ✓ Agent created")
        print(f"  - Action: {action_idx}")
        print(f"  - Log prob: {log_prob:.4f}")
        print(f"  - Value: {value:.4f}")

        print("\n✓ PPO agent works!\n")
        return True

    except Exception as e:
        print(f"\n✗ PPO agent test failed: {e}\n")
        return False


def test_host_binary():
    """测试 Phase 1 Host 可执行文件"""
    print("Testing host binary...")

    try:
        host_path = PROJECT_ROOT / 'tools' / 'PpoEngineHost' / 'bin' / 'Release' / 'net6.0' / 'PpoEngineHost'
        if not host_path.exists():
            raise FileNotFoundError(host_path)

        print(f"  ✓ Host exists")
        print(f"  - Path: {host_path}")

        print("\n✓ Host binary ready!\n")
        return True

    except Exception as e:
        print(f"\n✗ Game engine test failed: {e}\n")
        return False


def test_teacher_action_bridge():
    """测试 warm-start 教师动作桥接"""
    print("Testing teacher-action bridge...")

    try:
        from engine_bridge import EngineBridge

        host_path = PROJECT_ROOT / 'tools' / 'PpoEngineHost' / 'bin' / 'Release' / 'net6.0' / 'PpoEngineHost'
        with EngineBridge(str(host_path)) as bridge:
            reset = bridge.reset(seed=0, ppo_seats=[0, 2], rule_ai_seats=[1, 3])
            env_id = reset["env_id"]
            teacher = bridge.get_teacher_action(env_id)

            action = teacher.get("teacher_action") or {}
            slot = action.get("slot")
            legal_slots = {item["slot"] for item in reset.get("legal_actions", [])}

            print(f"  ✓ Teacher action fetched")
            print(f"  - Current player: {teacher.get('current_player')}")
            print(f"  - Teacher slot: {slot}")

            assert slot in legal_slots, f"Teacher slot {slot} not in reset legal actions"

        print("\n✓ Teacher-action bridge works!\n")
        return True

    except Exception as e:
        print(f"\n✗ Teacher-action bridge test failed: {e}\n")
        return False


def test_directories():
    """测试 Phase 1 目录结构"""
    print("Testing directories...")

    dirs = [
        PROJECT_ROOT / 'checkpoints',
        PROJECT_ROOT / 'checkpoints' / 'phase1',
        PROJECT_ROOT / 'logs',
        PROJECT_ROOT / 'logs' / 'phase1',
    ]

    for d in dirs:
        if d.exists():
            print(f"  ✓ {d}")
        else:
            print(f"  ✗ {d} missing")
            return False

    print("\n✓ All directories exist!\n")
    return True


def main():
    """运行所有测试"""
    print("=" * 60)
    print("Phase 1 PPO System Test")
    print("=" * 60)
    print()

    tests = [
        ("Imports", test_imports),
        ("Config", test_config),
        ("Directories", test_directories),
        ("Host Binary", test_host_binary),
        ("Teacher Bridge", test_teacher_action_bridge),
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
        print("  1. Generate eval seeds:")
        print("     python3 generate_eval_seeds.py")
        print()
        print("  2. Run a smoke test:")
        print("     python3 train_phase1.py --max_iterations 1")
        print()
        print("  3. Start full training:")
        print("     python3 train_phase1.py")
        return 0
    else:
        print("✗ Some tests failed. Please fix the issues above.")
        return 1


if __name__ == "__main__":
    sys.exit(main())
