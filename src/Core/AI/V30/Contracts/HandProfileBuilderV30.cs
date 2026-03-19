using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30.Contracts
{
    /// <summary>
    /// 手牌结构构造器（最小字段版）。
    /// </summary>
    public sealed class HandProfileBuilderV30
    {
        private readonly GameConfig _config;

        public HandProfileBuilderV30(GameConfig config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        public HandProfileV30 Build(List<Card> hand)
        {
            if (hand == null)
                throw new ArgumentNullException(nameof(hand));

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

            int trumpPairCount = trumpCards
                .GroupBy(card => (card.Suit, card.Rank))
                .Sum(group => group.Count() / 2);
            int trumpTractorUnits = CountTractorPairUnits(trumpCards);

            int highTrumpCount = trumpCards.Count(IsHighTrumpCard);
            bool hasControlTrump = highTrumpCount > 0 || trumpPairCount > 0;

            return new HandProfileV30
            {
                TrumpCount = trumpCards.Count,
                HighTrumpCount = highTrumpCount,
                JokerCount = hand.Count(card => card.IsJoker),
                LevelCardCount = hand.Count(card => card.Rank == _config.LevelRank),
                TrumpPairCount = trumpPairCount,
                TrumpTractorCount = trumpTractorUnits,
                ScoreCardCount = hand.Count(card => card.Score > 0),
                HasControlTrump = hasControlTrump,
                SuitLengths = nonTrumpBySuit,
                StrongestSuit = strongestSuit,
                WeakestSuit = weakestSuit,
                PotentialVoidTargets = potentialVoidTargets,
                StructureSummary = $"trump={trumpCards.Count},trump_pairs={trumpPairCount},trump_tractor_units={trumpTractorUnits}"
            };
        }

        private bool IsHighTrumpCard(Card card)
        {
            if (card.Rank == Rank.BigJoker || card.Rank == Rank.SmallJoker)
                return true;

            if (card.Rank == _config.LevelRank)
                return true;

            if (_config.IsTrump(card) && card.Rank == Rank.Ace)
                return true;

            return false;
        }

        private int CountTractorPairUnits(List<Card> trumpCards)
        {
            var pairReps = trumpCards
                .GroupBy(card => (card.Suit, card.Rank))
                .Where(group => group.Count() >= 2)
                .Select(group => group.First())
                .OrderByDescending(card => GetTrumpStrength(card))
                .ToList();

            if (pairReps.Count < 2)
                return 0;

            int totalUnits = 0;
            int index = 0;
            while (index < pairReps.Count - 1)
            {
                int run = 1;
                while (index + run < pairReps.Count &&
                       IsAdjacentTrumpPair(pairReps[index + run - 1], pairReps[index + run]))
                {
                    run++;
                }

                if (run >= 2)
                    totalUnits += run;

                index += run;
            }

            return totalUnits;
        }

        private bool IsAdjacentTrumpPair(Card higher, Card lower)
        {
            var sample = new List<Card> { higher, higher, lower, lower };
            return new CardPattern(sample, _config).IsTractor(sample);
        }

        private int GetTrumpStrength(Card card)
        {
            if (card.Rank == Rank.BigJoker) return 1000;
            if (card.Rank == Rank.SmallJoker) return 900;
            if (card.Rank == _config.LevelRank && _config.TrumpSuit.HasValue && card.Suit == _config.TrumpSuit.Value) return 800;
            if (card.Rank == _config.LevelRank) return 700;
            if (_config.IsTrump(card)) return 600 + (int)card.Rank;
            return (int)card.Rank;
        }
    }
}

