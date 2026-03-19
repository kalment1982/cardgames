using System.Collections.Generic;

namespace TractorGame.Core.AI.V30.Bottom
{
    /// <summary>
    /// Estimates bottom points for non-dealer perspective and aggregates contestable score.
    /// </summary>
    public sealed class BottomScoreEstimatorV30
    {
        public const int DefaultBottomEstimate = 10;

        public int EstimateBottomPoints(int? knownBottomPoints, IReadOnlyList<BottomScoreSignalV30>? signals = null)
        {
            if (knownBottomPoints.HasValue)
                return ClampBottomEstimate(knownBottomPoints.Value);

            int estimate = DefaultBottomEstimate;
            if (signals == null || signals.Count == 0)
                return estimate;

            for (int i = 0; i < signals.Count; i++)
            {
                var signal = signals[i];
                if (signal.Confidence < 0.5)
                    continue;

                estimate += signal.SignalType switch
                {
                    BottomScoreSignalTypeV30.SuitExhaustedScoreUnseen => 5,
                    BottomScoreSignalTypeV30.MultiSuitExhaustedScoreUnseen => 10,
                    BottomScoreSignalTypeV30.ExplicitHighBottomEvidence => signal.SuggestedPoints > 0 ? signal.SuggestedPoints : 15,
                    _ => 0
                };
            }

            return ClampBottomEstimate(estimate);
        }

        public int EstimateRemainingContestableScore(int unplayedScorePoints, int estimatedBottomPoints)
        {
            if (unplayedScorePoints < 0)
                unplayedScorePoints = 0;

            if (estimatedBottomPoints < 0)
                estimatedBottomPoints = 0;

            return unplayedScorePoints + estimatedBottomPoints;
        }

        public BottomScoreBandV30 ResolveBottomScoreBand(int bottomPoints)
        {
            if (bottomPoints <= 10)
                return BottomScoreBandV30.Low;

            if (bottomPoints >= 30)
                return BottomScoreBandV30.High;

            return BottomScoreBandV30.Medium;
        }

        private static int ClampBottomEstimate(int estimate)
        {
            if (estimate < 0)
                return 0;

            if (estimate > 60)
                return 60;

            return estimate;
        }
    }
}
