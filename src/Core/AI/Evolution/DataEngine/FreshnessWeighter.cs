using System;

namespace TractorGame.Core.AI.Evolution.DataEngine
{
    public sealed class FreshnessWeighter
    {
        private readonly double _halfLifeDays;

        public FreshnessWeighter(double halfLifeDays = 7.0)
        {
            _halfLifeDays = halfLifeDays <= 0 ? 7.0 : halfLifeDays;
        }

        public double CalculateWeight(DateTime timestampUtc, bool isHardCase = false, bool isLongTail = false)
        {
            if (isHardCase)
                return 1.0;

            var ageDays = Math.Max(0, (DateTime.UtcNow - timestampUtc).TotalDays);
            var adjustedHalfLife = isLongTail ? _halfLifeDays * 2.0 : _halfLifeDays;
            return Math.Exp(-Math.Log(2.0) * ageDays / adjustedHalfLife);
        }
    }
}
