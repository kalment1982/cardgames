using System;

namespace TractorGame.Core.AI.Evolution.ReleaseManager
{
    public sealed class CooldownManager
    {
        public bool IsInCooldown(EvolutionState state)
        {
            return state.CooldownUntilUtc.HasValue && state.CooldownUntilUtc.Value > DateTime.UtcNow;
        }

        public void StartCooldown(EvolutionState state, int hours)
        {
            state.CooldownUntilUtc = DateTime.UtcNow.AddHours(hours);
        }

        public void ClearCooldown(EvolutionState state)
        {
            state.CooldownUntilUtc = null;
        }
    }
}
