namespace TractorGame.Core.AI.Evolution.GateKeeper
{
    public sealed class StagnationDetector
    {
        public bool IsStagnating(int consecutiveNoPromotion, int threshold)
        {
            return consecutiveNoPromotion >= threshold;
        }
    }
}
