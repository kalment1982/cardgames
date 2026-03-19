# PPO AI_MacMini训练迁移指南 v1.0

## 1. 目标

把当前 `PPO AI` 训练迁移到另一台 `Mac mini`，并支持两种启动方式：

1. 直接 shell 启动
2. 由远端 `Codex CLI` 自己接手启动

默认续训起点：

- `checkpoints/phase1_overnight_20260319_005351/best_model.pt`

---

## 2. 建议迁移方式

推荐顺序：

1. 在远端 `Mac mini` 上放好仓库
2. 确认默认续训 checkpoint 已同步
3. 运行准备脚本
4. 启动独立续训 run

---

## 3. 需要同步的关键内容

至少确保以下路径已同步到远端：

1. 代码仓库本身
2. `tools/PpoEngineHost/`
3. `rl_training/`
4. `checkpoints/phase1_overnight_20260319_005351/best_model.pt`
5. `rl_training/eval_seeds.txt`

如果远端直接 `git clone`，但 checkpoint 不在 Git 中，则单独补传 checkpoint 即可。

---

## 4. 远端准备

在远端仓库根目录执行：

```bash
bash scripts/setup_macmini_ppo.sh
```

这个脚本会完成：

1. 创建 / 复用 `.venv_phase2`
2. 安装 `rl_training/requirements.txt`
3. 固定 `setuptools<81`
4. 编译 `tools/PpoEngineHost`
5. 生成 / 校验 `eval_seeds.txt`

---

## 5. 直接 shell 启动

默认启动命令：

```bash
bash scripts/start_macmini_ppo_resume.sh
```

默认行为：

1. 从 `checkpoints/phase1_overnight_20260319_005351/best_model.pt` 续训
2. 自动生成独立 run tag
3. 单独生成日志目录和 checkpoint 目录
4. 自动启动 TensorBoard
5. 自动启动 Streamlit
6. 自动挂 `caffeinate`

默认端口：

1. TensorBoard: `6010`
2. Streamlit: `8503`

---

## 6. 由远端 Codex CLI 启动

如果远端也是 `Codex CLI` 管控，直接执行：

```bash
bash scripts/start_macmini_codex_ppo.sh
```

这个脚本会让远端 `Codex`：

1. 检查准备脚本和续训脚本
2. 运行环境准备
3. 启动续训
4. 校验 `run.out` 是否持续写入
5. 汇报 run tag、目录、PID 和可视化地址

要求：

1. 远端 `codex` 可执行
2. 远端 `codex` 具备高权限配置
3. 当前用户对仓库目录有写权限

---

## 7. 常用环境变量

如果默认值不合适，可先导出这些变量再启动：

```bash
export TRACTOR_PPO_RESUME_CHECKPOINT=/path/to/your_checkpoint.pt
export TRACTOR_PPO_MAX_ITERATIONS=5000
export TRACTOR_PPO_RUN_TAG=phase1_macmini_custom
export TRACTOR_PPO_TB_PORT=6012
export TRACTOR_PPO_STREAMLIT_PORT=8505
```

然后再执行：

```bash
bash scripts/start_macmini_ppo_resume.sh
```

---

## 8. 监控位置

一次启动后，重点看：

1. `logs/<run-tag>/run.out`
2. `logs/<run-tag>/training_log.csv`
3. `logs/<run-tag>/eval_summary.csv`
4. `checkpoints/<run-tag>/`

网页监控地址：

1. `http://<macmini-ip>:6010`
2. `http://<macmini-ip>:8503`

如果修改过端口，则用修改后的端口。

---

## 9. 停止某次 run

已提供停止脚本：

```bash
bash scripts/stop_macmini_ppo_run.sh <run-tag>
```

例如：

```bash
bash scripts/stop_macmini_ppo_run.sh phase1_macmini_20260319_120000
```

它会尝试停止：

1. training
2. tensorboard
3. streamlit
4. caffeinate

---

## 10. 推荐迁移命令示例

### 10.1 本地仓库 rsync 到远端

```bash
rsync -az --info=progress2 \
  /Users/karmy/Projects/CardGame/tractor/ \
  <user>@<macmini>:/Users/<user>/Projects/CardGame/tractor/
```

### 10.2 远端登录后直接启动

```bash
cd /Users/<user>/Projects/CardGame/tractor
bash scripts/start_macmini_ppo_resume.sh
```

### 10.3 远端登录后交给 Codex CLI

```bash
cd /Users/<user>/Projects/CardGame/tractor
bash scripts/start_macmini_codex_ppo.sh
```

---

## 11. 当前建议

当前推荐在 `Mac mini` 上使用：

1. 已修复桥接冲突后的代码
2. `best_model.pt` 作为续训起点
3. 独立 run 目录，不复用旧日志目录

这样可以避免：

1. 旧日志污染
2. 多训练实例覆盖同一 checkpoint
3. 旧的 `ACTION_SLOT_CONFLICT` 再次中断长跑
