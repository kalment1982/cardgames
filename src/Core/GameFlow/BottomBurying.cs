using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 扣底阶段
    /// </summary>
    public class BottomBurying
    {
        private readonly List<Card> _bottomCards;
        private List<Card> _buriedCards;

        public BottomBurying(List<Card> bottomCards)
        {
            _bottomCards = bottomCards ?? throw new ArgumentNullException(nameof(bottomCards));
            _buriedCards = new List<Card>();
        }

        public List<Card> BottomCards => new List<Card>(_bottomCards);
        public List<Card> BuriedCards => new List<Card>(_buriedCards);

        /// <summary>
        /// 扣底（庄家选择8张牌）
        /// </summary>
        public bool BuryCards(List<Card> hand, List<Card> cardsToBury)
        {
            return BuryCardsEx(hand, cardsToBury).Success;
        }

        /// <summary>
        /// 扣底（庄家选择8张牌），返回失败原因。
        /// </summary>
        public OperationResult BuryCardsEx(List<Card> hand, List<Card> cardsToBury)
        {
            if (cardsToBury == null || cardsToBury.Count != 8)
                return OperationResult.Fail(ReasonCodes.BuryNot8Cards);

            // 检查所有牌都在手牌中
            var handCopy = new List<Card>(hand);
            handCopy.AddRange(_bottomCards);

            foreach (var card in cardsToBury)
            {
                if (!handCopy.Contains(card))
                    return OperationResult.Fail(ReasonCodes.BuryCardNotFound);
                handCopy.Remove(card);
            }

            _buriedCards = new List<Card>(cardsToBury);
            return OperationResult.Ok;
        }
    }
}
