using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 跟牌约束检查器
    /// </summary>
    public class FollowValidator
    {
        private readonly GameConfig _config;

        public FollowValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 检查跟牌是否合法
        /// 规则：
        ///   1. 有首引花色 → 必须先跟尽所有首引花色，不足部分才能垫牌/毙牌
        ///   2. 首引是对子 → 有对子必须出对子（同花对子数量足够时）
        ///   3. 首引是拖拉机 → 有拖拉机必须出拖拉机（同花拖拉机存在时）
        ///   4. 无首引花色 → 可以任意垫牌或毙牌
        /// </summary>
        public bool IsValidFollow(List<Card> hand, List<Card> leadCards, List<Card> followCards)
        {
            return IsValidFollowEx(hand, leadCards, followCards).Success;
        }

        /// <summary>
        /// 检查跟牌是否合法，返回失败原因。
        /// </summary>
        public OperationResult IsValidFollowEx(List<Card> hand, List<Card> leadCards, List<Card> followCards)
        {
            if (followCards == null || followCards.Count == 0)
                return OperationResult.Fail(ReasonCodes.FollowCountMismatch);
            if (followCards.Count != leadCards.Count)
                return OperationResult.Fail(ReasonCodes.FollowCountMismatch);
            if (!AllCardsInHand(hand, followCards))
                return OperationResult.Fail(ReasonCodes.CardNotInHand);

            var leadCategory = GetCardCategory(leadCards[0]);
            var leadSuit     = leadCards[0].Suit;
            int needed       = leadCards.Count;

            // 手里有多少张首引花色
            var suitCardsInHand = hand.Where(c => MatchesSuit(c, leadSuit, leadCategory)).ToList();
            int available = suitCardsInHand.Count;

            if (available == 0)
            {
                // 无首引花色，任意出牌合法
                return OperationResult.Ok;
            }

            // 跟牌中属于首引花色的张数
            var followSuitCards = followCards.Where(c => MatchesSuit(c, leadSuit, leadCategory)).ToList();
            int mustFollow = System.Math.Min(available, needed);

            // 必须跟尽所有能跟的首引花色
            if (followSuitCards.Count < mustFollow)
                return OperationResult.Fail(ReasonCodes.FollowSuitRequired);

            // 如果首引花色数量足够填满，还需检查牌型约束
            if (available >= needed)
                return ValidatePatternConstraintEx(hand, leadCards, followCards, leadSuit, leadCategory);

            return OperationResult.Ok;
        }

        private bool AllCardsInHand(List<Card> hand, List<Card> cards)
        {
            var handCopy = new List<Card>(hand);
            foreach (var card in cards)
            {
                var found = handCopy.FirstOrDefault(c => c.Equals(card));
                if (found == null) return false;
                handCopy.Remove(found);
            }
            return true;
        }

        private CardCategory GetCardCategory(Card card)
        {
            return _config.GetCardCategory(card);
        }

        private bool HasSuit(List<Card> hand, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
            {
                // 检查是否有主牌
                return hand.Any(c => _config.IsTrump(c));
            }
            else
            {
                // 检查是否有该花色的副牌
                return hand.Any(c => !_config.IsTrump(c) && c.Suit == suit);
            }
        }

        private bool AllMatchSuit(List<Card> cards, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
            {
                return cards.All(c => _config.IsTrump(c));
            }
            else
            {
                return cards.All(c => !_config.IsTrump(c) && c.Suit == suit);
            }
        }

        private OperationResult ValidatePatternConstraintEx(List<Card> hand, List<Card> leadCards,
            List<Card> followCards, Suit suit, CardCategory category)
        {
            var suitCards = hand.Where(c => MatchesSuit(c, suit, category)).ToList();
            var followSuitCards = followCards.Where(c => MatchesSuit(c, suit, category)).ToList();
            int mustFollow = System.Math.Min(suitCards.Count, leadCards.Count);

            var requirements = BuildLeadRequirements(leadCards);
            if (requirements.TractorPairCounts.Count == 0 && requirements.PairCount == 0)
                return OperationResult.Ok;

            var bestProfile = BuildBestStructureProfile(suitCards, mustFollow, requirements);
            var actualProfile = BuildBestStructureProfile(followSuitCards, followSuitCards.Count, requirements);

            if (actualProfile.CompareTo(bestProfile) >= 0)
                return OperationResult.Ok;

            return OperationResult.Fail(GetStructureReasonCode(bestProfile, actualProfile));
        }

        /// <summary>
        /// 在给定牌组中寻找长度 >= minLen 的拖拉机
        /// </summary>
        private bool FindTractorInCards(List<Card> cards, int minLen)
        {
            if (cards.Count < minLen) return false;
            // 枚举所有子集组合，找到能构成拖拉机且长度 >= minLen 的
            // 简化：按对子为单位检查连续对子
            var comparer = new CardComparer(_config);
            var sorted = cards.OrderBy(c => c, comparer).ToList();

            // 找出所有对子
            var pairs = new List<List<Card>>();
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                if (sorted[i].Equals(sorted[i + 1]))
                {
                    pairs.Add(new List<Card> { sorted[i], sorted[i + 1] });
                    i++; // 跳过已配对
                }
            }

            if (pairs.Count * 2 < minLen) return false;

            // 检查是否有连续对子（拖拉机）
            for (int start = 0; start <= pairs.Count - minLen / 2; start++)
            {
                var candidate = pairs.Skip(start).Take(minLen / 2).SelectMany(p => p).ToList();
                var pattern = new CardPattern(candidate, _config);
                if (pattern.IsTractor(candidate)) return true;
            }
            return false;
        }

        private List<List<Card>> GetAvailablePairs(List<Card> hand, Suit suit, CardCategory category)
        {
            var pairs = new List<List<Card>>();
            var suitCards = hand.Where(c => MatchesSuit(c, suit, category)).ToList();

            for (int i = 0; i < suitCards.Count - 1; i++)
            {
                for (int j = i + 1; j < suitCards.Count; j++)
                {
                    if (suitCards[i].Equals(suitCards[j]))
                    {
                        pairs.Add(new List<Card> { suitCards[i], suitCards[j] });
                    }
                }
            }

            return pairs;
        }

        private LeadRequirements BuildLeadRequirements(List<Card> leadCards)
        {
            var requirements = new LeadRequirements();
            if (leadCards == null || leadCards.Count == 0)
                return requirements;

            var comparer = new CardComparer(_config);
            var pairRepresentatives = new List<Card>();

            var groups = leadCards
                .GroupBy(card => card)
                .Select(group => new { Card = group.Key, Count = group.Count() })
                .ToList();

            foreach (var group in groups)
            {
                int pairCount = group.Count / 2;
                for (int i = 0; i < pairCount; i++)
                    pairRepresentatives.Add(group.Card);
            }

            while (true)
            {
                var tractors = FindAllTractors(pairRepresentatives);
                if (tractors.Count == 0)
                    break;

                var best = tractors
                    .OrderByDescending(tractor => tractor.PairCount)
                    .ThenByDescending(tractor => tractor.HighestCard, comparer)
                    .First();

                requirements.TractorPairCounts.Add(best.PairCount);
                RemovePairRepresentatives(pairRepresentatives, best.Cards);
            }

            requirements.PairCount = pairRepresentatives.Count;
            return requirements;
        }

        private StructureProfile BuildBestStructureProfile(List<Card> cards, int cardLimit, LeadRequirements requirements)
        {
            if (cards == null || cards.Count == 0 || cardLimit <= 0)
                return StructureProfile.Empty(requirements.TractorPairCounts.Count);

            var counts = BuildCounts(cards);
            var memo = new Dictionary<string, StructureProfile>();
            var result = SearchBestStructureProfile(
                counts,
                requirements,
                tractorIndex: 0,
                selectedCards: 0,
                pendingPairRequirements: requirements.PairCount,
                cardLimit,
                memo);

            return result.IsValid
                ? result
                : StructureProfile.Empty(requirements.TractorPairCounts.Count);
        }

        private StructureProfile SearchBestStructureProfile(
            Dictionary<Card, int> counts,
            LeadRequirements requirements,
            int tractorIndex,
            int selectedCards,
            int pendingPairRequirements,
            int cardLimit,
            Dictionary<string, StructureProfile> memo)
        {
            int remainingCards = counts.Values.Sum();
            if (selectedCards > cardLimit || selectedCards + remainingCards < cardLimit)
                return StructureProfile.Invalid(requirements.TractorPairCounts.Count - tractorIndex);

            string cacheKey = BuildStateKey(counts, tractorIndex, selectedCards, pendingPairRequirements);
            if (memo.TryGetValue(cacheKey, out var cached))
                return cached;

            if (tractorIndex >= requirements.TractorPairCounts.Count)
            {
                int remainingPairs = GetAvailablePairCount(counts);
                int pairMatches = System.Math.Min(
                    System.Math.Min(remainingPairs, pendingPairRequirements),
                    (cardLimit - selectedCards) / 2);

                var terminal = new StructureProfile(
                    new List<bool>(),
                    pairMatches,
                    isValid: true);
                memo[cacheKey] = terminal;
                return terminal;
            }

            int tractorPairCount = requirements.TractorPairCounts[tractorIndex];
            var best = SearchBestStructureProfile(
                counts,
                requirements,
                tractorIndex + 1,
                selectedCards,
                pendingPairRequirements + tractorPairCount,
                cardLimit,
                memo).Prepend(false);

            int tractorCardCount = tractorPairCount * 2;
            if (selectedCards + tractorCardCount <= cardLimit)
            {
                foreach (var candidate in FindAllTractors(GetPairRepresentatives(counts), tractorPairCount))
                {
                    var nextCounts = CloneCounts(counts);
                    if (!TryConsumeCards(nextCounts, candidate.Cards))
                        continue;

                    var matched = SearchBestStructureProfile(
                        nextCounts,
                        requirements,
                        tractorIndex + 1,
                        selectedCards + tractorCardCount,
                        pendingPairRequirements,
                        cardLimit,
                        memo).Prepend(true);

                    if (matched.CompareTo(best) > 0)
                        best = matched;
                }
            }

            memo[cacheKey] = best;
            return best;
        }

        private Dictionary<Card, int> BuildCounts(List<Card> cards)
        {
            return cards
                .GroupBy(card => card)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        private Dictionary<Card, int> CloneCounts(Dictionary<Card, int> counts)
        {
            return counts.ToDictionary(entry => entry.Key, entry => entry.Value);
        }

        private bool TryConsumeCards(Dictionary<Card, int> counts, List<Card> cards)
        {
            foreach (var card in cards)
            {
                if (!counts.TryGetValue(card, out var count) || count <= 0)
                    return false;
                counts[card] = count - 1;
            }

            var emptyKeys = counts.Where(entry => entry.Value == 0).Select(entry => entry.Key).ToList();
            foreach (var key in emptyKeys)
                counts.Remove(key);

            return true;
        }

        private string BuildStateKey(Dictionary<Card, int> counts, int tractorIndex, int selectedCards, int pendingPairRequirements)
        {
            var comparer = new CardComparer(_config);
            var cardState = string.Join("|",
                counts.OrderBy(entry => entry.Key, comparer)
                    .Select(entry => $"{entry.Key.Suit}-{entry.Key.Rank}:{entry.Value}"));
            return $"{tractorIndex}:{selectedCards}:{pendingPairRequirements}:{cardState}";
        }

        private int GetAvailablePairCount(Dictionary<Card, int> counts)
        {
            return counts.Values.Sum(count => count / 2);
        }

        private List<TractorCandidate> FindAllTractors(List<Card> pairRepresentatives)
        {
            return FindAllTractors(pairRepresentatives, 2);
        }

        private List<TractorCandidate> FindAllTractors(List<Card> pairRepresentatives, int exactPairCount)
        {
            var tractors = new List<TractorCandidate>();
            if (pairRepresentatives.Count < exactPairCount || exactPairCount < 2)
                return tractors;

            var comparer = new CardComparer(_config);
            int count = pairRepresentatives.Count;
            int maskLimit = 1 << count;

            for (int mask = 0; mask < maskLimit; mask++)
            {
                int pairCount = CountBits(mask);
                if (pairCount != exactPairCount)
                    continue;

                var candidatePairs = new List<Card>(pairCount);
                var candidateCards = new List<Card>(pairCount * 2);

                for (int i = 0; i < count; i++)
                {
                    if ((mask & (1 << i)) == 0)
                        continue;

                    var pair = pairRepresentatives[i];
                    candidatePairs.Add(pair);
                    candidateCards.Add(pair);
                    candidateCards.Add(pair);
                }

                var pattern = new CardPattern(candidateCards, _config);
                if (!pattern.IsTractor(candidateCards))
                    continue;

                var sortedPairs = candidatePairs.OrderBy(card => card, comparer).ToList();
                tractors.Add(new TractorCandidate(
                    candidateCards,
                    pairCount,
                    sortedPairs[^1]));
            }

            return tractors;
        }

        private List<Card> GetPairRepresentatives(Dictionary<Card, int> counts)
        {
            var comparer = new CardComparer(_config);
            var pairRepresentatives = new List<Card>();

            foreach (var entry in counts)
            {
                int pairCount = entry.Value / 2;
                for (int i = 0; i < pairCount; i++)
                    pairRepresentatives.Add(entry.Key);
            }

            return pairRepresentatives.OrderByDescending(card => card, comparer).ToList();
        }

        private void RemovePairRepresentatives(List<Card> sourcePairs, List<Card> usedCards)
        {
            var toRemove = usedCards
                .GroupBy(card => card)
                .ToDictionary(group => group.Key, group => group.Count() / 2);

            foreach (var entry in toRemove)
            {
                int remaining = entry.Value;
                for (int i = sourcePairs.Count - 1; i >= 0 && remaining > 0; i--)
                {
                    if (!sourcePairs[i].Equals(entry.Key))
                        continue;

                    sourcePairs.RemoveAt(i);
                    remaining--;
                }
            }
        }

        private int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        private string GetStructureReasonCode(StructureProfile bestProfile, StructureProfile actualProfile)
        {
            for (int i = 0; i < bestProfile.TractorMatches.Count && i < actualProfile.TractorMatches.Count; i++)
            {
                if (bestProfile.TractorMatches[i] == actualProfile.TractorMatches[i])
                    continue;

                return ReasonCodes.FollowTractorRequired;
            }

            if (actualProfile.PairMatches < bestProfile.PairMatches)
                return ReasonCodes.FollowPairRequired;

            return ReasonCodes.FollowSuitRequired;
        }

        private bool MatchesSuit(Card card, Suit suit, CardCategory category)
        {
            if (category == CardCategory.Trump)
                return _config.IsTrump(card);
            else
                return !_config.IsTrump(card) && card.Suit == suit;
        }

        private sealed class LeadRequirements
        {
            public List<int> TractorPairCounts { get; } = new();
            public int PairCount { get; set; }
        }

        private sealed class TractorCandidate
        {
            public TractorCandidate(List<Card> cards, int pairCount, Card highestCard)
            {
                Cards = cards;
                PairCount = pairCount;
                HighestCard = highestCard;
            }

            public List<Card> Cards { get; }
            public int PairCount { get; }
            public Card HighestCard { get; }
        }

        private sealed class StructureProfile
        {
            public StructureProfile(List<bool> tractorMatches, int pairMatches, bool isValid)
            {
                TractorMatches = tractorMatches;
                PairMatches = pairMatches;
                IsValid = isValid;
            }

            public List<bool> TractorMatches { get; }
            public int PairMatches { get; }
            public bool IsValid { get; }

            public StructureProfile Prepend(bool tractorMatched)
            {
                if (!IsValid)
                    return this;

                var matches = new List<bool>(TractorMatches.Count + 1) { tractorMatched };
                matches.AddRange(TractorMatches);
                return new StructureProfile(matches, PairMatches, true);
            }

            public int CompareTo(StructureProfile other)
            {
                if (IsValid != other.IsValid)
                    return IsValid ? 1 : -1;

                int tractorCount = System.Math.Min(TractorMatches.Count, other.TractorMatches.Count);
                for (int i = 0; i < tractorCount; i++)
                {
                    if (TractorMatches[i] == other.TractorMatches[i])
                        continue;

                    return TractorMatches[i] ? 1 : -1;
                }

                return PairMatches.CompareTo(other.PairMatches);
            }

            public static StructureProfile Empty(int tractorCount)
            {
                return new StructureProfile(Enumerable.Repeat(false, tractorCount).ToList(), 0, true);
            }

            public static StructureProfile Invalid(int remainingTractors)
            {
                return new StructureProfile(Enumerable.Repeat(false, System.Math.Max(0, remainingTractors)).ToList(), 0, false);
            }
        }
    }
}
