using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 一墩出牌记录
    /// </summary>
    public class TrickPlay
    {
        public int PlayerIndex { get; set; }
        public List<Card> Cards { get; set; }

        public TrickPlay(int playerIndex, List<Card> cards)
        {
            PlayerIndex = playerIndex;
            Cards = cards;
        }
    }

    /// <summary>
    /// 胜负判定器
    /// </summary>
    public class TrickJudge
    {
        private readonly GameConfig _config;

        public TrickJudge(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 判定一墩的获胜者
        /// </summary>
        public int DetermineWinner(List<TrickPlay> plays)
        {
            if (plays == null || plays.Count == 0)
                return -1;

            var leadPlay = plays[0];
            var leadCategory = _config.GetCardCategory(leadPlay.Cards[0]);
            var leadSuit = leadPlay.Cards[0].Suit;

            int winnerIndex = 0;
            var winnerPlay = leadPlay;

            for (int i = 1; i < plays.Count; i++)
            {
                var currentPlay = plays[i];

                // 比较当前出牌和当前获胜者
                if (IsStronger(currentPlay, winnerPlay, leadSuit, leadCategory))
                {
                    winnerIndex = i;
                    winnerPlay = currentPlay;
                }
            }

            return plays[winnerIndex].PlayerIndex;
        }

        /// <summary>
        /// 判断 play1 是否强于 play2
        /// </summary>
        private bool IsStronger(TrickPlay play1, TrickPlay play2, Suit leadSuit, CardCategory leadCategory)
        {
            var cat1 = _config.GetCardCategory(play1.Cards[0]);
            var cat2 = _config.GetCardCategory(play2.Cards[0]);

            // 主牌压副牌
            if (cat1 == CardCategory.Trump && cat2 == CardCategory.Suit)
                return true;
            if (cat1 == CardCategory.Suit && cat2 == CardCategory.Trump)
                return false;

            // 都是主牌，比较主牌大小
            if (cat1 == CardCategory.Trump && cat2 == CardCategory.Trump)
                return CompareTrumpCards(play1.Cards, play2.Cards);

            // 都是副牌
            var suit1 = play1.Cards[0].Suit;
            var suit2 = play2.Cards[0].Suit;

            // 首引花色压其他花色（垫牌）
            bool isLeadSuit1 = (suit1 == leadSuit && cat1 == leadCategory);
            bool isLeadSuit2 = (suit2 == leadSuit && cat2 == leadCategory);

            if (isLeadSuit1 && !isLeadSuit2)
                return true;
            if (!isLeadSuit1 && isLeadSuit2)
                return false;

            // 都是首引花色，比较大小
            if (isLeadSuit1 && isLeadSuit2)
                return CompareSuitCards(play1.Cards, play2.Cards);

            // 都是垫牌，无法比较
            return false;
        }

        /// <summary>
        /// 比较主牌大小
        /// </summary>
        private bool CompareTrumpCards(List<Card> cards1, List<Card> cards2)
        {
            var pattern1 = new CardPattern(cards1, _config);
            var pattern2 = new CardPattern(cards2, _config);

            // 比较牌型优先级：拖拉机 > 对子 > 单张
            if (pattern1.Type != pattern2.Type)
            {
                return GetPatternPriority(pattern1.Type) > GetPatternPriority(pattern2.Type);
            }

            // 同牌型，比较最大牌
            var comparer = new CardComparer(_config);
            var sorted1 = cards1.OrderByDescending(c => c, comparer).ToList();
            var sorted2 = cards2.OrderByDescending(c => c, comparer).ToList();

            return comparer.Compare(sorted1[0], sorted2[0]) > 0;
        }

        /// <summary>
        /// 比较副牌大小
        /// </summary>
        private bool CompareSuitCards(List<Card> cards1, List<Card> cards2)
        {
            var pattern1 = new CardPattern(cards1, _config);
            var pattern2 = new CardPattern(cards2, _config);

            // 比较牌型优先级
            if (pattern1.Type != pattern2.Type)
            {
                return GetPatternPriority(pattern1.Type) > GetPatternPriority(pattern2.Type);
            }

            // 同牌型，比较最大牌
            var max1 = cards1.Max(c => (int)c.Rank);
            var max2 = cards2.Max(c => (int)c.Rank);

            return max1 > max2;
        }

        private int GetPatternPriority(PatternType type)
        {
            return type switch
            {
                PatternType.Tractor => 3,
                PatternType.Pair => 2,
                PatternType.Single => 1,
                PatternType.Mixed => 0,
                _ => 0
            };
        }
    }
}
