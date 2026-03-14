using System.IO;
using TractorGame.Core.AI.Evolution;
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests.Evolution
{
    [Collection("EvolutionSerial")]
    public class IntegrationTests
    {
        private readonly ITestOutputHelper _output;

        public IntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void EvolutionRunner_CompletesOneGeneration()
        {
            var config = new EvolutionConfig
            {
                CandidateCountOverride = 8,
                Layer1TopK = 4,
                Layer2TopK = 2,
                Layer1GamesPerCandidate = 4,
                Layer2GamesPerCandidate = 8,
                Layer3GamesPerSeed = 12,
                Layer3Seeds = 2,
                MaxTurnsPerGame = 240,
                MaxParallelism = 4,
                BootstrapIterations = 1000,
                Layer1Selection = Layer1SelectionMode.TopK
            };

            var runner = new EvolutionRunner(seed: 20260314);
            var result = runner.RunOneGeneration(config);

            _output.WriteLine($"generation={result.Generation}");
            _output.WriteLine($"promoted={result.Promoted}");
            _output.WriteLine($"reason={result.PromotionReason}");
            _output.WriteLine($"best_candidate={result.BestCandidate?.Candidate.CandidateId}");
            _output.WriteLine($"best_winrate={result.BestCandidate?.WinRate:P2}");
            _output.WriteLine($"best_ci=[{result.BestCandidate?.WinRateCiLow:P2},{result.BestCandidate?.WinRateCiHigh:P2}]");
            _output.WriteLine($"report={result.ReportPath}");

            Assert.True(result.CandidateCount > 0);
            Assert.NotNull(result.BestCandidate);
            Assert.False(string.IsNullOrWhiteSpace(result.ReportPath));
            Assert.True(File.Exists(result.ReportPath!));
        }
    }
}
