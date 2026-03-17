namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 负责统一判定收官阶段、比分压力和保底风险。
    /// </summary>
    public sealed class EndgamePolicy
    {
        public ScorePressureLevel ResolveScorePressure(int defenderScore)
        {
            if (defenderScore >= 70)
                return ScorePressureLevel.Critical;

            if (defenderScore >= 40)
                return ScorePressureLevel.Tight;

            return ScorePressureLevel.Relaxed;
        }

        public EndgameLevel ResolveEndgameLevel(int cardsLeftMin)
        {
            if (cardsLeftMin > 0 && cardsLeftMin <= 2)
                return EndgameLevel.LastTrickRace;

            if (cardsLeftMin > 0 && cardsLeftMin <= 5)
                return EndgameLevel.FinalThree;

            if (cardsLeftMin > 0 && cardsLeftMin <= 8)
                return EndgameLevel.Late;

            return EndgameLevel.None;
        }

        public RiskLevel ResolveBottomRisk(
            AIRole role,
            int bottomPoints,
            int cardsLeftMin,
            ScorePressureLevel scorePressure,
            int defenderScore,
            int remainingScoreTotal)
        {
            if (role != AIRole.Dealer && role != AIRole.DealerPartner)
                return RiskLevel.None;

            if (cardsLeftMin > 0 && cardsLeftMin <= 5 && remainingScoreTotal > 0 && defenderScore + remainingScoreTotal >= 80)
                return RiskLevel.High;

            if (bottomPoints >= 20 && cardsLeftMin > 0 && cardsLeftMin <= 5)
                return RiskLevel.High;

            if (bottomPoints >= 10 && cardsLeftMin > 0 && cardsLeftMin <= 8)
                return RiskLevel.Medium;

            if (bottomPoints >= 10 || scorePressure == ScorePressureLevel.Critical)
                return RiskLevel.Low;

            return RiskLevel.None;
        }

        public RiskLevel ResolveDealerRetentionRisk(AIRole role, int defenderScore, int bottomPoints, int cardsLeftMin)
        {
            if (role != AIRole.Dealer && role != AIRole.DealerPartner)
                return RiskLevel.None;

            if (defenderScore >= 70 && (bottomPoints >= 10 || (cardsLeftMin > 0 && cardsLeftMin <= 5)))
                return RiskLevel.High;

            if (defenderScore >= 50 && bottomPoints >= 10)
                return RiskLevel.Medium;

            if (defenderScore >= 40 && bottomPoints > 0)
                return RiskLevel.Low;

            return RiskLevel.None;
        }

        public RiskLevel ResolveBottomContestPressure(
            AIRole role,
            int defenderScore,
            int remainingScoreTotal,
            int bottomPoints,
            int cardsLeftMin,
            int remainingScoreCards)
        {
            if (role != AIRole.Opponent)
                return RiskLevel.None;

            if (cardsLeftMin <= 0 || remainingScoreTotal <= 0)
                return RiskLevel.None;

            if (remainingScoreCards <= 0)
                return RiskLevel.None;

            int estimatedBottom = bottomPoints > 0
                ? bottomPoints
                : System.Math.Min(remainingScoreTotal, 20);

            bool canWinNoBottom = defenderScore + remainingScoreTotal >= 80;
            bool canWinWithDouble = defenderScore + remainingScoreTotal + estimatedBottom >= 80;

            if (cardsLeftMin <= 5 && !canWinNoBottom && canWinWithDouble)
                return RiskLevel.High;

            if (cardsLeftMin <= 5 && canWinNoBottom)
                return RiskLevel.Medium;

            if (cardsLeftMin <= 8 && defenderScore >= 60 && remainingScoreTotal >= 10)
                return RiskLevel.Low;

            return RiskLevel.None;
        }
    }
}
