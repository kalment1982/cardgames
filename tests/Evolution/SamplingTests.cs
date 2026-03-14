using System;
using TractorGame.Core.AI.Evolution.DataEngine;
using Xunit;

namespace TractorGame.Tests.Evolution
{
    [Collection("EvolutionSerial")]
    public class SamplingTests
    {
        [Fact]
        public void StratifiedSampler_ReturnsRequestedCount()
        {
            var sampler = new StratifiedSampler(seed: 1);
            var weighter = new FreshnessWeighter(7);

            for (var i = 0; i < 120; i++)
            {
                var isLongTail = i % 13 == 0;
                sampler.AddSample(new TrainingSample
                {
                    Difficulty = i % 2 == 0 ? "hard" : "medium",
                    Role = i % 3 == 0 ? "dealer" : "opponent",
                    Phase = i % 4 == 0 ? "late" : "mid",
                    Pattern = isLongTail ? "tractor" : "single",
                    TimestampUtc = DateTime.UtcNow.AddDays(-i % 10),
                    IsHardCase = i % 25 == 0,
                    FreshnessWeight = weighter.CalculateWeight(DateTime.UtcNow.AddDays(-i % 10), isHardCase: i % 25 == 0, isLongTail: isLongTail)
                });
            }

            var samples = sampler.Sample(40, longTailRatio: 0.30);
            Assert.Equal(40, samples.Count);
        }
    }
}
