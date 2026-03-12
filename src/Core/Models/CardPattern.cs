using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.Models
{
    /// <summary>
    /// 牌型识别
    /// </summary>
    public class CardPattern
    {
        public PatternType Type { get; }
        public List<Card> Cards { get; }
        public GameConfig Config { get; }

        public CardPattern(List<Card> cards, GameConfig config)
        {
            Cards = cards;
            Config = config;
            Type = IdentifyPattern();
        }

        private PatternType IdentifyPattern()
        {
            if (Cards.Count == 1) return PatternType.Single;
            if (Cards.Count == 2 && IsPair(Cards)) return PatternType.Pair;
            if (IsTractor(Cards)) return PatternType.Tractor;
            return PatternType.Mixed;
        }

        /// <summary>
        /// 判断是否为对子
        /// </summary>
        public static bool IsPair(List<Card> cards)
        {
            if (cards.Count != 2) return false;
            return cards[0].Equals(cards[1]);
        }

        /// <summary>
        /// 判断是否为拖拉机
        /// </summary>
        public bool IsTractor(List<Card> cards)
        {
            if (cards.Count < 4 || cards.Count % 2 != 0) return false;

            // 按配置排序
            var sorted = cards.OrderByDescending(c => c, new CardComparer(Config)).ToList();

            // 检查是否都是对子
            var pairs = new List<List<Card>>();
            for (int i = 0; i < sorted.Count; i += 2)
            {
                if (i + 1 >= sorted.Count || !sorted[i].Equals(sorted[i + 1]))
                    return false;
                pairs.Add(new List<Card> { sorted[i], sorted[i + 1] });
            }

            // 检查对子是否连续
            return AreConsecutivePairs(pairs);
        }

        private bool AreConsecutivePairs(List<List<Card>> pairs)
        {
            if (pairs.Count < 2) return false;

            for (int i = 0; i < pairs.Count - 1; i++)
            {
                if (!AreAdjacentPairs(pairs[i][0], pairs[i + 1][0]))
                    return false;
            }
            return true;
        }

        private bool AreAdjacentPairs(Card card1, Card card2)
        {
            // 获取在主牌或副牌序列中的位置
            var comparer = new CardComparer(Config);
            int cmp = comparer.Compare(card1, card2);

            // 必须相邻（考虑断档拖：级牌在中间时可跳过）
            return IsAdjacent(card1, card2);
        }

        private bool IsAdjacent(Card card1, Card card2)
        {
            bool isTrump1 = Config.IsTrump(card1);
            bool isTrump2 = Config.IsTrump(card2);

            // 必须同类别（都是主牌或都是副牌）
            if (isTrump1 != isTrump2) return false;

            if (isTrump1)
                return IsAdjacentInTrump(card1, card2);
            else
                return IsAdjacentInSuit(card1, card2);
        }

        private bool IsAdjacentInTrump(Card card1, Card card2)
        {
            // 主牌序列中的相邻判断
            var trumpOrder = GetTrumpOrder();
            int idx1 = trumpOrder.IndexOf(card1.Rank);
            int idx2 = trumpOrder.IndexOf(card2.Rank);

            if (idx1 == -1 || idx2 == -1) return false;
            return System.Math.Abs(idx1 - idx2) == 1;
        }

        private bool IsAdjacentInSuit(Card card1, Card card2)
        {
            // 副牌必须同花色
            if (card1.Suit != card2.Suit) return false;

            int rank1 = (int)card1.Rank;
            int rank2 = (int)card2.Rank;
            int levelRank = (int)Config.LevelRank;

            // 点数相邻
            if (System.Math.Abs(rank1 - rank2) == 1) return true;

            // 断档拖：级牌在中间时，两侧点数视为相邻
            // 例如：级牌为5时，4和6相邻
            if (System.Math.Abs(rank1 - rank2) == 2)
            {
                int minRank = System.Math.Min(rank1, rank2);
                int maxRank = System.Math.Max(rank1, rank2);
                return levelRank == minRank + 1;
            }

            return false;
        }

        private List<Rank> GetTrumpOrder()
        {
            // 主牌序列：大王 > 小王 > 主级牌 > 副级牌 > 主花色A-2
            var order = new List<Rank> { Rank.BigJoker, Rank.SmallJoker };

            // 级牌
            order.Add(Config.LevelRank);

            // 主花色普通牌（从大到小）
            var ranks = new[] { Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten,
                               Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five,
                               Rank.Four, Rank.Three, Rank.Two };

            foreach (var rank in ranks)
            {
                if (rank != Config.LevelRank)
                    order.Add(rank);
            }

            return order;
        }
    }
}
