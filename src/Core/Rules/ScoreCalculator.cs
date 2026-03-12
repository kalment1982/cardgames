using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 抠底计分器
    /// </summary>
    public class ScoreCalculator
    {
        private readonly GameConfig _config;

        public ScoreCalculator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 计算抠底分数
        /// </summary>
        public int CalculateBottomScore(List<Card> bottomCards, List<Card> lastTrickCards)
        {
            // 底牌基础分
            int baseScore = bottomCards.Sum(c => c.Score);

            // 最后一墩牌型倍数
            int multiplier = CalculateMultiplier(lastTrickCards);

            return baseScore * multiplier;
        }

        /// <summary>
        /// 计算牌型倍数
        /// </summary>
        private int CalculateMultiplier(List<Card> cards)
        {
            var pattern = new CardPattern(cards, _config);

            if (pattern.Type == PatternType.Tractor)
            {
                // 拖拉机：2^n，n为对子数
                int pairCount = cards.Count / 2;
                return (int)System.Math.Pow(2, pairCount);
            }
            else if (pattern.Type == PatternType.Pair)
            {
                // 对子：×4
                return 4;
            }
            else
            {
                // 单张：×2
                return 2;
            }
        }
    }
}
