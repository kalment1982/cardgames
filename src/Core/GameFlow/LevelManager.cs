using TractorGame.Core.Models;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 升级管理器
    /// </summary>
    public class LevelManager
    {
        /// <summary>
        /// 根据得分判定升级
        /// </summary>
        public LevelResult DetermineLevelChange(int defenderScore, Rank currentLevel)
        {
            var result = new LevelResult
            {
                Winner = defenderScore >= 80 ? "闲家" : "庄家",
                DefenderScore = defenderScore
            };

            // 规则文档 5.2：按闲家方得分分段
            // 庄家方获胜（闲家 < 80）
            //   0分        → 庄家升3级
            //   5-35分     → 庄家升2级
            //   40-75分    → 庄家升1级
            // 闲家方获胜（闲家 ≥ 80）
            //   80-115分   → 闲家上台，不升级（升0级）
            //   120-155分  → 闲家上台升1级
            //   160-195分  → 闲家上台升2级
            //   200分+     → 闲家上台升3级
            if (defenderScore < 80)
            {
                result.NextDealer = "庄家";
                if (defenderScore == 0)
                    result.LevelChange = 3;
                else if (defenderScore <= 35)
                    result.LevelChange = 2;
                else
                    result.LevelChange = 1; // 40-75分
            }
            else
            {
                result.NextDealer = "闲家";
                if (defenderScore < 120)
                    result.LevelChange = 0; // 80-115：上台不升级
                else if (defenderScore < 160)
                    result.LevelChange = 1; // 120-155：升1级
                else if (defenderScore < 200)
                    result.LevelChange = 2; // 160-195：升2级
                else
                    result.LevelChange = 3; // 200+：升3级
            }

            result.NextLevel = CalculateNextLevel(currentLevel, result.LevelChange);
            return result;
        }

        private Rank CalculateNextLevel(Rank current, int change)
        {
            int[] levels = { 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14 }; // 2-A
            int currentIndex = (int)current - 2;

            if (currentIndex < 0 || currentIndex >= levels.Length)
                return current;

            int nextIndex = currentIndex + change;
            if (nextIndex >= levels.Length)
                return Rank.Ace; // 已经到A

            return (Rank)(levels[nextIndex]);
        }
    }

    public class LevelResult
    {
        public string Winner { get; set; }
        public int DefenderScore { get; set; }
        public int LevelChange { get; set; }
        public Rank NextLevel { get; set; }
        public string NextDealer { get; set; }
    }
}
