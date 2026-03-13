using System;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

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
            return TryBidEx(playerIndex, levelRank, cards).Success;
        }

        /// <summary>
        /// 尝试亮主（携带 reason_code）。
        /// </summary>
        public OperationResult TryBidEx(int playerIndex, Rank levelRank, List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return OperationResult.Fail(ReasonCodes.BidNotLevelCard);

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
                    return OperationResult.Fail(ReasonCodes.BidNotLevelCard);
            }

            if (!allLevelCards)
                return OperationResult.Fail(ReasonCodes.BidNotLevelCard);

            int bidLevel = cards.Count == 1 ? 0 : cards.Count == 2 ? 1 : 2;

            // 检查是否可以反主
            if (_trumpSuit != null)
            {
                // 必须级别更高才能反
                if (bidLevel <= _trumpLevel)
                    return OperationResult.Fail(ReasonCodes.BidPriorityTooLow);
            }

            _trumpSuit = suit;
            _trumpPlayer = playerIndex;
            _trumpLevel = bidLevel;
            return OperationResult.Ok;
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
