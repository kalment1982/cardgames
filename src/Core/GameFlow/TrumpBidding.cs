using System;
using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 亮主阶段
    /// </summary>
    public class TrumpBidding
    {
        private Suit? _trumpSuit;
        private int _trumpPlayer = -1;
        private int _trumpLevel = 0; // 0=单张, 1=对子, 2=自保

        public Suit? TrumpSuit => _trumpSuit;
        public int TrumpPlayer => _trumpPlayer;

        /// <summary>
        /// 尝试亮主
        /// </summary>
        public bool TryBid(int playerIndex, Rank levelRank, List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return false;

            // 检查是否为级牌
            bool allLevelCards = true;
            Suit? suit = null;
            foreach (var card in cards)
            {
                if (card.Rank != levelRank)
                {
                    allLevelCards = false;
                    break;
                }
                if (suit == null)
                    suit = card.Suit;
                else if (suit != card.Suit)
                    return false; // 花色不一致
            }

            if (!allLevelCards)
                return false;

            int bidLevel = cards.Count == 1 ? 0 : cards.Count == 2 ? 1 : 2;

            // 检查是否可以反主
            if (_trumpSuit != null)
            {
                // 必须级别更高才能反
                if (bidLevel <= _trumpLevel)
                    return false;
            }

            _trumpSuit = suit;
            _trumpPlayer = playerIndex;
            _trumpLevel = bidLevel;
            return true;
        }

        /// <summary>
        /// 自保（庄家确认主花色）
        /// </summary>
        public void SelfProtect(Suit suit)
        {
            _trumpSuit = suit;
            _trumpLevel = 2;
        }
    }
}
