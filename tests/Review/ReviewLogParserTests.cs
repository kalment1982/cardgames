using System.Linq;
using System.IO;
using TractorGame.Core.Review;
using Xunit;

namespace TractorGame.Tests.Review
{
    public class ReviewLogParserTests
    {
        private static readonly string SampleAuditPath = TestPathHelper.ResolveFromRepoRoot(
            "artifacts",
            "ruleai_v30_vs_v21_eval",
            "20260320_103902",
            "raw",
            "2026-03-20",
            "v30-vs-v21-audit-2026-03-20-02.jsonl");

        [Fact]
        public void ParseFile_BuildsSessionSummaries()
        {
            var parser = new ReviewLogParser();

            var sessions = parser.ParseFile(SampleAuditPath, "audit", "V30 vs V21 Audit");

            Assert.Equal(100, sessions.Count);

            var first = sessions.Single(session => session.Summary.RoundId == "mixed_mixed100_g000_23000");
            Assert.Equal("audit", first.Summary.SourceTag);
            Assert.Equal("V30 vs V21 Audit", first.Summary.SourceLabel);
            Assert.Equal("Diamond", first.Summary.TrumpSuit);
            Assert.Equal("Two", first.Summary.LevelRank);
            Assert.Equal(0, first.Summary.DealerIndex);
            Assert.Equal(19, first.Tricks.Count);
            Assert.NotEmpty(first.Summary.AiLineSummary);
            Assert.Equal(4, first.Summary.PlayerAiLines.Count);
        }

        [Fact]
        public void ParseFile_PreservesHandsPlaysBottomAndDecisions()
        {
            var parser = new ReviewLogParser();

            var sessions = parser.ParseFile(SampleAuditPath, "audit", "V30 vs V21 Audit");
            var session = sessions.Single(item => item.Summary.RoundId == "mixed_mixed100_g000_23000");
            var trick = session.Tricks.Single(item => item.TrickNo == 10);

            Assert.Equal(8, session.BottomCards.Count);
            Assert.Equal(4, trick.HandsBefore.Count);
            Assert.Equal(4, trick.Plays.Count);
            Assert.Equal(4, trick.Decisions.Count);
            Assert.Equal("trick_0010", trick.TrickId);
            Assert.Equal(3, trick.WinnerIndex);

            var leadDecision = trick.Decisions.Single(item => item.TurnId == "turn_0037");
            Assert.Equal(0, leadDecision.PlayerIndex);
            Assert.Equal("Lead", leadDecision.Phase);
            Assert.Equal("rule_ai_v30_lead_overlay", leadDecision.Path);
            Assert.Equal("ForceTrump", leadDecision.PrimaryIntent);
            Assert.Equal("lead003.force_trump", leadDecision.SelectedCandidateId);
            Assert.Single(leadDecision.SelectedCards);
            Assert.NotNull(leadDecision.Bundle);
            Assert.NotNull(leadDecision.BundleV30);

            var followDecision = trick.Decisions.Single(item => item.TurnId == "turn_0038");
            Assert.Equal("rule_ai_v21_follow_policy2", followDecision.Path);
            Assert.Equal("TakeScore", followDecision.PrimaryIntent);
            Assert.NotNull(followDecision.Bundle);
        }

        [Fact]
        public void ParseFile_AcceptsBrowserStylePlayAndTrickPayloads()
        {
            var tempPath = Path.GetTempFileName();
            try
            {
                File.WriteAllLines(tempPath, new[]
                {
                    "{\"event\":\"game.start\",\"round_id\":\"round_browser\",\"game_id\":\"round_browser\",\"payload\":{\"dealer_index\":0,\"level_rank\":\"Two\",\"playerAiLines\":[{\"playerIndex\":0,\"aiLine\":\"Human\"},{\"playerIndex\":1,\"aiLine\":\"V30\"},{\"playerIndex\":2,\"aiLine\":\"V30\"},{\"playerIndex\":3,\"aiLine\":\"V30\"}]}}",
                    "{\"event\":\"turn.start\",\"round_id\":\"round_browser\",\"game_id\":\"round_browser\",\"trick_id\":\"trick_0001\",\"payload\":{\"trick_no\":1,\"is_lead\":true,\"lead_player\":0,\"dealer_index\":0,\"level_rank\":\"Two\",\"trump_suit\":\"Heart\"}}",
                    "{\"event\":\"play.accept\",\"round_id\":\"round_browser\",\"game_id\":\"round_browser\",\"trick_id\":\"trick_0001\",\"turn_id\":\"turn_0001\",\"payload\":{\"playerIndex\":0,\"is_lead\":true,\"leadPlayer\":0,\"handsBeforeTrick\":[{\"player_index\":0,\"hand_count\":25,\"cards\":[{\"text\":\"♠A\"}]},{\"player_index\":1,\"hand_count\":25,\"cards\":[{\"text\":\"♥K\"}]},{\"player_index\":2,\"hand_count\":25,\"cards\":[{\"text\":\"♣Q\"}]},{\"player_index\":3,\"hand_count\":25,\"cards\":[{\"text\":\"♦J\"}]}],\"cards\":[{\"text\":\"♠A\"}]}}",
                    "{\"event\":\"trick.finish\",\"round_id\":\"round_browser\",\"game_id\":\"round_browser\",\"trick_id\":\"trick_0001\",\"payload\":{\"type\":\"trick_end\",\"trickIndex\":1,\"winner\":1,\"trickScore\":15,\"defenderScoreBefore\":0,\"defenderScoreAfter\":15,\"plays\":[{\"playerIndex\":0,\"cards\":[{\"text\":\"♠A\"}]},{\"playerIndex\":1,\"cards\":[{\"text\":\"♥A\"}]},{\"playerIndex\":2,\"cards\":[{\"text\":\"♣Q\"}]},{\"playerIndex\":3,\"cards\":[{\"text\":\"♦J\"}]}]}}"
                });

                var parser = new ReviewLogParser();
                var sessions = parser.ParseFile(tempPath, "logs_raw", "logs/raw");
                var session = Assert.Single(sessions);
                var trick = Assert.Single(session.Tricks);

                Assert.Equal("round_browser", session.Summary.RoundId);
                Assert.Equal("Heart", session.Summary.TrumpSuit);
                Assert.Equal(1, trick.TrickNo);
                Assert.Equal(1, trick.WinnerIndex);
                Assert.Equal(15, trick.TrickScore);
                Assert.Equal(4, trick.HandsBefore.Count);
                Assert.Equal(4, trick.Plays.Count);
                Assert.Equal("♠A", trick.HandsBefore[0].Cards[0].Text);
                Assert.Equal("♥A", trick.Plays[1].Cards[0].Text);
                Assert.Equal("Human", session.Summary.PlayerAiLines.Single(item => item.PlayerIndex == 0).AiLine);
                Assert.Equal("V30", session.Summary.PlayerAiLines.Single(item => item.PlayerIndex == 1).AiLine);
            }
            finally
            {
                if (File.Exists(tempPath))
                    File.Delete(tempPath);
            }
        }
    }
}
