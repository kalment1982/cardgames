using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    public sealed class HandProfileBuilder
    {
        private readonly GameConfig _config;

        public HandProfileBuilder(GameConfig config)
        {
            _config = config;
        }

        public HandProfile Build(List<Card> hand)
        {
            hand ??= new List<Card>();
            var comparer = new CardComparer(_config);
            var trumpCards = hand.Where(_config.IsTrump).ToList();
            var nonTrumpBySuit = hand
                .Where(card => !_config.IsTrump(card))
                .GroupBy(card => card.Suit)
                .ToDictionary(group => group.Key, group => group.Count());

            var strongestSuit = nonTrumpBySuit.Count == 0
                ? (Suit?)null
                : nonTrumpBySuit.OrderByDescending(entry => entry.Value).ThenBy(entry => entry.Key).First().Key;
            var weakestSuit = nonTrumpBySuit.Count == 0
                ? (Suit?)null
                : nonTrumpBySuit.OrderBy(entry => entry.Value).ThenBy(entry => entry.Key).First().Key;

            var potentialVoidTargets = nonTrumpBySuit
                .Where(entry => entry.Value <= 3)
                .OrderBy(entry => entry.Value)
                .ThenBy(entry => entry.Key)
                .Select(entry => entry.Key)
                .ToList();

            var trumpPairs = trumpCards.GroupBy(card => card).Sum(group => group.Count() / 2);
            var trumpTractors = RuleAIUtility.CountTractorPairUnits(_config, trumpCards, comparer);

            return new HandProfile
            {
                TrumpCount = trumpCards.Count,
                HighTrumpCount = trumpCards.Count(card => RuleAIUtility.GetCardValue(card, _config) >= 700),
                JokerCount = hand.Count(card => card.IsJoker),
                LevelCardCount = hand.Count(card => card.Rank == _config.LevelRank),
                TrumpPairCount = trumpPairs,
                TrumpTractorCount = trumpTractors,
                SuitLengths = nonTrumpBySuit,
                StrongestSuit = strongestSuit,
                WeakestSuit = weakestSuit,
                PotentialVoidTargets = potentialVoidTargets,
                ScoreCardCount = hand.Count(card => card.Score > 0),
                StructureSummary = $"trump={trumpCards.Count},pairs={RuleAIUtility.CountPairs(hand)},tractors={RuleAIUtility.CountTractorPairUnits(_config, hand, comparer)}"
            };
        }
    }
}
