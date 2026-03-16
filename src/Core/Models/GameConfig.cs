namespace TractorGame.Core.Models
{
    /// <summary>
    /// 游戏配置
    /// </summary>
    public class GameConfig
    {
        /// <summary>
        /// 当前级牌
        /// </summary>
        public Rank LevelRank { get; set; }

        /// <summary>
        /// 主花色（null表示无主）
        /// </summary>
        public Suit? TrumpSuit { get; set; }

        /// <summary>
        /// 甩牌失败罚分
        /// </summary>
        public int ThrowFailPenalty { get; set; } = 0;

        /// <summary>
        /// 是否启用反底（一期关闭）
        /// </summary>
        public bool EnableCounterBottom { get; set; } = false;

        /// <summary>
        /// 判断是否为主牌
        /// </summary>
        public bool IsTrump(Card card)
        {
            // 大小王永远是主牌
            if (card.IsJoker) return true;

            // 级牌是主牌
            if (card.Rank == LevelRank) return true;

            // 主花色的牌是主牌（无主时无主花色）
            if (TrumpSuit.HasValue && card.Suit == TrumpSuit.Value)
                return true;

            return false;
        }

        /// <summary>
        /// 获取类别
        /// </summary>
        public CardCategory GetCardCategory(Card card)
        {
            return IsTrump(card) ? CardCategory.Trump : CardCategory.Suit;
        }

        /// <summary>
        /// 计算两副牌中主牌总数：大小王4张 + 级牌8张 + 主花色非级牌24张（有主时）
        /// </summary>
        public int GetTotalTrumpCount()
        {
            // 2副牌：大王2 + 小王2 = 4
            int total = 4;
            // 级牌：4花色 × 2副 = 8（含主花色级牌，不重复计）
            total += 8;
            // 主花色非级牌：(13 - 1) × 2 = 24
            if (TrumpSuit.HasValue)
                total += 24;
            return total;
        }
    }
}
