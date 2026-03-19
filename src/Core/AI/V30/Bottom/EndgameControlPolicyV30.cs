namespace TractorGame.Core.AI.V30.Bottom
{
    /// <summary>
    /// Applies endgame control constraints under protect-bottom pressure.
    /// </summary>
    public sealed class EndgameControlPolicyV30
    {
        public JokerControlDecisionV30 DecideJokerOrder(JokerControlInputV30 input)
        {
            bool shouldPlaySmallFirst = true;
            string reason = "Default_SmallThenBig";

            if (input.BigJokerUnplayedLikelyInRearOpponent)
            {
                shouldPlaySmallFirst = false;
                reason = "RearLikelyHasBigJoker";
            }
            else if (input.RearOpponentLikelyHasStrongerTrumpStructure)
            {
                shouldPlaySmallFirst = false;
                reason = "RearLikelyHasStrongerStructure";
            }
            else if (input.SmallJokerSecurity == WinSecurityTierV30.FragileWin)
            {
                shouldPlaySmallFirst = false;
                reason = "SmallJokerOnlyFragileWin";
            }

            return new JokerControlDecisionV30
            {
                ShouldPlaySmallJokerFirst = shouldPlaySmallFirst,
                Reason = reason
            };
        }

        public EndgameControlDecisionV30 ResolveTrumpControl(EndgameControlInputV30 input)
        {
            bool freeze = input.OperationalMode == BottomOperationalModeV30.StrongProtectBottom;
            bool allowConcedeLowPoint = freeze && input.CurrentTrickPoints <= 10;

            return new EndgameControlDecisionV30
            {
                FreezeTrumpResources = freeze,
                AllowConcedeLowPointTrick = allowConcedeLowPoint
            };
        }
    }
}
