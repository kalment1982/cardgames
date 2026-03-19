namespace TractorGame.Core.AI.V30.Bottom
{
    /// <summary>
    /// Chooses single-bottom vs double-bottom plan under risk-control constraints.
    /// </summary>
    public sealed class BottomPlanSelectorV30
    {
        public const int WinlineScore = 80;

        public BottomPlanDecisionV30 Select(BottomPlanInputV30 input)
        {
            bool canSingle = input.DefenderScore + input.SingleBottomGainPoints >= WinlineScore;
            bool canDouble = input.DefenderScore + input.DoubleBottomGainPoints >= WinlineScore;

            if (canSingle)
            {
                if (canDouble && input.DoublePlanStability == input.SinglePlanStability)
                {
                    return new BottomPlanDecisionV30
                    {
                        Goal = BottomPlanGoalV30.DoubleBottomPreferred,
                        CanWinWithSingleBottom = true,
                        CanWinWithDoubleBottom = true,
                        ShouldPreservePairsAndTractors = true,
                        Reason = "SingleAlreadyWins_ButDoubleEquallyStable"
                    };
                }

                return new BottomPlanDecisionV30
                {
                    Goal = BottomPlanGoalV30.SingleBottomPreferred,
                    CanWinWithSingleBottom = true,
                    CanWinWithDoubleBottom = canDouble,
                    ShouldPreservePairsAndTractors = false,
                    Reason = "SingleAlreadyWins_ReduceRisk"
                };
            }

            if (canDouble)
            {
                return new BottomPlanDecisionV30
                {
                    Goal = BottomPlanGoalV30.DoubleBottomPreferred,
                    CanWinWithSingleBottom = false,
                    CanWinWithDoubleBottom = true,
                    ShouldPreservePairsAndTractors = true,
                    Reason = "NeedDoubleToReachWinline"
                };
            }

            return new BottomPlanDecisionV30
            {
                Goal = BottomPlanGoalV30.NoBottomLine,
                CanWinWithSingleBottom = false,
                CanWinWithDoubleBottom = false,
                ShouldPreservePairsAndTractors = false,
                Reason = "BottomNotEnoughToReachWinline"
            };
        }
    }
}
