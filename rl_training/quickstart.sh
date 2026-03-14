#!/bin/bash
# 快速开始脚本

echo "=========================================="
echo "增强版RL训练系统 - 快速开始"
echo "=========================================="
echo ""

# 检查环境变量
echo "[1/5] 检查GMN配置..."
API_KEY=${CODEX_API_KEY:-$ANTHROPIC_AUTH_TOKEN}
API_BASE=${CODEX_API_BASE:-$ANTHROPIC_BASE_URL}

if [ -z "$API_KEY" ]; then
    echo "  ✗ 未设置 CODEX_API_KEY 或 ANTHROPIC_AUTH_TOKEN"
    echo "  请先配置训练所需的 API 凭据"
    exit 1
fi

if [ -z "$API_BASE" ]; then
    echo "  ✗ 未设置 CODEX_API_BASE 或 ANTHROPIC_BASE_URL"
    echo "  请先配置训练所需的 API 地址"
    exit 1
fi

echo "  ✓ GMN配置已设置"
echo "  - API Base: $API_BASE"
echo ""

# 测试API连接
echo "[2/5] 测试LLM API连接..."
python3 test_llm_api.py > /dev/null 2>&1
if [ $? -ne 0 ]; then
    echo "  ✗ API连接失败"
    echo "  请运行: python3 test_llm_api.py 查看详情"
    exit 1
fi
echo "  ✓ API连接成功"
echo ""

# 询问是否生成数据
echo "[3/5] 生成专家数据"
read -p "  生成多少局游戏数据？(默认: 100): " num_games
num_games=${num_games:-100}

echo "  开始生成 $num_games 局数据..."
echo "  预计成本: \$$(python3 -c "print(f'{$num_games * 0.1:.1f}')")"
read -p "  确认继续？(y/n): " confirm

if [ "$confirm" != "y" ]; then
    echo "  已取消"
    exit 0
fi

python3 llm_teacher.py --generate --num-games $num_games
if [ $? -ne 0 ]; then
    echo "  ✗ 数据生成失败"
    exit 1
fi
echo "  ✓ 数据生成完成"
echo ""

# 预训练
echo "[4/5] 预训练策略网络..."
python3 pretrain.py
if [ $? -ne 0 ]; then
    echo "  ✗ 预训练失败"
    exit 1
fi
echo "  ✓ 预训练完成"
echo ""

# PPO训练
echo "[5/5] 开始PPO训练..."
echo "  训练将在后台运行"
echo "  查看进度: tensorboard --logdir logs/tensorboard"
echo ""
read -p "  开始训练？(y/n): " confirm

if [ "$confirm" == "y" ]; then
    python3 train_enhanced.py
fi

echo ""
echo "=========================================="
echo "✓ 完成！"
echo "=========================================="
