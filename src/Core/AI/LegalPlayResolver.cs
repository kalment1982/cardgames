using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// 为 AI 提供最终执行层的合法出牌兜底。
    /// 规则上总存在可出的牌，因此当策略层返回非法牌时，
    /// 这里负责枚举出一手真正可提交给 Game.PlayCardsEx 的牌。
    /// </summary>
    public static class LegalPlayResolver
    {
        public static bool TryResolve(Game game, int playerIndex, GameConfig config, out List<Card> cards)
        {
            cards = new List<Card>();
            if (game == null || config == null)
                return false;

            var hand = new List<Card>(game.State.PlayerHands[playerIndex]);
            if (hand.Count == 0)
                return false;

            if (game.CurrentTrick.Count == 0)
                return TryResolveLead(hand, config, out cards);

            return TryResolveFollow(hand, game.CurrentTrick[0].Cards, config, out cards);
        }

        private static bool TryResolveLead(List<Card> hand, GameConfig config, out List<Card> cards)
        {
            cards = new List<Card>();
            var validator = new PlayValidator(config);
            var comparer = new CardComparer(config);

            foreach (var card in hand.OrderBy(card => card, comparer))
            {
                var trial = new List<Card> { card };
                if (validator.IsValidPlay(hand, trial))
                {
                    cards = trial;
                    return true;
                }
            }

            return false;
        }

        private static bool TryResolveFollow(
            List<Card> hand,
            List<Card> leadCards,
            GameConfig config,
            out List<Card> cards)
        {
            cards = new List<Card>();
            var validator = new FollowValidator(config);
            var comparer = new CardComparer(config);
            int need = leadCards.Count;
            if (hand.Count < need || need <= 0)
                return false;

            var leadCategory = config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategory = hand
                .Where(card => MatchesLeadCategory(card, leadSuit, leadCategory, config))
                .ToList();

            if (sameCategory.Count >= need &&
                TryFindGroupedLegalCandidate(hand, leadCards, sameCategory, comparer, validator, out cards))
            {
                return true;
            }

            if (TryFindGroupedLegalCandidate(hand, leadCards, hand, comparer, validator, out cards))
                return true;

            var ordered = hand
                .OrderBy(card => EstimateDiscardCost(hand, card, comparer, config))
                .ThenBy(card => card, comparer)
                .ToList();

            foreach (var combo in Combinations(ordered, need))
            {
                if (!validator.IsValidFollow(hand, leadCards, combo))
                    continue;

                cards = combo;
                return true;
            }

            return false;
        }

        private static bool TryFindGroupedLegalCandidate(
            List<Card> hand,
            List<Card> leadCards,
            List<Card> searchPool,
            CardComparer comparer,
            FollowValidator validator,
            out List<Card> cards)
        {
            cards = new List<Card>();
            int need = leadCards.Count;
            if (searchPool.Count < need)
                return false;

            var groups = searchPool
                .GroupBy(card => card)
                .Select(group => new CardGroup(group.Key, group.Count()))
                .OrderByDescending(group => group.Count)
                .ThenBy(group => EstimateDiscardCost(searchPool, group.Card, comparer, null))
                .ThenBy(group => group.Card, comparer)
                .ToList();

            var remainingCounts = new int[groups.Count];
            for (int i = groups.Count - 1; i >= 0; i--)
                remainingCounts[i] = groups[i].Count + (i + 1 < groups.Count ? remainingCounts[i + 1] : 0);

            var candidate = new List<Card>(need);
            return Search(
                hand,
                leadCards,
                validator,
                groups,
                remainingCounts,
                0,
                need,
                candidate,
                out cards);
        }

        private static bool Search(
            List<Card> hand,
            List<Card> leadCards,
            FollowValidator validator,
            List<CardGroup> groups,
            int[] remainingCounts,
            int index,
            int remainingToPick,
            List<Card> candidate,
            out List<Card> cards)
        {
            cards = new List<Card>();
            if (remainingToPick == 0)
            {
                if (!validator.IsValidFollow(hand, leadCards, candidate))
                    return false;

                cards = new List<Card>(candidate);
                return true;
            }

            if (index >= groups.Count || remainingCounts[index] < remainingToPick)
                return false;

            var group = groups[index];
            int maxTake = System.Math.Min(group.Count, remainingToPick);
            for (int take = maxTake; take >= 0; take--)
            {
                for (int i = 0; i < take; i++)
                    candidate.Add(group.Card);

                if (Search(
                    hand,
                    leadCards,
                    validator,
                    groups,
                    remainingCounts,
                    index + 1,
                    remainingToPick - take,
                    candidate,
                    out cards))
                {
                    return true;
                }

                for (int i = 0; i < take; i++)
                    candidate.RemoveAt(candidate.Count - 1);
            }

            return false;
        }

        private static bool MatchesLeadCategory(Card card, Suit leadSuit, CardCategory leadCategory, GameConfig config)
        {
            if (leadCategory == CardCategory.Trump)
                return config.IsTrump(card);

            return !config.IsTrump(card) && card.Suit == leadSuit;
        }

        private static int EstimateDiscardCost(List<Card> cards, Card card, CardComparer comparer, GameConfig? config)
        {
            int sameCount = cards.Count(existing => existing.Equals(card));
            int points = card.Score;
            int trumpPenalty = config != null && config.IsTrump(card) ? 10 : 0;
            return sameCount * 100 + points * 10 + trumpPenalty - RankWeight(card, comparer);
        }

        private static int RankWeight(Card card, CardComparer comparer)
        {
            var lighter = new Card(card.Suit, Rank.Three);
            return comparer.Compare(card, lighter);
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

        private sealed class CardGroup
        {
            public CardGroup(Card card, int count)
            {
                Card = card;
                Count = count;
            }

            public Card Card { get; }
            public int Count { get; }
        }
    }
}
