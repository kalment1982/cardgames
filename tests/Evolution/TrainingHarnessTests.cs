using System;
using System.IO;
using TractorGame.Core.AI.Evolution;
using Xunit;
using Xunit.Abstractions;

namespace TractorGame.Tests.Evolution
{
    [Collection("EvolutionSerial")]
    public class TrainingHarnessTests
    {
        private readonly ITestOutputHelper _output;

        public TrainingHarnessTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void TrainOneGeneration_Harness()
        {
            var config = new EvolutionConfig
            {
                CandidateCountOverride = ReadInt("EVOLUTION_CANDIDATE_COUNT", 12),
                Layer1TopK = ReadInt("EVOLUTION_LAYER1_TOPK", 4),
                Layer2TopK = ReadInt("EVOLUTION_LAYER2_TOPK", 2),
                Layer1GamesPerCandidate = ReadInt("EVOLUTION_LAYER1_GAMES", 20),
                Layer2GamesPerCandidate = ReadInt("EVOLUTION_LAYER2_GAMES", 40),
                Layer3GamesPerSeed = ReadInt("EVOLUTION_LAYER3_GAMES", 50),
                Layer3Seeds = ReadInt("EVOLUTION_LAYER3_SEEDS", 4),
                MaxTurnsPerGame = ReadInt("EVOLUTION_MAX_TURNS", 260),
                MaxParallelism = ReadInt("EVOLUTION_PARALLELISM", 4),
                BootstrapIterations = ReadInt("EVOLUTION_BOOTSTRAP_ITERS", 3000),
                Layer1Selection = Layer1SelectionMode.TopK,
                ForceDefenderBoostCandidate = ReadBool("EVOLUTION_FORCE_DEFENDER_BOOST", false),
                ForceDefenderBoostGeneration = ReadInt("EVOLUTION_FORCE_DEFENDER_BOOST_GENERATION", 24)
            };

            var runner = new EvolutionRunner(seed: DateTime.UtcNow.Millisecond + 20260314);
            var result = runner.RunOneGeneration(config);

            _output.WriteLine($"generation={result.Generation}");
            _output.WriteLine($"promoted={result.Promoted}");
            _output.WriteLine($"reason={result.PromotionReason}");
            _output.WriteLine($"report={result.ReportPath}");
            _output.WriteLine($"best={result.BestCandidate?.Candidate.CandidateId}");
            _output.WriteLine($"winrate={result.BestCandidate?.WinRate:P2}");
            _output.WriteLine($"ci=[{result.BestCandidate?.WinRateCiLow:P2},{result.BestCandidate?.WinRateCiHigh:P2}]");

            Assert.True(!string.IsNullOrWhiteSpace(result.ReportPath) && File.Exists(result.ReportPath));
        }

        private static int ReadInt(string key, int fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            return int.TryParse(value, out var parsed) && parsed > 0 ? parsed : fallback;
        }

        private static bool ReadBool(string key, bool fallback)
        {
            var value = Environment.GetEnvironmentVariable(key);
            if (string.IsNullOrWhiteSpace(value))
                return fallback;

            return value == "1"
                   || value.Equals("true", StringComparison.OrdinalIgnoreCase)
                   || value.Equals("yes", StringComparison.OrdinalIgnoreCase);
        }
    }
}
