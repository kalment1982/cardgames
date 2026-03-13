# 出牌显示布局优化

## 修改时间
2026-03-13

## 优化内容

### 1. 动态卡牌尺寸
根据出牌数量自动调整卡牌显示尺寸：
- **1-3张**：大尺寸 (70px × 105px)
- **4-8张**：中尺寸 (55px × 82px)
- **9张以上**：小尺寸 (45px × 67px)

### 2. 新的布局结构
```
                    北玩家信息
                 北玩家出的牌（居中）


西玩家信息  西玩家出的牌（左对齐）        东玩家出的牌（右对齐）  东玩家信息


                   南玩家出的牌（居中）
                  南玩家手牌
```

### 3. 布局特点
- **北玩家出牌区**：居中显示，支持换行
- **西/东玩家出牌区**：
  - 在同一行显示
  - 各占屏幕宽度的48%
  - 西玩家左对齐，东玩家右对齐
  - 支持自动换行（flex-wrap）
- **南玩家出牌区**：居中显示，支持换行

### 4. 玩家信息位置调整
- **北玩家**：top: 12%（更靠近出牌区）
- **西玩家**：left: 3%, top: 40%
- **东玩家**：right: 3%, top: 40%
- 所有玩家信息更紧凑（padding: 8px 16px）

## 技术实现

### CSS关键样式
```css
.play-area {
    position: absolute;
    top: 15%;
    left: 50%;
    transform: translateX(-50%);
    width: 90%;
    max-width: 1200px;
    height: 60%;
    display: flex;
    flex-direction: column;
    justify-content: space-between;
}

.played-cards-middle-row {
    display: flex;
    justify-content: space-between;
    width: 100%;
    gap: 20px;
}

.played-cards-west {
    display: flex;
    flex-wrap: wrap;
    gap: 5px;
    justify-content: flex-start;
    width: 48%;
}

.played-cards-east {
    display: flex;
    flex-wrap: wrap;
    gap: 5px;
    justify-content: flex-end;
    width: 48%;
}
```

### Razor代码
```csharp
private string GetCardSizeClass(int cardCount)
{
    if (cardCount <= 3)
        return "size-large";
    else if (cardCount <= 8)
        return "size-medium";
    else
        return "size-small";
}
```

## 优势

### 1. 自适应显示
- 少量牌时显示大，方便查看
- 大量牌时自动缩小，避免溢出
- 超过容器宽度时自动换行

### 2. 空间利用
- 西/东玩家共享中间行，节省垂直空间
- 各占48%宽度，留2%间隙，避免拥挤
- 出牌区占据60%屏幕高度，充分利用空间

### 3. 视觉清晰
- 玩家信息紧邻出牌区，关联性强
- 左右对齐方式符合视觉习惯
- 居中显示的北/南玩家出牌区对称美观

## 修复的其他问题

### CardMemory.cs编译错误
- 问题：TrickPlay.Position 不存在
- 修复：改为 TrickPlay.PlayerIndex
- 添加：using TractorGame.Core.Rules

## 测试建议

### 场景1：少量牌（1-3张）
- 验证卡牌显示为大尺寸
- 验证布局居中/对齐正确

### 场景2：中等数量（4-8张）
- 验证卡牌显示为中尺寸
- 验证不会溢出容器

### 场景3：大量牌（9张以上）
- 验证卡牌显示为小尺寸
- 验证自动换行功能
- 验证西/东玩家各占一半宽度

### 场景4：甩牌（10+张）
- 验证小尺寸卡牌能容纳所有牌
- 验证换行后的显示效果

---

**修改文件**：
- WebUI/wwwroot/css/game.css
- WebUI/Pages/GamePage.razor
- src/Core/AI/CardMemory.cs
