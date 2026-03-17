using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.V21
{
    internal static class RuleAIUtility
    {
        public const int TotalScorePoints = 200;
        public const int TotalScoreCardCount = 24;

        public static int GetCardPoints(Card card)
        {
            return card.Score;
        }

        public static int GetCardValue(Card card, GameConfig config)
        {
            if (card.Rank == Rank.BigJoker) return 1000;
            if (card.Rank == Rank.SmallJoker) return 900;

            bool isLevel = card.Rank == config.LevelRank;
            bool isTrumpSuit = config.TrumpSuit.HasValue && card.Suit == config.TrumpSuit.Value;

            if (isLevel && isTrumpSuit) return 800;
            if (isLevel) return 700;
            if (config.IsTrump(card)) return 600 + (int)card.Rank;
            return 100 + (int)card.Rank;
        }

        public static int CountPairs(IEnumerable<Card> cards)
        {
            return cards
                .GroupBy(card => (card.Suit, card.Rank))
                .Sum(group => group.Count() / 2);
        }

        public static int CountTractorPairUnits(GameConfig config, List<Card> cards, CardComparer comparer)
        {
            int total = 0;
            foreach (var group in BuildSystemGroups(config, cards))
            {
                var pairReps = group
                    .GroupBy(card => card)
                    .Where(g => g.Count() >= 2)
                    .Select(g => g.Key)
                    .OrderByDescending(card => card, comparer)
                    .ToList();

                if (pairReps.Count < 2)
                    continue;

                int index = 0;
                while (index < pairReps.Count - 1)
                {
                    int run = 1;
                    while (index + run < pairReps.Count &&
                        IsConsecutivePairForTractor(config, pairReps[index + run - 1], pairReps[index + run]))
                    {
                        run++;
                    }

                    if (run >= 2)
                        total += run;

                    index += run;
                }
            }

            return total;
        }

        public static List<List<Card>> BuildSystemGroups(GameConfig config, List<Card> cards)
        {
            var groups = new List<List<Card>>();

            var trumpCards = cards.Where(config.IsTrump).ToList();
            if (trumpCards.Count > 0)
                groups.Add(trumpCards);

            groups.AddRange(cards
                .Where(card => !config.IsTrump(card))
                .GroupBy(card => card.Suit)
                .Select(group => group.ToList()));

            return groups;
        }

        public static bool IsConsecutivePairForTractor(GameConfig config, Card higher, Card lower)
        {
            var cards = new List<Card> { higher, higher, lower, lower };
            return new CardPattern(cards, config).IsTractor(cards);
        }

        public static bool MatchesLeadCategory(GameConfig config, Card card, Suit leadSuit, CardCategory leadCategory)
        {
            if (leadCategory == CardCategory.Trump)
                return config.IsTrump(card);

            return !config.IsTrump(card) && card.Suit == leadSuit;
        }

        public static List<Card> RemoveCards(List<Card> source, IEnumerable<Card> toRemove)
        {
            var copy = new List<Card>(source);
            foreach (var card in toRemove)
            {
                var found = copy.FirstOrDefault(existing => existing.Equals(card));
                if (found != null)
                    copy.Remove(found);
            }

            return copy;
        }

        public static List<List<Card>> DeduplicateCandidates(IEnumerable<List<Card>> candidates)
        {
            var result = new List<List<Card>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.Count == 0)
                    continue;

                var key = BuildCandidateKey(candidate);
                if (seen.Add(key))
                    result.Add(candidate);
            }

            return result;
        }

        public static string BuildCandidateKey(IEnumerable<Card> cards)
        {
            return string.Join(",", cards
                .OrderBy(card => (int)card.Suit)
                .ThenBy(card => (int)card.Rank)
                .Select(card => $"{(int)card.Suit}-{(int)card.Rank}"));
        }

        public static List<Card>? FindStrongestPair(List<Card> cards, CardComparer comparer)
        {
            var groups = cards.GroupBy(card => (card.Suit, card.Rank))
                .Where(group => group.Count() >= 2)
                .Select(group => group.Take(2).ToList())
                .ToList();

            if (groups.Count == 0)
                return null;

            return groups
                .OrderByDescending(group => group[0], comparer)
                .First();
        }

        public static List<Card>? FindSmallestPair(List<Card> cards, CardComparer comparer)
        {
            var groups = cards.GroupBy(card => (card.Suit, card.Rank))
                .Where(group => group.Count() >= 2)
                .Select(group => group.Take(2).ToList())
                .ToList();

            if (groups.Count == 0)
                return null;

            return groups
                .OrderBy(group => group[0], comparer)
                .First();
        }

        public static List<Card>? FindStrongestTractor(GameConfig config, List<Card> cards, int neededCount, CardComparer comparer)
        {
            if (neededCount < 4 || neededCount % 2 != 0)
                return null;

            int pairCount = neededCount / 2;
            var pairUnits = cards
                .GroupBy(card => (card.Suit, card.Rank))
                .Where(group => group.Count() >= 2)
                .Select(group => group.Take(2).ToList())
                .OrderByDescending(group => group[0], comparer)
                .ToList();

            if (pairUnits.Count < pairCount)
                return null;

            for (int start = 0; start <= pairUnits.Count - pairCount; start++)
            {
                var candidate = pairUnits.Skip(start).Take(pairCount).SelectMany(group => group).ToList();
                if (new CardPattern(candidate, config).IsTractor(candidate))
                    return candidate;
            }

            // 滑窗未找到时，全量组合搜索（无上限，主牌对子数通常 <= 15）
            long combinationCount = EstimateCombinationCount(pairUnits.Count, pairCount);
            if (combinationCount <= 2000)
            {
                foreach (var combo in Combinations(pairUnits, pairCount))
                {
                    var candidate = combo.SelectMany(group => group).ToList();
                    if (new CardPattern(candidate, config).IsTractor(candidate))
                        return candidate;
                }
            }

            return null;
        }

        public static List<Card>? FindSmallestTractor(GameConfig config, List<Card> cards, int neededCount, CardComparer comparer)
        {
            if (neededCount < 4 || neededCount % 2 != 0)
                return null;

            int pairCount = neededCount / 2;
            var pairUnits = cards
                .GroupBy(card => (card.Suit, card.Rank))
                .Where(group => group.Count() >= 2)
                .Select(group => group.Take(2).ToList())
                .OrderBy(group => group[0], comparer)
                .ToList();

            if (pairUnits.Count < pairCount)
                return null;

            for (int start = 0; start <= pairUnits.Count - pairCount; start++)
            {
                var candidate = pairUnits.Skip(start).Take(pairCount).SelectMany(group => group).ToList();
                if (new CardPattern(candidate, config).IsTractor(candidate))
                    return candidate;
            }

            long combinationCount = EstimateCombinationCount(pairUnits.Count, pairCount);
            if (combinationCount <= 2000)
            {
                foreach (var combo in Combinations(pairUnits, pairCount))
                {
                    var candidate = combo.SelectMany(group => group).ToList();
                    if (new CardPattern(candidate, config).IsTractor(candidate))
                        return candidate;
                }
            }

            return null;
        }

        public static bool CanBeatCards(GameConfig config, List<Card> currentWinningCards, List<Card> followCards)
        {
            if (currentWinningCards.Count != followCards.Count)
                return false;

            var judge = new TrickJudge(config);
            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, currentWinningCards),
                new TrickPlay(1, followCards)
            };

            return judge.DetermineWinner(plays) == 1;
        }

        public static int CalculateWinMargin(GameConfig config, List<Card> candidate, List<Card> currentWinningCards, CardComparer comparer)
        {
            // 多张牌型胜负由整体决定，不能逐张比较
            if (!CanBeatCards(config, currentWinningCards, candidate))
                return 0;

            // 用最高控制牌的价值差估算优势幅度
            var topCandidate = candidate.OrderByDescending(c => GetCardValue(c, config)).First();
            var topWinning = currentWinningCards.OrderByDescending(c => GetCardValue(c, config)).First();
            return Math.Max(0, GetCardValue(topCandidate, config) - GetCardValue(topWinning, config));
        }

        public static int CompareCardSets(GameConfig config, List<Card> a, List<Card> b, CardComparer comparer)
        {
            var sa = a.OrderByDescending(card => GetCardValue(card, config)).ToList();
            var sb = b.OrderByDescending(card => GetCardValue(card, config)).ToList();

            int n = Math.Min(sa.Count, sb.Count);
            for (int i = 0; i < n; i++)
            {
                int cmp = comparer.Compare(sa[i], sb[i]);
                if (cmp != 0)
                    return cmp;

                int va = GetCardValue(sa[i], config);
                int vb = GetCardValue(sb[i], config);
                if (va != vb)
                    return va.CompareTo(vb);
            }

            return sa.Count.CompareTo(sb.Count);
        }

        public static int EstimateStructureValue(GameConfig config, List<Card> cards, CardComparer comparer)
        {
            if (cards == null || cards.Count == 0)
                return 0;

            int pairValue = CountPairs(cards) * 8;
            int tractorValue = CountTractorPairUnits(config, cards, comparer) * 12;
            return pairValue + tractorValue;
        }

        public static int EstimateStructureLoss(GameConfig config, List<Card> source, List<Card> toRemove, CardComparer comparer)
        {
            var before = EstimateStructureValue(config, source, comparer);
            var after = EstimateStructureValue(config, RemoveCards(source, toRemove), comparer);
            return Math.Max(0, before - after);
        }

        public static int EstimateSingleCardDiscardCost(GameConfig config, List<Card> source, Card card, CardComparer comparer)
        {
            var remaining = RemoveCards(source, new List<Card> { card });
            int structureLoss = EstimateStructureValue(config, source, comparer) - EstimateStructureValue(config, remaining, comparer);
            int pointCost = GetCardPoints(card) * 4;
            int trumpCost = config.IsTrump(card) ? 12 : 0;
            int valueCost = GetCardValue(card, config) / 40;
            return structureLoss * 10 + pointCost + trumpCost + valueCost;
        }

        public static int CountHighControlCards(GameConfig config, IEnumerable<Card> cards)
        {
            return cards.Count(card => GetCardValue(card, config) >= 700);
        }

        public static string BuildReadableCandidate(IEnumerable<Card> cards)
        {
            return string.Join(" ", cards.Select(card => card.ToString()));
        }

        public static long EstimateCombinationCount(int n, int k)
        {
            if (k < 0 || k > n) return 0;
            if (k == 0 || k == n) return 1;
            k = Math.Min(k, n - k);
            long result = 1;
            for (int i = 1; i <= k; i++)
                result = result * (n - k + i) / i;
            return result;
        }

        private static IEnumerable<List<T>> Combinations<T>(List<T> items, int choose)
        {
            var buffer = new List<T>();
            foreach (var combo in CombinationsCore(items, choose, 0, buffer))
                yield return combo;
        }

        private static IEnumerable<List<T>> CombinationsCore<T>(List<T> items, int choose, int start, List<T> buffer)
        {
            if (buffer.Count == choose)
            {
                yield return new List<T>(buffer);
                yield break;
            }

            int needed = choose - buffer.Count;
            for (int i = start; i <= items.Count - needed; i++)
            {
                buffer.Add(items[i]);
                foreach (var combo in CombinationsCore(items, choose, i + 1, buffer))
                    yield return combo;
                buffer.RemoveAt(buffer.Count - 1);
            }
        }
    }
}
