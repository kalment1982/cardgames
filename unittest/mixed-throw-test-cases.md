# 混合甩牌测试用例（扩充版）

## 目标
验证混合甩牌在规则层和集成层的正确性，覆盖成功、失败、回退三大路径。

## 测试约束
- 仅新增/修改 `unittest` 目录下测试与文档。
- 不修改游戏业务代码（`src` 目录）。

## 覆盖接口
- `ThrowValidator`
  - `IsThrowSuccessful`
  - `AnalyzeThrow`
  - `CanBeatThrow`
  - `DecomposeThrow`
  - `GetFallbackPlay`
  - `GetSmallestCard`
- `PlayValidator`
  - `IsValidPlayEx(hand, cards, otherHands)`
  - `ValidatePattern`
- `Game`
  - `PlayCardsEx`（甩牌失败回退链路）

## 用例总数
- 共 `37` 条（MT-01 ~ MT-37）

## 用例清单

### A. ThrowValidator 判定（MT-01 ~ MT-12）

| 用例ID | 场景 | 预期 |
|---|---|---|
| MT-01 | 甩牌 `null` | 失败 |
| MT-02 | 甩牌空列表 | 失败 |
| MT-03 | 甩牌含混花/混主副 | 失败 |
| MT-04 | 无跟牌信息（`followPlays=null`） | 成功 |
| MT-05 | 跟牌列表含 `null/empty` 项 | 忽略无效项并成功 |
| MT-06 | 单牌子结构可被拦截 | 失败，`blocked_component_type=Single` |
| MT-07 | 对子子结构可被拦截 | 失败，`blocked_component_type=Pair` |
| MT-08 | 拖拉机子结构可被拦截 | 失败，`blocked_component_type=Tractor` |
| MT-09 | 多家可拦，命中首个可拦家 | `follower_hand_index=0` |
| MT-10 | 跟牌仅“同强度”无更大牌 | 成功 |
| MT-11 | 跟牌拖拉机长度不匹配 | 成功 |
| MT-12 | 全主混合甩牌，无更大主牌可拦 | 成功 |

### B. ThrowValidator 分解/回退（MT-13 ~ MT-18）

| 用例ID | 场景 | 预期 |
|---|---|---|
| MT-13 | 拖拉机+对子+单牌混合分解 | 顺序：拖拉机优先，其次对子，再单牌 |
| MT-14 | 非同花/同主甩牌分解 | 返回空分解 |
| MT-15 | 回退优先级：有单牌时 | 回退为最小单牌 |
| MT-16 | 无单牌，仅对子 | 回退为最小对子 |
| MT-17 | 仅拖拉机组件 | 回退为最小拖拉机 |
| MT-18 | 回退不可用 | `GetSmallestCard` 返回 `null` |

### C. PlayValidator 判定（MT-19 ~ MT-33）

| 用例ID | 场景 | 预期 |
|---|---|---|
| MT-19 | 混合甩牌无人可拦 | 成功 |
| MT-20 | 单牌子结构被拦 | `THROW_NOT_MAX` + `Single` |
| MT-21 | 对子子结构被拦 | `THROW_NOT_MAX` + `Pair` |
| MT-22 | 拖拉机子结构被拦 | `THROW_NOT_MAX` + `Tractor` |
| MT-23 | 不提供 `otherHands` | 仅基础校验通过即成功 |
| MT-24 | 出对子（2张）且可被理论拦截 | 仍成功（2张不走甩牌拦截） |
| MT-25 | 出拖拉机且可被理论拦截 | 仍成功（拖拉机不走甩牌拦截） |
| MT-26 | 混花混主副牌型 | `PLAY_PATTERN_INVALID` |
| MT-27 | 出牌不在手牌中 | `CARD_NOT_IN_HAND` |
| MT-28 | 空出牌 | `PLAY_PATTERN_INVALID` |
| MT-29 | 两张非对子模式校验 | `ValidatePattern=false` |
| MT-30 | 同花混合甩牌模式校验 | `ValidatePattern=true` |
| MT-31 | `CanBeatThrow` 输入非法甩牌 | 返回 `false` |
| MT-32 | `CanBeatThrow` 任一子结构可拦 | 返回 `true` |
| MT-33 | `GetSmallestCard` 从有效回退中取最小牌 | 返回最小单牌 |

### D. Game.PlayCardsEx 集成（MT-34 ~ MT-37）

| 用例ID | 场景 | 预期 |
|---|---|---|
| MT-34 | 混合甩牌失败（含单牌） | 自动回退最小单张并成功出牌 |
| MT-35 | 混合甩牌失败（无单牌） | 自动回退最小对子并成功出牌 |
| MT-36 | 混合甩牌失败（仅拖拉机组件） | 自动回退最小拖拉机并成功出牌 |
| MT-37 | 混合甩牌成功 | 按原始选牌完整出牌 |
