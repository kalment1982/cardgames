using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

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
        /// 规则：
        ///   1. 有首引花色 → 必须先跟尽所有首引花色，不足部分才能垫牌/毙牌
        ///   2. 首引是对子 → 有对子必须出对子（同花对子数量足够时）
        ///   3. 首引是拖拉机 → 有拖拉机必须出拖拉机（同花拖拉机存在时）
        ///   4. 无首引花色 → 可以任意垫牌或毙牌
        /// </summary>
        public bool IsValidFollow(List<Card> hand, List<Card> leadCards, List<Card> followCards)
        {
            return IsValidFollowEx(hand, leadCards, followCards).Success;
        }

        /// <summary>
        /// 检查跟牌是否合法，返回失败原因。
        /// </summary>
        public OperationResult IsValidFollowEx(List<Card> hand, List<Card> leadCards, List<Card> followCards)
        {
            if (followCards == null || followCards.Count == 0)
                return OperationResult.Fail(ReasonCodes.FollowCountMismatch);
            if (followCards.Count != leadCards.Count)
                return OperationResult.Fail(ReasonCodes.FollowCountMismatch);
            if (!AllCardsInHand(hand, followCards))
                return OperationResult.Fail(ReasonCodes.CardNotInHand);

            var leadCategory = GetCardCategory(leadCards[0]);
            var leadSuit     = leadCards[0].Suit;
            int needed       = leadCards.Count;

            // 手里有多少张首引花色
            var suitCardsInHand = hand.Where(c => MatchesSuit(c, leadSuit, leadCategory)).ToList();
            int available = suitCardsInHand.Count;

            if (available == 0)
            {
                // 无首引花色，任意出牌合法
                return OperationResult.Ok;
            }

            // 跟牌中属于首引花色的张数
            var followSuitCards = followCards.Where(c => MatchesSuit(c, leadSuit, leadCategory)).ToList();
            int mustFollow = System.Math.Min(available, needed);

            // 必须跟尽所有能跟的首引花色
            if (followSuitCards.Count < mustFollow)
                return OperationResult.Fail(ReasonCodes.FollowSuitRequired);

            // 如果首引花色数量足够填满，还需检查牌型约束
            if (available >= needed)
                return ValidatePatternConstraintEx(hand, leadCards, followCards, leadSuit, leadCategory);

            return OperationResult.Ok;
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

        private OperationResult ValidatePatternConstraintEx(List<Card> hand, List<Card> leadCards,
            List<Card> followCards, Suit suit, CardCategory category)
        {
            var leadPattern = new CardPattern(leadCards, _config);

            if (leadPattern.Type == PatternType.Single)
                return OperationResult.Ok;

            if (leadPattern.Type == PatternType.Pair)
            {
                // 有对子必须出对子
                if (GetAvailablePairs(hand, suit, category).Count > 0)
                    return CardPattern.IsPair(followCards)
                        ? OperationResult.Ok
                        : OperationResult.Fail(ReasonCodes.FollowPairRequired);
                return OperationResult.Ok;
            }

            if (leadPattern.Type == PatternType.Tractor)
            {
                int tractorLen = leadCards.Count;
                // 找手牌中同花色里是否存在长度 >= tractorLen 的拖拉机
                var suitCards = hand.Where(c => MatchesSuit(c, suit, category)).ToList();
                bool hasTractor = FindTractorInCards(suitCards, tractorLen);
                if (hasTractor)
                {
                    // 跟牌必须是拖拉机
                    var followSuitCards = followCards.Where(c => MatchesSuit(c, suit, category)).ToList();
                    if (followSuitCards.Count < tractorLen)
                        return OperationResult.Fail(ReasonCodes.FollowTractorRequired);
                    var followPattern = new CardPattern(followSuitCards, _config);
                    return followPattern.IsTractor(followSuitCards)
                        ? OperationResult.Ok
                        : OperationResult.Fail(ReasonCodes.FollowTractorRequired);
                }
                return OperationResult.Ok;
            }

            return OperationResult.Ok;
        }

        /// <summary>
        /// 在给定牌组中寻找长度 >= minLen 的拖拉机
        /// </summary>
        private bool FindTractorInCards(List<Card> cards, int minLen)
        {
            if (cards.Count < minLen) return false;
            // 枚举所有子集组合，找到能构成拖拉机且长度 >= minLen 的
            // 简化：按对子为单位检查连续对子
            var comparer = new CardComparer(_config);
            var sorted = cards.OrderBy(c => c, comparer).ToList();

            // 找出所有对子
            var pairs = new List<List<Card>>();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].Equals(sorted[i + 1]))
                {
                    pairs.Add(new List<Card> { sorted[i], sorted[i + 1] });
                    i++; // 跳过已配对
                }
            }

            if (pairs.Count * 2 < minLen) return false;

            // 检查是否有连续对子（拖拉机）
            for (int start = 0; start <= pairs.Count - minLen / 2; start++)
            {
                var candidate = pairs.Skip(start).Take(minLen / 2).SelectMany(p => p).ToList();
                var pattern = new CardPattern(candidate, _config);
                if (pattern.IsTractor(candidate)) return true;
            }
            return false;
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

        private bool MatchesSuit(Card card, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
                return _config.IsTrump(card);
            else
                return !_config.IsTrump(card) && card.Suit == suit;
        }
    }
}
