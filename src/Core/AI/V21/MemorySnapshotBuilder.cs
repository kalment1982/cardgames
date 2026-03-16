using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    public sealed class MemorySnapshotBuilder
    {
        public MemorySnapshot Build(CardMemory memory, List<Card>? knownBottomCards = null)
        {
            if (memory == null)
                return new MemorySnapshot();

            return new MemorySnapshot
            {
                PlayedCountByCard = memory.GetPlayedCountSnapshot(),
                VoidSuitsByPlayer = memory.GetVoidSuitsSnapshot(),
                NoPairEvidence = memory.GetNoPairEvidenceSnapshot(),
                NoTractorEvidence = memory.GetNoTractorEvidenceSnapshot(),
                KnownBottomCards = (knownBottomCards ?? new List<Card>()).ConvertAll(card => card.ToString())
            };
        }
    }
}
