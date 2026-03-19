using System.Collections.Generic;
using TractorGame.Core.AI.V30.Bottom;
using Xunit;

namespace TractorGame.Tests.V30.Bottom
{
    public class BottomScoreEstimatorV30Tests
    {
        [Fact]
        public void EstimateBottomPoints_UsesDefault10_WhenNoKnownAndNoSignal()
        {
            var estimator = new BottomScoreEstimatorV30();
            var estimated = estimator.EstimateBottomPoints(knownBottomPoints: null);
            Assert.Equal(10, estimated);
        }

        [Fact]
        public void EstimateBottomPoints_UsesKnownValue_WhenDealerKnowsBottom()
        {
            var estimator = new BottomScoreEstimatorV30();
            var estimated = estimator.EstimateBottomPoints(knownBottomPoints: 24);
            Assert.Equal(24, estimated);
        }

        [Fact]
        public void EstimateBottomPoints_RaisesEstimate_WhenHighSignalsAppear()
        {
            var estimator = new BottomScoreEstimatorV30();
            var signals = new List<BottomScoreSignalV30>
            {
                new BottomScoreSignalV30
                {
                    SignalType = BottomScoreSignalTypeV30.SuitExhaustedScoreUnseen,
                    Confidence = 0.9
                },
                new BottomScoreSignalV30
                {
                    SignalType = BottomScoreSignalTypeV30.MultiSuitExhaustedScoreUnseen,
                    Confidence = 0.9
                }
            };

            var estimated = estimator.EstimateBottomPoints(knownBottomPoints: null, signals);
            Assert.Equal(25, estimated);
        }

        [Fact]
        public void EstimateRemainingContestableScore_AddsUnplayedAndEstimatedBottom()
        {
            var estimator = new BottomScoreEstimatorV30();
            var score = estimator.EstimateRemainingContestableScore(unplayedScorePoints: 28, estimatedBottomPoints: 10);
            Assert.Equal(38, score);
        }

        [Theory]
        [InlineData(8, BottomScoreBandV30.Low)]
        [InlineData(15, BottomScoreBandV30.Medium)]
        [InlineData(30, BottomScoreBandV30.High)]
        public void ResolveBottomScoreBand_MapsRanges(int points, BottomScoreBandV30 expected)
        {
            var estimator = new BottomScoreEstimatorV30();
            Assert.Equal(expected, estimator.ResolveBottomScoreBand(points));
        }
    }
}
