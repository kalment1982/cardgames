using TractorGame.Core.AI;
using TractorGame.Core.AI.Evolution.PolicyFactory;
using Xunit;

namespace TractorGame.Tests.Evolution
{
    [Collection("EvolutionSerial")]
    public class MutationTests
    {
        [Fact]
        public void MutationAndRepair_KeepRandomnessMonotonic()
        {
            var baseParams = AIStrategyParameters.CreatePreset(AIDifficulty.Hard);
            var mutator = new MutationOperator(42);
            var repair = new RepairOperator();

            var mutated = mutator.Mutate(baseParams, exploratory: true);
            var fixedParams = repair.Repair(mutated);

            Assert.True(fixedParams.EasyRandomnessRate >= fixedParams.MediumRandomnessRate);
            Assert.True(fixedParams.MediumRandomnessRate >= fixedParams.HardRandomnessRate);
            Assert.True(fixedParams.HardRandomnessRate >= fixedParams.ExpertRandomnessRate);
            Assert.InRange(fixedParams.LeadThrowMinAdvantage, 0, 3);
        }
    }
}
