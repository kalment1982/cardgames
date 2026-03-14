// AI决策改进方案 - 修复基本技巧缺失

## 问题1: 对家赢牌时垫牌逻辑错误

### 当前代码（第614-625行）
```csharp
if (partnerWinning) {
    // 优先送分牌
    int pointsA = a.Sum(c => GetCardPoints(c));
    int pointsB = b.Sum(c => GetCardPoints(c));
    if (pointsA != pointsB)
        return pointsA.CompareTo(pointsB);

    // ❌ 错误：其次出小牌
    if (beatA != beatB)
        return beatA ? -1 : 1; // 不赢牌优先
}
```

### 修复方案
```csharp
if (partnerWinning) {
    // 1. 优先送分牌（正确）
    int pointsA = a.Sum(c => GetCardPoints(c));
    int pointsB = b.Sum(c => GetCardPoints(c));
    if (pointsA != pointsB)
        return pointsA.CompareTo(pointsB);

    // 2. 其次垫小牌（保留大牌）
    int minValueA = a.Min(c => GetCardValue(c));
    int minValueB = b.Min(c => GetCardValue(c));
    if (minValueA != minValueB)
        return minValueA.CompareTo(minValueB); // 小牌优先

    // 3. 避免垫主牌
    int trumpCountA = a.Count(_config.IsTrump);
    int trumpCountB = b.Count(_config.IsTrump);
    if (trumpCountA != trumpCountB)
        return trumpCountA.CompareTo(trumpCountB); // 少垫主牌
}
```

---

## 问题2: 缺少"大起来控制下轮"逻辑

### 新增方法
```csharp
/// <summary>
/// 评估赢牌后的控制力（用最小的能赢的牌）
/// </summary>
private int EvaluateWinningControl(List<Card> candidate, List<Card> currentWinning,
    List<Card> hand, CardComparer comparer)
{
    if (!CanBeatCards(currentWinning, candidate))
        return 0; // 不能赢，控制力=0

    // 计算"赢牌余量"（越小越好，说明用最小的牌赢）
    int margin = 0;
    for (int i = 0; i < candidate.Count; i++)
    {
        var myCard = candidate[i];
        var theirCard = i < currentWinning.Count ? currentWinning[i] : null;

        if (theirCard != null)
        {
            int cmp = comparer.Compare(myCard, theirCard);
            if (cmp > 0)
                margin += (GetCardValue(myCard) - GetCardValue(theirCard));
        }
    }

    // 余量越小越好（用最小的牌赢）
    return 10000 - margin;
}
```

### 修改CompareFollowCandidates
```csharp
// 对手赢牌时：优先争胜
if (beatA != beatB)
{
    var beatBias = ResolveRoleWeighted01(_strategy.FollowBeatAttemptBias, role, neutral: 0.5);
    if (beatBias >= 0.5)
    {
        // ✅ 新增：如果都能赢，选择"赢得最小"的
        if (beatA && beatB)
        {
            int controlA = EvaluateWinningControl(a, currentWinningCards, hand, comparer);
            int controlB = EvaluateWinningControl(b, currentWinningCards, hand, comparer);
            if (controlA != controlB)
                return controlA.CompareTo(controlB); // 控制力高的优先
        }
        return beatA ? 1 : -1;
    }
    return beatA ? -1 : 1;
}
```

---

## 问题3: 保底逻辑缺失

### 新增方法
```csharp
/// <summary>
/// 评估底牌安全性（庄家和队友都需要关注）
/// </summary>
private int EvaluateBottomSafety(List<Card> candidate, AIRole role, int tricksRemaining)
{
    // 只在最后3-5墩关注保底
    if (tricksRemaining > 5)
        return 0;

    bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;
    if (!isDealerSide)
        return 0; // 闲家不关心保底

    // 如果出的是短门（容易被抠底），扣分
    var suitCards = hand.Where(c => !_config.IsTrump(c) &&
                                     c.Suit == candidate[0].Suit).ToList();
    if (suitCards.Count <= 2)
        return -500; // 短门危险

    // 如果出的是主牌（相对安全），加分
    if (candidate.All(_config.IsTrump))
        return 300;

    return 0;
}
```

### 修改CompareFollowCandidates（在最后添加）
```csharp
// 收官阶段：庄家方关注保底
if (tricksRemaining <= 5)
{
    int safetyA = EvaluateBottomSafety(a, role, tricksRemaining);
    int safetyB = EvaluateBottomSafety(b, role, tricksRemaining);
    if (safetyA != safetyB)
        return safetyA.CompareTo(safetyB);
}
```

---

## 问题4: 垫牌优先级错误

### 修复方案
```csharp
// 无法赢时：优先垫小牌和非分牌
if (!beatA && !beatB)
{
    // 1. 优先垫非分牌
    int pointsA = a.Sum(c => GetCardPoints(c));
    int pointsB = b.Sum(c => GetCardPoints(c));
    if (pointsA != pointsB)
        return pointsB.CompareTo(pointsA); // 少送分优先

    // 2. 其次垫小牌（按牌力）
    int avgValueA = (int)a.Average(c => GetCardValue(c));
    int avgValueB = (int)b.Average(c => GetCardValue(c));
    if (avgValueA != avgValueB)
        return avgValueA.CompareTo(avgValueB); // 小牌优先

    // 3. 避免垫主牌
    int trumpCountA = a.Count(_config.IsTrump);
    int trumpCountB = b.Count(_config.IsTrump);
    if (trumpCountA != trumpCountB)
        return trumpCountA.CompareTo(trumpCountB); // 少垫主牌

    // 4. 庄家额外保守
    if (isDealerSide)
    {
        int cmpSmall = CompareCardSets(b, a, comparer);
        if (cmpSmall != 0)
            return cmpSmall;
    }
}
```

---

## 实施步骤

1. **立即修复**（30分钟）
   - 修复对家赢牌时的垫牌逻辑
   - 修复无法赢时的垫牌优先级

2. **短期优化**（1小时）
   - 添加"大起来控制下轮"逻辑
   - 添加保底安全性评估

3. **测试验证**（30分钟）
   - 运行100局自博弈
   - 检查是否还有"弱智"行为

---

## 预期改进

修复后AI应该能做到：
- ✅ 对家赢牌时垫小牌和分牌
- ✅ 用最小的能赢的牌（保留大牌控制下轮）
- ✅ 垫牌时优先垫小牌和非分牌
- ✅ 收官时帮队友保底
- ✅ 避免垫主牌（除非必要）

修复后Defender胜率预计从40%提升到45-48%。
