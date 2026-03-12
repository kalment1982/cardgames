using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 甩牌验证器
    /// </summary>
    public class ThrowValidator
    {
        private readonly GameConfig _config;

        public ThrowValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 判断甩牌是否成功
        /// </summary>
        public bool IsThrowSuccessful(List<Card> throwCards, List<List<Card>> followPlays)
        {
            if (throwCards == null || throwCards.Count == 0)
                return false;

            var throwSuit = GetSuitOrCategory(throwCards[0]);

            // 检查所有跟牌
            foreach (var follow in followPlays)
            {
                if (CanBeatThrow(follow, throwCards, throwSuit))
                    return false;
            }

            return true;
        }

        /// <summary>
        /// 检查跟牌是否能管上甩牌
        /// </summary>
        private bool CanBeatThrow(List<Card> follow, List<Card> throwCards, string throwSuit)
        {
            var followSuit = GetSuitOrCategory(follow[0]);

            // 不是同花色，无法管上
            if (followSuit != throwSuit)
                return false;

            // 比较最大牌型结构
            return HasBetterPattern(follow, throwCards);
        }

        /// <summary>
        /// 检查是否有更好的牌型结构
        /// </summary>
        private bool HasBetterPattern(List<Card> follow, List<Card> throwCards)
        {
            var throwPattern = AnalyzePattern(throwCards);
            var followPattern = AnalyzePattern(follow);

            // 只要跟牌方有对应或更高的牌型结构，就算管上
            if (followPattern.HasTractor && throwPattern.HasTractor)
                return true;
            if (followPattern.PairCount > 0 && throwPattern.PairCount > 0)
                return true;

            return false;
        }

        private PatternAnalysis AnalyzePattern(List<Card> cards)
        {
            var analysis = new PatternAnalysis();
            var pattern = new CardPattern(cards, _config);

            if (pattern.Type == PatternType.Tractor)
            {
                analysis.HasTractor = true;
            }

            // 统计对子数量
            var sorted = cards.OrderBy(c => c.Rank).ThenBy(c => c.Suit).ToList();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].Equals(sorted[i + 1]))
                {
                    analysis.PairCount++;
                    i++; // 跳过已配对的牌
                }
            }

            return analysis;
        }

        private string GetSuitOrCategory(Card card)
        {
            if (_config.IsTrump(card))
                return "Trump";
            return card.Suit.ToString();
        }

        /// <summary>
        /// 获取甩牌失败后的最小单牌
        /// </summary>
        public Card GetSmallestCard(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return null;

            var comparer = new CardComparer(_config);
            return cards.OrderBy(c => c, comparer).First();
        }

        private class PatternAnalysis
        {
            public bool HasTractor { get; set; }
            public int PairCount { get; set; }
        }
    }
}
