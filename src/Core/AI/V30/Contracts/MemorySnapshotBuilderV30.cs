using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 记牌快照构造器（只复制当前可用事实，不做额外推断）。
    /// </summary>
    public sealed class MemorySnapshotBuilderV30
    {
        public MemorySnapshotV30 Build(CardMemory? memory, List<Card>? knownBottomCards = null)
        {
            if (memory == null)
            {
                return new MemorySnapshotV30
                {
                    KnownBottomCards = (knownBottomCards ?? new List<Card>()).ConvertAll(card => card.ToString())
                };
            }

            return new MemorySnapshotV30
            {
                PlayedCountByCard = memory.GetPlayedCountSnapshot(),
                VoidSuitsByPlayer = memory.GetVoidSuitsSnapshot(),
                NoPairEvidence = memory.GetNoPairEvidenceSnapshot(),
                NoTractorEvidence = memory.GetNoTractorEvidenceSnapshot(),
                KnownBottomCards = (knownBottomCards ?? new List<Card>()).ConvertAll(card => card.ToString()),
                PlayedScoreTotal = memory.GetPlayedScoreTotal(),
                PlayedScoreCardCount = memory.GetPlayedScoreCardCount()
            };
        }
    }
}

