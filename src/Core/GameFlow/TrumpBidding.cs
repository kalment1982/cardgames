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
        public int TrumpLevel => _trumpLevel;

        /// <summary>
        /// 尝试亮主
        /// </summary>
        public bool TryBid(int playerIndex, Rank levelRank, List<Card> cards)
        {
            return TryBidEx(playerIndex, levelRank, cards).Success;
        }

        /// <summary>
        /// 校验亮主（不修改状态）。
        /// </summary>
        public OperationResult CanBidEx(int playerIndex, Rank levelRank, List<Card> cards)
        {
            var attemptCards = cards ?? new List<Card>();
            var inspect = InspectBid(levelRank, attemptCards);
            if (!inspect.Success)
                return OperationResult.Fail(inspect.ReasonCode!);

            if (_trumpSuit != null)
            {
                // 已有亮主时，必须更高优先级才能反主。
                if (inspect.BidLevel <= _trumpLevel)
                    return OperationResult.Fail(ReasonCodes.BidPriorityTooLow);
            }

            return OperationResult.Ok;
        }

        /// <summary>
        /// 尝试亮主（携带 reason_code）。
        /// </summary>
        public OperationResult TryBidEx(int playerIndex, Rank levelRank, List<Card> cards)
        {
            var attemptCards = cards ?? new List<Card>();
            var validation = CanBidEx(playerIndex, levelRank, attemptCards);
            if (!validation.Success)
                return validation;

            var inspect = InspectBid(levelRank, attemptCards);
            if (!inspect.Success || inspect.Suit == null)
                return OperationResult.Fail(inspect.ReasonCode ?? ReasonCodes.UnknownError);

            _trumpSuit = inspect.Suit;
            _trumpPlayer = playerIndex;
            _trumpLevel = inspect.BidLevel;
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

        private static (bool Success, Suit? Suit, int BidLevel, string? ReasonCode) InspectBid(Rank levelRank, List<Card> cards)
        {
            if (cards.Count == 0)
                return (false, null, 0, ReasonCodes.BidNotLevelCard);

            Suit? suit = null;
            foreach (var card in cards)
            {
                if (card.Rank != levelRank)
                    return (false, null, 0, ReasonCodes.BidNotLevelCard);

                if (suit == null)
                    suit = card.Suit;
                else if (suit != card.Suit)
                    return (false, null, 0, ReasonCodes.BidNotLevelCard);
            }

            int bidLevel = cards.Count == 1 ? 0 : cards.Count == 2 ? 1 : 2;
            return (true, suit, bidLevel, null);
        }
    }
}
