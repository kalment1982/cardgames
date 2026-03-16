using System.Collections.Generic;
using System.IO;
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

        [Fact]
        public void BidTrumpEx_LogsBidDecisionDetail_WhenProvided()
        {
            var sink = new InMemoryLogSink();
            var logger = new CoreLogger(sink);
            var game = new Game(1, logger: logger);
            game.StartGame();

            var detail = new Dictionary<string, object?>
            {
                ["bid_reason"] = "C2",
                ["bid_reasons"] = new[] { "C2", "C3" },
                ["round_luck_p"] = 0.1675,
                ["bid_stage"] = "Early",
                ["bid_used_luck"] = false
            };

            var result = game.BidTrumpEx(0, new List<Card> { new Card(Suit.Spade, Rank.Two) }, detail);

            Assert.True(result.Success);
            var acceptLog = sink.Entries.Last(entry => entry.Event == "trump.bid.accept");
            Assert.Equal("C2", acceptLog.Payload["bid_reason"]);
            Assert.Equal(0.1675, acceptLog.Payload["round_luck_p"]);
            Assert.Equal("Early", acceptLog.Payload["bid_stage"]);
            Assert.Equal(false, acceptLog.Payload["bid_used_luck"]);
        }

        [Fact]
        public void AIDecisionBundleLogSink_WritesPrettyJsonFile()
        {
            var tempRoot = Path.Combine(Path.GetTempPath(), "tractor-ai-bundle-test", System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempRoot);

            try
            {
                var logger = new CoreLogger(new AIDecisionBundleLogSink(tempRoot));
                logger.Log(new LogEntry
                {
                    Category = LogCategories.Diag,
                    Level = LogLevels.Info,
                    Event = "ai.bundle",
                    RoundId = "round_test",
                    TrickId = "trick_0003",
                    CorrelationId = "follow_p2_trick_0003_turn_0002",
                    Payload = new Dictionary<string, object?>
                    {
                        ["decision_trace_id"] = "follow_p2_trick_0003_turn_0002",
                        ["bundle_version"] = "1.0",
                        ["bundle"] = new Dictionary<string, object?>
                        {
                            ["meta"] = new Dictionary<string, object?>
                            {
                                ["phase"] = "Follow"
                            }
                        }
                    }
                });

                var expectedPath = Path.Combine(
                    tempRoot,
                    System.DateTime.UtcNow.ToString("yyyy-MM-dd"),
                    "round_test",
                    "trick_0003",
                    "follow_p2_trick_0003_turn_0002.json");

                Assert.True(File.Exists(expectedPath));
                var json = File.ReadAllText(expectedPath);
                Assert.Contains("\"decision_trace_id\": \"follow_p2_trick_0003_turn_0002\"", json);
                Assert.Contains("\"bundle_version\": \"1.0\"", json);
            }
            finally
            {
                if (Directory.Exists(tempRoot))
                    Directory.Delete(tempRoot, recursive: true);
            }
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
