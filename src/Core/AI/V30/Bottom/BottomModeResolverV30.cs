using TractorGame.Core.AI;

namespace TractorGame.Core.AI.V30.Bottom
{
    /// <summary>
    /// Resolves dealer protect-bottom mode and opponent contest-bottom mode.
    /// </summary>
    public sealed class BottomModeResolverV30
    {
        public BottomModeDecisionV30 Resolve(BottomModeInputV30 input)
        {
            var band = ResolveBottomScoreBand(input.BottomPoints);
            var operational = ResolveOperationalMode(
                input.Role,
                input.DefenderScore,
                input.RemainingContestableScore,
                input.BottomPoints);
            var contest = ResolveContestMode(
                input.Role,
                input.DefenderScore,
                input.EstimatedBottomPoints,
                input.BottomMultiplier);

            return new BottomModeDecisionV30
            {
                BottomScoreBand = band,
                OperationalMode = operational,
                ContestMode = contest
            };
        }

        public BottomScoreBandV30 ResolveBottomScoreBand(int bottomPoints)
        {
            if (bottomPoints <= 10)
                return BottomScoreBandV30.Low;

            if (bottomPoints >= 30)
                return BottomScoreBandV30.High;

            return BottomScoreBandV30.Medium;
        }

        public BottomOperationalModeV30 ResolveOperationalMode(
            AIRole role,
            int defenderScore,
            int remainingContestableScore,
            int bottomPoints)
        {
            if (role != AIRole.Dealer && role != AIRole.DealerPartner)
                return BottomOperationalModeV30.NormalOperation;

            bool attention = defenderScore + remainingContestableScore > 50 && bottomPoints >= 15;
            bool strong = defenderScore + (bottomPoints * 2) >= 70;

            if (strong)
                return BottomOperationalModeV30.StrongProtectBottom;

            if (attention)
                return BottomOperationalModeV30.ProtectBottomAttention;

            return BottomOperationalModeV30.NormalOperation;
        }

        public BottomContestModeV30 ResolveContestMode(
            AIRole role,
            int defenderScore,
            int estimatedBottomPoints,
            int bottomMultiplier)
        {
            if (role != AIRole.Opponent)
                return BottomContestModeV30.NormalContest;

            int normalizedMultiplier = bottomMultiplier < 1 ? 1 : bottomMultiplier;
            bool strong = defenderScore + estimatedBottomPoints * normalizedMultiplier >= 80;
            if (strong)
                return BottomContestModeV30.StrongContestBottom;

            if (defenderScore >= 60)
                return BottomContestModeV30.ContestBottomAttention;

            if (defenderScore >= 50)
                return BottomContestModeV30.ContestBottomAttention;

            return BottomContestModeV30.NormalContest;
        }
    }
}
