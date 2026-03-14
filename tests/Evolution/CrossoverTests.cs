using TractorGame.Core.AI;
using TractorGame.Core.AI.Evolution.PolicyFactory;
using Xunit;

namespace TractorGame.Tests.Evolution
{
    [Collection("EvolutionSerial")]
    public class CrossoverTests
    {
        [Fact]
        public void Crossover_GeneratesDeterministicHashShape()
        {
            var parentA = AIStrategyParameters.CreatePreset(AIDifficulty.Medium);
            var parentB = AIStrategyParameters.CreatePreset(AIDifficulty.Expert);
            var crossover = new CrossoverOperator(7);
            var repair = new RepairOperator();

            var child = repair.Repair(crossover.Crossover(parentA, parentB));
            var hash = new ParameterGenome(child).ComputeHash();

            Assert.NotNull(hash);
            Assert.Equal(64, hash.Length);
        }
    }
}
