using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests
{
    public class LoggingInfrastructureTests
    {
        [Fact]
        public void CoreLogger_AssignsSeqPerRound()
        {
            var sink = new InMemoryLogSink();
            var logger = new CoreLogger(sink);

            logger.Log(new LogEntry
            {
                Category = LogCategories.Audit,
                Level = LogLevels.Info,
                Event = "round.start",
                SessionId = "sess_1",
                GameId = "game_1",
                RoundId = "round_a"
            });

            logger.Log(new LogEntry
            {
                Category = LogCategories.Audit,
                Level = LogLevels.Info,
                Event = "round.finish",
                SessionId = "sess_1",
                GameId = "game_1",
                RoundId = "round_a"
            });

            logger.Log(new LogEntry
            {
                Category = LogCategories.Audit,
                Level = LogLevels.Info,
                Event = "round.start",
                SessionId = "sess_1",
                GameId = "game_1",
                RoundId = "round_b"
            });

            var entries = sink.Entries.ToList();
            Assert.Equal(3, entries.Count);
            Assert.Equal(1, entries[0].Seq);
            Assert.Equal(2, entries[1].Seq);
            Assert.Equal(1, entries[2].Seq);
        }

        [Fact]
        public void BidTrumpEx_ReturnsReasonCode_WhenPhaseInvalid()
        {
            var game = new Game(1);
            var result = game.BidTrumpEx(0, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.PhaseInvalid, result.ReasonCode);
        }

        [Fact]
        public void BuryBottomEx_ReturnsReasonCode_WhenCountIsNotEight()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var result = game.BuryBottomEx(game.State.PlayerHands[0].Take(7).ToList());

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.BuryNot8Cards, result.ReasonCode);
        }

        private static void DealToEnd(Game game)
        {
            while (!game.IsDealingComplete)
            {
                var step = game.DealNextCardEx();
                Assert.True(step.Success);
            }
        }
    }
}
