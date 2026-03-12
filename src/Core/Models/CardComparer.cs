using System.Collections.Generic;

namespace TractorGame.Core.Models
{
    /// <summary>
    /// 卡牌比较器（用于排序）
    /// </summary>
    public class CardComparer : IComparer<Card>
    {
        private readonly GameConfig _config;

        public CardComparer(GameConfig config)
        {
            _config = config;
        }

        public int Compare(Card x, Card y)
        {
            if (x == null || y == null) return 0;

            bool xIsTrump = _config.IsTrump(x);
            bool yIsTrump = _config.IsTrump(y);

            // 主牌 > 副牌
            if (xIsTrump && !yIsTrump) return 1;
            if (!xIsTrump && yIsTrump) return -1;

            // 都是主牌，按主牌序比较
            if (xIsTrump && yIsTrump)
                return CompareTrump(x, y);

            // 都是副牌，按副牌序比较
            return CompareSuit(x, y);
        }

        private int CompareTrump(Card x, Card y)
        {
            // 大王 > 小王
            if (x.Rank == Rank.BigJoker && y.Rank == Rank.BigJoker) return 0;
            if (x.Rank == Rank.BigJoker) return 1;
            if (y.Rank == Rank.BigJoker) return -1;
            if (x.Rank == Rank.SmallJoker && y.Rank == Rank.SmallJoker) return 0;
            if (x.Rank == Rank.SmallJoker) return 1;
            if (y.Rank == Rank.SmallJoker) return -1;

            bool xIsLevelCard = x.Rank == _config.LevelRank;
            bool yIsLevelCard = y.Rank == _config.LevelRank;

            // 主级牌 > 副级牌
            if (xIsLevelCard && yIsLevelCard)
            {
                bool xIsMainSuit = _config.TrumpSuit.HasValue && x.Suit == _config.TrumpSuit.Value;
                bool yIsMainSuit = _config.TrumpSuit.HasValue && y.Suit == _config.TrumpSuit.Value;

                if (xIsMainSuit && !yIsMainSuit) return 1;
                if (!xIsMainSuit && yIsMainSuit) return -1;
                return 0; // 副级牌之间无大小
            }

            // 级牌 > 主花色普通牌
            if (xIsLevelCard) return 1;
            if (yIsLevelCard) return -1;

            // 主花色普通牌按点数比较
            return x.Rank.CompareTo(y.Rank);
        }

        private int CompareSuit(Card x, Card y)
        {
            // 不同花色无法比较
            if (x.Suit != y.Suit) return 0;

            // 同花色按点数比较（级牌已被抽离为主牌）
            return x.Rank.CompareTo(y.Rank);
        }
    }
}
