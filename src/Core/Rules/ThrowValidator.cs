using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Logging;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 甩牌验证器
    /// </summary>
    public class ThrowValidator
    {
        private readonly GameConfig _config;

        public ThrowValidator(GameConfig config)
        {
            _config = config;
        }

        /// <summary>
        /// 判断甩牌是否成功
        /// </summary>
        public bool IsThrowSuccessful(List<Card> throwCards, List<List<Card>> followPlays)
        {
            return AnalyzeThrow(throwCards, followPlays).Success;
        }

        /// <summary>
        /// 分析甩牌是否被拦截；失败时返回可用于日志的 detail。
        /// </summary>
        public ThrowCheckResult AnalyzeThrow(List<Card> throwCards, List<List<Card>> followPlays)
        {
            if (throwCards == null || throwCards.Count == 0)
                return ThrowCheckResult.Fail();
            if (!IsSameSuitOrTrump(throwCards))
                return ThrowCheckResult.Fail();

            if (followPlays == null || followPlays.Count == 0)
                return ThrowCheckResult.Ok();

            var throwSuit = GetSuitOrCategory(throwCards[0]);

            // 检查所有跟牌，命中任一子结构拦截即失败
            for (var i = 0; i < followPlays.Count; i++)
            {
                var follow = followPlays[i];
                if (follow == null || follow.Count == 0)
                    continue;

                var sameSuitCards = follow.Where(c => GetSuitOrCategory(c) == throwSuit).ToList();
                if (sameSuitCards.Count == 0)
                    continue;

                if (CanBeatThrow(sameSuitCards, throwCards, out var blockDetail))
                {
                    var detail = blockDetail ?? new Dictionary<string, object?>();
                    detail["throw_suit_category"] = throwSuit;
                    detail["follower_hand_index"] = i;
                    return ThrowCheckResult.Fail(detail);
                }
            }

            return ThrowCheckResult.Ok();
        }

        /// <summary>
        /// 检查同花手牌是否能在任一子结构上拦截甩牌
        /// </summary>
        public bool CanBeatThrow(List<Card> sameSuitCards, List<Card> throwCards)
        {
            return CanBeatThrow(sameSuitCards, throwCards, out _);
        }

        private bool CanBeatThrow(List<Card> sameSuitCards, List<Card> throwCards, out Dictionary<string, object?>? blockDetail)
        {
            blockDetail = null;

            if (sameSuitCards == null || sameSuitCards.Count == 0)
                return false;
            if (throwCards == null || throwCards.Count == 0)
                return false;
            if (!IsSameSuitOrTrump(throwCards))
                return false;

            var throwComponents = DecomposeThrowComponents(throwCards);
            if (throwComponents.Count == 0)
                return false;

            // v1.4+：逐子结构判定。
            // 只要任一子结构（单牌/对子/拖拉机）可被同门同牌型更大结构拦截，即判可挡。
            foreach (var component in throwComponents)
            {
                if (!CanBeatComponent(sameSuitCards, component, out var blockerCards))
                    continue;

                blockDetail = new Dictionary<string, object?>
                {
                    ["blocked_component_type"] = component.Type.ToString(),
                    ["blocked_component_cards"] = component.Cards.Select(c => c.ToString()).ToArray(),
                    ["blocked_component_top"] = component.HighestCard.ToString(),
                    ["blocking_cards"] = blockerCards.Select(c => c.ToString()).ToArray()
                };
                return true;
            }

            return false;
        }

        /// <summary>
        /// 将甩牌按规则拆解：拖拉机优先 -> 对子 -> 单牌
        /// </summary>
        public List<List<Card>> DecomposeThrow(List<Card> throwCards)
        {
            return DecomposeThrowComponents(throwCards).Select(component => component.Cards).ToList();
        }

        /// <summary>
        /// 甩牌失败后的回退出牌：
        /// 单牌 > 对子 > 拖拉机，且在选中牌型内出最小牌（或最小组合）
        /// </summary>
        public List<Card> GetFallbackPlay(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return new List<Card>();
            if (!IsSameSuitOrTrump(cards))
                return new List<Card>();

            var comparer = new CardComparer(_config);
            var components = DecomposeThrowComponents(cards);

            var single = components
                .Where(component => component.Type == ThrowComponentType.Single)
                .OrderBy(component => component.HighestCard, comparer)
                .FirstOrDefault();
            if (single != null)
                return TakeFromOriginal(cards, single.Cards).OrderBy(card => card, comparer).ToList();

            var pair = components
                .Where(component => component.Type == ThrowComponentType.Pair)
                .OrderBy(component => component.HighestCard, comparer)
                .FirstOrDefault();
            if (pair != null)
                return TakeFromOriginal(cards, pair.Cards).OrderBy(card => card, comparer).ToList();

            var tractor = components
                .Where(component => component.Type == ThrowComponentType.Tractor)
                .OrderBy(component => component.PairCount)
                .ThenBy(component => component.HighestCard, comparer)
                .FirstOrDefault();

            if (tractor != null)
                return TakeFromOriginal(cards, tractor.Cards).OrderBy(card => card, comparer).ToList();

            return new List<Card>();
        }

        /// <summary>
        /// 兼容旧接口：返回回退牌型里的最小单张
        /// </summary>
        public Card? GetSmallestCard(List<Card> cards)
        {
            var fallbackPlay = GetFallbackPlay(cards);
            if (fallbackPlay.Count == 0)
                return null;

            var comparer = new CardComparer(_config);
            return fallbackPlay.OrderBy(card => card, comparer).First();
        }

        private bool CanBeatComponent(List<Card> sameSuitCards, ThrowComponent component, out List<Card> blockerCards)
        {
            blockerCards = new List<Card>();
            var comparer = new CardComparer(_config);

            if (component.Type == ThrowComponentType.Single)
            {
                var bestSingle = sameSuitCards.OrderByDescending(card => card, comparer).First();
                if (comparer.Compare(bestSingle, component.HighestCard) > 0)
                {
                    blockerCards.Add(bestSingle);
                    return true;
                }
                return false;
            }

            if (component.Type == ThrowComponentType.Pair)
            {
                var pairRepresentatives = GetPairRepresentatives(sameSuitCards);
                var beatPair = pairRepresentatives
                    .Where(pair => comparer.Compare(pair, component.HighestCard) > 0)
                    .OrderBy(pair => pair, comparer)
                    .FirstOrDefault();
                if (beatPair != null)
                {
                    blockerCards.Add(beatPair);
                    blockerCards.Add(beatPair);
                    return true;
                }
                return false;
            }

            // 拖拉机：仅同长度拖拉机可拦截
            var followerTractors = FindAllTractors(GetPairRepresentatives(sameSuitCards));
            var beatTractor = followerTractors
                .Where(tractor => tractor.PairCount == component.PairCount &&
                                  comparer.Compare(tractor.HighestCard, component.HighestCard) > 0)
                .OrderBy(tractor => tractor.HighestCard, comparer)
                .FirstOrDefault();
            if (beatTractor != null)
            {
                blockerCards.AddRange(beatTractor.Cards);
                return true;
            }

            return false;
        }

        private List<ThrowComponent> DecomposeThrowComponents(List<Card> cards)
        {
            var components = new List<ThrowComponent>();
            if (cards == null || cards.Count == 0 || !IsSameSuitOrTrump(cards))
                return components;

            var comparer = new CardComparer(_config);
            var pairRepresentatives = new List<Card>();
            var singles = new List<Card>();

            // 先拆对子单元与单张
            var groups = cards
                .GroupBy(card => card)
                .Select(group => new { Card = group.Key, Count = group.Count() })
                .ToList();

            foreach (var group in groups)
            {
                int pairCount = group.Count / 2;
                for (int i = 0; i < pairCount; i++)
                    pairRepresentatives.Add(group.Card);

                if (group.Count % 2 == 1)
                    singles.Add(group.Card);
            }

            // 拖拉机优先：每次取“最长且最大”的拖拉机并移除，再继续识别
            while (true)
            {
                var tractors = FindAllTractors(pairRepresentatives);
                if (tractors.Count == 0)
                    break;

                var best = tractors
                    .OrderByDescending(tractor => tractor.PairCount)
                    .ThenByDescending(tractor => tractor.HighestCard, comparer)
                    .First();

                components.Add(best);
                RemovePairRepresentatives(pairRepresentatives, best.Cards);
            }

            // 剩余对子
            foreach (var pair in pairRepresentatives)
            {
                components.Add(new ThrowComponent(
                    ThrowComponentType.Pair,
                    new List<Card> { pair, pair },
                    1,
                    pair));
            }

            // 单张
            foreach (var single in singles)
            {
                components.Add(new ThrowComponent(
                    ThrowComponentType.Single,
                    new List<Card> { single },
                    0,
                    single));
            }

            return components;
        }

        private List<Card> TakeFromOriginal(List<Card> originalCards, List<Card> patternCards)
        {
            var pool = new List<Card>(originalCards);
            var selected = new List<Card>();

            foreach (var patternCard in patternCards)
            {
                var found = pool.FirstOrDefault(card => card.Equals(patternCard));
                if (found == null)
                    return new List<Card>();

                selected.Add(found);
                pool.Remove(found);
            }

            return selected;
        }

        private void RemovePairRepresentatives(List<Card> sourcePairs, List<Card> usedCards)
        {
            var toRemove = usedCards.GroupBy(card => card).ToDictionary(group => group.Key, group => group.Count() / 2);

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

        private List<ThrowComponent> FindAllTractors(List<Card> pairRepresentatives)
        {
            var tractors = new List<ThrowComponent>();
            if (pairRepresentatives.Count < 2)
                return tractors;

            var comparer = new CardComparer(_config);
            int count = pairRepresentatives.Count;
            int maskLimit = 1 << count;

            for (int mask = 0; mask < maskLimit; mask++)
            {
                int pairCount = CountBits(mask);
                if (pairCount < 2)
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

                var sortedPairs = candidatePairs.OrderBy(c => c, comparer).ToList();
                tractors.Add(new ThrowComponent(
                    ThrowComponentType.Tractor,
                    candidateCards,
                    pairCount,
                    sortedPairs[^1]));
            }

            return tractors;
        }

        private List<Card> GetPairRepresentatives(List<Card> cards)
        {
            var comparer = new CardComparer(_config);
            var pairs = new List<Card>();

            var groups = cards
                .GroupBy(card => card)
                .Select(group => new { Card = group.Key, Count = group.Count() });

            foreach (var group in groups)
            {
                int pairCount = group.Count / 2;
                for (int i = 0; i < pairCount; i++)
                {
                    pairs.Add(group.Card);
                }
            }

            return pairs.OrderByDescending(c => c, comparer).ToList();
        }

        private static int CountBits(int value)
        {
            int count = 0;
            while (value != 0)
            {
                value &= value - 1;
                count++;
            }

            return count;
        }

        private bool IsSameSuitOrTrump(List<Card> cards)
        {
            bool allTrump = cards.All(c => _config.IsTrump(c));
            if (allTrump) return true;

            bool allSuit = cards.All(c => !_config.IsTrump(c));
            if (!allSuit) return false;

            var firstSuit = cards[0].Suit;
            return cards.All(c => c.Suit == firstSuit);
        }

        private string GetSuitOrCategory(Card card)
        {
            if (_config.IsTrump(card))
                return "Trump";
            return card.Suit.ToString();
        }

        private enum ThrowComponentType
        {
            Tractor,
            Pair,
            Single
        }

        private sealed class ThrowComponent
        {
            public ThrowComponentType Type { get; }
            public List<Card> Cards { get; }
            public int PairCount { get; }
            public Card HighestCard { get; }

            public ThrowComponent(
                ThrowComponentType type,
                List<Card> cards,
                int pairCount,
                Card highestCard)
            {
                Type = type;
                Cards = cards;
                PairCount = pairCount;
                HighestCard = highestCard;
            }
        }

        public sealed class ThrowCheckResult
        {
            public bool Success { get; }
            public Dictionary<string, object?>? Detail { get; }

            private ThrowCheckResult(bool success, Dictionary<string, object?>? detail = null)
            {
                Success = success;
                Detail = detail;
            }

            public static ThrowCheckResult Ok() => new(true);

            public static ThrowCheckResult Fail(Dictionary<string, object?>? detail = null) => new(false, detail);
        }
    }
}
