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
        /// 获取牌的类别
        /// </summary>
        public CardCategory GetCardCategory(Card card)
        {
            return IsTrump(card) ? CardCategory.Trump : CardCategory.Suit;
        }
    }
}
