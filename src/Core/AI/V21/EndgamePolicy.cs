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

        public RiskLevel ResolveBottomRisk(AIRole role, int bottomPoints, int cardsLeftMin, ScorePressureLevel scorePressure)
        {
            if (role != AIRole.Dealer && role != AIRole.DealerPartner)
                return RiskLevel.None;

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
    }
}
