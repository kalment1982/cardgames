using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// AI玩家
    /// </summary>
    public class AIPlayer
    {
        private readonly GameConfig _config;
        private readonly Random _random;

        public AIPlayer(GameConfig config, int seed = 0)
        {
            _config = config;
            _random = seed > 0 ? new Random(seed) : new Random();
        }

        /// <summary>
        /// 首家出牌
        /// </summary>
        public List<Card> Lead(List<Card> hand)
        {
            if (hand == null || hand.Count == 0)
                return new List<Card>();

            // 简单策略：随机出一张牌
            int index = _random.Next(hand.Count);
            return new List<Card> { hand[index] };
        }

        /// <summary>
        /// 跟牌
        /// </summary>
        public List<Card> Follow(List<Card> hand, List<Card> leadCards)
        {
            var validator = new FollowValidator(_config);

            // 尝试跟同花色
            var sameSuit = hand.Where(c => GetSuitOrCategory(c) == GetSuitOrCategory(leadCards[0])).ToList();

            if (sameSuit.Count >= leadCards.Count)
            {
                return sameSuit.Take(leadCards.Count).ToList();
            }

            // 垫牌：随机选择
            return hand.Take(leadCards.Count).ToList();
        }

        /// <summary>
        /// 扣底
        /// </summary>
        public List<Card> BuryBottom(List<Card> hand)
        {
            if (hand.Count < 8)
                return new List<Card>();

            // 简单策略：扣最小的8张牌
            var comparer = new CardComparer(_config);
            return hand.OrderBy(c => c, comparer).Take(8).ToList();
        }

        private string GetSuitOrCategory(Card card)
        {
            if (_config.IsTrump(card))
                return "Trump";
            return card.Suit.ToString();
        }
    }
}
