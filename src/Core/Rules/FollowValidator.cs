using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 跟牌约束检查器
    /// </summary>
    public class FollowValidator
    {
        private readonly GameConfig _config;

        public FollowValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 检查跟牌是否合法
        /// </summary>
        public bool IsValidFollow(List<Card> hand, List<Card> leadCards, List<Card> followCards)
        {
            // 基础检查
            if (followCards == null || followCards.Count == 0)
                return false;

            // 必须跟相同张数
            if (followCards.Count != leadCards.Count)
                return false;

            // 检查是否都在手牌中
            if (!AllCardsInHand(hand, followCards))
                return false;

            // 获取首引花色/类别
            var leadCategory = GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;

            // 检查是否有首引花色
            bool hasLeadSuit = HasSuit(hand, leadSuit, leadCategory);

            if (hasLeadSuit)
            {
                // 有首引花色，必须跟该花色
                if (!AllMatchSuit(followCards, leadSuit, leadCategory))
                    return false;

                // 检查牌型约束
                return ValidatePatternConstraint(hand, leadCards, followCards, leadSuit, leadCategory);
            }
            else
            {
                // 无首引花色，可以垫牌或毙牌
                return true;
            }
        }

        private bool AllCardsInHand(List<Card> hand, List<Card> cards)
        {
            var handCopy = new List<Card>(hand);
            foreach (var card in cards)
            {
                var found = handCopy.FirstOrDefault(c => c.Equals(card));
                if (found == null) return false;
                handCopy.Remove(found);
            }
            return true;
        }

        private CardCategory GetCardCategory(Card card)
        {
            return _config.GetCardCategory(card);
        }

        private bool HasSuit(List<Card> hand, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
            {
                // 检查是否有主牌
                return hand.Any(c => _config.IsTrump(c));
            }
            else
            {
                // 检查是否有该花色的副牌
                return hand.Any(c => !_config.IsTrump(c) && c.Suit == suit);
            }
        }

        private bool AllMatchSuit(List<Card> cards, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
            {
                return cards.All(c => _config.IsTrump(c));
            }
            else
            {
                return cards.All(c => !_config.IsTrump(c) && c.Suit == suit);
            }
        }

        private bool ValidatePatternConstraint(List<Card> hand, List<Card> leadCards,
            List<Card> followCards, Suit suit, CardCategory category)
        {
            // 识别首引牌型
            var leadPattern = new CardPattern(leadCards, _config);

            // 单张无约束
            if (leadPattern.Type == PatternType.Single)
                return true;

            // 对子：如果有对子必须出对子
            if (leadPattern.Type == PatternType.Pair)
            {
                var availablePairs = GetAvailablePairs(hand, suit, category);
                if (availablePairs.Count > 0)
                {
                    // 必须出对子
                    return CardPattern.IsPair(followCards);
                }
                return true;
            }

            // 拖拉机：如果有拖拉机必须出拖拉机
            if (leadPattern.Type == PatternType.Tractor)
            {
                var availableTractors = GetAvailableTractors(hand, suit, category, leadCards.Count);
                if (availableTractors.Count > 0)
                {
                    // 必须出拖拉机
                    var followPattern = new CardPattern(followCards, _config);
                    return followPattern.IsTractor(followCards);
                }
                return true;
            }

            return true;
        }

        private List<List<Card>> GetAvailablePairs(List<Card> hand, Suit suit, CardCategory category)
        {
            var pairs = new List<List<Card>>();
            var suitCards = hand.Where(c => MatchesSuit(c, suit, category)).ToList();

            for (int i = 0; i < suitCards.Count - 1; i++)
            {
                for (int j = i + 1; j < suitCards.Count; j++)
                {
                    if (suitCards[i].Equals(suitCards[j]))
                    {
                        pairs.Add(new List<Card> { suitCards[i], suitCards[j] });
                    }
                }
            }

            return pairs;
        }

        private List<List<Card>> GetAvailableTractors(List<Card> hand, Suit suit,
            CardCategory category, int minLength)
        {
            var tractors = new List<List<Card>>();
            var suitCards = hand.Where(c => MatchesSuit(c, suit, category)).ToList();

            // 简化实现：检查是否存在至少minLength张牌能组成拖拉机
            // 这里只做基础检查
            if (suitCards.Count >= minLength)
            {
                var pattern = new CardPattern(suitCards, _config);
                if (pattern.IsTractor(suitCards))
                {
                    tractors.Add(suitCards);
                }
            }

            return tractors;
        }

        private bool MatchesSuit(Card card, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
                return _config.IsTrump(card);
            else
                return !_config.IsTrump(card) && card.Suit == suit;
        }
    }
}
