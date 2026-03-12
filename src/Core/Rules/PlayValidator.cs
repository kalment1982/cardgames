using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 出牌合法性校验器
    /// </summary>
    public class PlayValidator
    {
        private readonly GameConfig _config;

        public PlayValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 检查出牌是否合法（首家出牌）
        /// </summary>
        public bool IsValidPlay(List<Card> hand, List<Card> cardsToPlay)
        {
            // 基础检查
            if (cardsToPlay == null || cardsToPlay.Count == 0)
                return false;

            // 检查是否都在手牌中
            if (!AllCardsInHand(hand, cardsToPlay))
                return false;

            // 检查牌型是否有效
            return ValidatePattern(cardsToPlay);
        }

        /// <summary>
        /// 检查所有牌是否都在手牌中
        /// </summary>
        private bool AllCardsInHand(List<Card> hand, List<Card> cardsToPlay)
        {
            var handCopy = new List<Card>(hand);

            foreach (var card in cardsToPlay)
            {
                var found = handCopy.FirstOrDefault(c => c.Equals(card));
                if (found == null) return false;
                handCopy.Remove(found);
            }

            return true;
        }

        /// <summary>
        /// 验证牌型是否有效
        /// </summary>
        public bool ValidatePattern(List<Card> cards)
        {
            if (cards.Count == 1) return true; // 单张总是合法

            // 检查是否为对子
            if (cards.Count == 2)
                return CardPattern.IsPair(cards);

            // 检查是否为拖拉机
            var pattern = new CardPattern(cards, _config);
            if (pattern.IsTractor(cards))
                return true;

            // 混合牌型（甩牌）- 需要进一步验证
            return ValidateMixedPattern(cards);
        }

        /// <summary>
        /// 验证混合牌型（甩牌）
        /// </summary>
        private bool ValidateMixedPattern(List<Card> cards)
        {
            // 混合牌型必须是同花色
            if (!IsSameSuitOrTrump(cards))
                return false;

            // 可以包含：拖拉机 + 对子 + 单张的组合
            // 这里简化处理：只要同花色就认为是合法的甩牌尝试
            return true;
        }

        /// <summary>
        /// 检查是否为同花色或同为主牌
        /// </summary>
        private bool IsSameSuitOrTrump(List<Card> cards)
        {
            bool allTrump = cards.All(c => _config.IsTrump(c));
            if (allTrump) return true;

            bool allSuit = cards.All(c => !_config.IsTrump(c));
            if (!allSuit) return false;

            // 都是副牌，检查是否同花色
            var firstSuit = cards[0].Suit;
            return cards.All(c => c.Suit == firstSuit);
        }
    }
}
