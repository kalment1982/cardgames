namespace TractorGame.Core.Models
{
    /// <summary>
    /// 花色
    /// </summary>
    public enum Suit
    {
        Spade = 0,    // 黑桃 ♠
        Heart = 1,    // 红桃 ♥
        Club = 2,     // 梅花 ♣
        Diamond = 3,  // 方块 ♦
        Joker = 4     // 王（大小王共用此花色）
    }

    /// <summary>
    /// 点数
    /// </summary>
    public enum Rank
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14,
        SmallJoker = 15,  // 小王
        BigJoker = 16     // 大王
    }

    /// <summary>
    /// 牌型类型
    /// </summary>
    public enum PatternType
    {
        Single,      // 单张
        Pair,        // 对子
        Tractor,     // 拖拉机（连对）
        Mixed        // 混合（甩牌用）
    }

    /// <summary>
    /// 牌的类别（主牌/副牌）
    /// </summary>
    public enum CardCategory
    {
        Trump,       // 主牌
        Suit         // 副牌
    }
}
