using Xunit;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;

namespace TractorGame.Tests
{
    public class LevelManagerTests
    {
        // ── 庄家方获胜 ────────────────────────────────────────────────────────

        [Fact]
        public void DetermineLevelChange_Score0_DealerUp3()
        {
            var result = new LevelManager().DetermineLevelChange(0, Rank.Two);
            Assert.Equal("庄家", result.Winner);
            Assert.Equal("庄家", result.NextDealer);
            Assert.Equal(3, result.LevelChange);
            Assert.Equal(Rank.Five, result.NextLevel); // 2+3=5
        }

        [Fact]
        public void DetermineLevelChange_Score20_DealerUp2()
        {
            var result = new LevelManager().DetermineLevelChange(20, Rank.Two);
            Assert.Equal("庄家", result.Winner);
            Assert.Equal("庄家", result.NextDealer);
            Assert.Equal(2, result.LevelChange);
            Assert.Equal(Rank.Four, result.NextLevel); // 2+2=4
        }

        [Fact]
        public void DetermineLevelChange_Score35_DealerUp2()
        {
            // 边界：35分仍是升2级
            var result = new LevelManager().DetermineLevelChange(35, Rank.Two);
            Assert.Equal(2, result.LevelChange);
            Assert.Equal("庄家", result.NextDealer);
        }

        [Fact]
        public void DetermineLevelChange_Score40_DealerUp1()
        {
            // 边界：40分升1级
            var result = new LevelManager().DetermineLevelChange(40, Rank.Two);
            Assert.Equal("庄家", result.Winner);
            Assert.Equal("庄家", result.NextDealer);
            Assert.Equal(1, result.LevelChange);
            Assert.Equal(Rank.Three, result.NextLevel); // 2+1=3
        }

        [Fact]
        public void DetermineLevelChange_Score65_DealerUp1()
        {
            var result = new LevelManager().DetermineLevelChange(65, Rank.Two);
            Assert.Equal("庄家", result.Winner);
            Assert.Equal(1, result.LevelChange);
        }

        // ── 闲家方获胜 ────────────────────────────────────────────────────────

        [Fact]
        public void DetermineLevelChange_Score80_DefenderUp0()
        {
            // 边界：80分，闲家上台但不升级
            var result = new LevelManager().DetermineLevelChange(80, Rank.Two);
            Assert.Equal("闲家", result.Winner);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(0, result.LevelChange);
            Assert.Equal(Rank.Two, result.NextLevel); // 不升级，仍打2
        }

        [Fact]
        public void DetermineLevelChange_Score100_DefenderUp0()
        {
            var result = new LevelManager().DetermineLevelChange(100, Rank.Three);
            Assert.Equal("闲家", result.Winner);
            Assert.Equal(0, result.LevelChange);
            Assert.Equal(Rank.Three, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_Score120_DefenderUp1()
        {
            // 边界：120分，闲家升1级
            var result = new LevelManager().DetermineLevelChange(120, Rank.Two);
            Assert.Equal("闲家", result.Winner);
            Assert.Equal("闲家", result.NextDealer);
            Assert.Equal(1, result.LevelChange);
            Assert.Equal(Rank.Three, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_Score155_DefenderUp1()
        {
            // 边界：155分仍是升1级
            var result = new LevelManager().DetermineLevelChange(155, Rank.Two);
            Assert.Equal(1, result.LevelChange);
        }

        [Fact]
        public void DetermineLevelChange_Score160_DefenderUp2()
        {
            // 边界：160分升2级
            var result = new LevelManager().DetermineLevelChange(160, Rank.Two);
            Assert.Equal("闲家", result.Winner);
            Assert.Equal(2, result.LevelChange);
            Assert.Equal(Rank.Four, result.NextLevel);
        }

        [Fact]
        public void DetermineLevelChange_Score200_DefenderUp3()
        {
            var result = new LevelManager().DetermineLevelChange(200, Rank.Two);
            Assert.Equal("闲家", result.Winner);
            Assert.Equal(3, result.LevelChange);
            Assert.Equal(Rank.Five, result.NextLevel);
        }
    }
}
