using System.Collections.Generic;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class DeduplicationCache
    {
        private readonly HashSet<string> _hashes = new();

        public bool TryAdd(string hash)
        {
            if (string.IsNullOrWhiteSpace(hash))
                return false;

            return _hashes.Add(hash);
        }

        public int Count => _hashes.Count;
    }
}
