using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.Rules
{
    /// <summary>
    /// 一墩出牌记录
    /// </summary>
    public class TrickPlay
    {
        public int PlayerIndex { get; set; }
        public List<Card> Cards { get; set; }

        public TrickPlay(int playerIndex, List<Card> cards)
        {
            PlayerIndex = playerIndex;
            Cards = cards;
        }
    }

    /// <summary>
    /// 胜负判定器
    /// </summary>
    public class TrickJudge
    {
        private readonly GameConfig _config;
        private readonly CardComparer _comparer;

        public TrickJudge(GameConfig config)
        {
            _config = config;
            _comparer = new CardComparer(config);
        }

        /// <summary>
        /// 判定一墩的获胜者
        /// </summary>
        public int DetermineWinner(List<TrickPlay> plays)
        {
            if (plays == null || plays.Count == 0)
                return -1;

            var leadPlay = plays[0];
            var leadCategory = _config.GetCardCategory(leadPlay.Cards[0]);
            var leadSuit = leadPlay.Cards[0].Suit;
            var leadComponents = DecomposeLeadComponents(leadPlay.Cards);

            int winnerIndex = 0;
            var winnerProfile = BuildComparisonProfile(leadPlay.Cards, leadSuit, leadCategory, leadComponents);

            for (int i = 1; i < plays.Count; i++)
            {
                var currentPlay = plays[i];

                // 比较当前出牌和当前获胜者
                var currentProfile = BuildComparisonProfile(currentPlay.Cards, leadSuit, leadCategory, leadComponents);
                if (CompareProfiles(currentProfile, winnerProfile) > 0)
                {
                    winnerIndex = i;
                    winnerProfile = currentProfile;
                }
            }

            return plays[winnerIndex].PlayerIndex;
        }

        private int GetPatternPriority(PatternType type)
        {
            return type switch
            {
                PatternType.Tractor => 3,
                PatternType.Pair => 2,
                PatternType.Single => 1,
                PatternType.Mixed => 0,
                _ => 0
            };
        }

        private ComparisonProfile BuildComparisonProfile(
            List<Card> cards,
            Suit leadSuit,
            CardCategory leadCategory,
            List<LeadComponent> leadComponents)
        {
            var leadOrigin = leadCategory == CardCategory.Trump ? ComponentOrigin.Trump : ComponentOrigin.Suit;
            var leadSystemCards = cards.Where(card => MatchesLeadSystem(card, leadSuit, leadCategory)).ToList();
            var suitOrTrumpComponents = DecomposeComponents(leadSystemCards, leadOrigin);
            var trumpComponents = leadCategory == CardCategory.Trump
                ? new List<ComparableComponent>()
                : DecomposeComponents(cards.Where(_config.IsTrump).ToList(), ComponentOrigin.Trump);

            var usedLeadSystem = new bool[suitOrTrumpComponents.Count];
            var usedTrump = new bool[trumpComponents.Count];
            var matched = new List<MatchedComponent>(leadComponents.Count);

            foreach (var leadComponent in leadComponents)
            {
                int leadSystemIndex = FindBestMatchIndex(suitOrTrumpComponents, usedLeadSystem, leadComponent);
                int trumpIndex = FindBestMatchIndex(trumpComponents, usedTrump, leadComponent);

                bool useTrump = trumpIndex >= 0 && IsTrumpCandidateStronger(
                    trumpComponents[trumpIndex],
                    leadSystemIndex >= 0 ? suitOrTrumpComponents[leadSystemIndex] : null);

                if (useTrump)
                {
                    usedTrump[trumpIndex] = true;
                    matched.Add(new MatchedComponent(
                        ComponentOrigin.Trump,
                        trumpComponents[trumpIndex].HighestCard));
                }
                else if (leadSystemIndex >= 0)
                {
                    usedLeadSystem[leadSystemIndex] = true;
                    matched.Add(new MatchedComponent(
                        suitOrTrumpComponents[leadSystemIndex].Origin,
                        suitOrTrumpComponents[leadSystemIndex].HighestCard));
                }
                else
                {
                    matched.Add(MatchedComponent.None);
                }
            }

            return new ComparisonProfile(
                matched.All(component => component.Origin != ComponentOrigin.None),
                matched);
        }

        private bool MatchesLeadSystem(Card card, Suit leadSuit, CardCategory leadCategory)
        {
            if (leadCategory == CardCategory.Trump)
                return _config.IsTrump(card);

            return !_config.IsTrump(card) && card.Suit == leadSuit;
        }

        private List<LeadComponent> DecomposeLeadComponents(List<Card> cards)
        {
            return DecomposeComponents(cards, ComponentOrigin.Suit)
                .Select(component => new LeadComponent(
                    component.Type,
                    component.PairCount,
                    component.HighestCard))
                .ToList();
        }

        private List<ComparableComponent> DecomposeComponents(List<Card> cards, ComponentOrigin origin)
        {
            var components = new List<ComparableComponent>();
            if (cards == null || cards.Count == 0)
                return components;

            var pairRepresentatives = new List<Card>();
            var singles = new List<Card>();

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

            while (true)
            {
                var tractors = FindAllTractors(pairRepresentatives, origin);
                if (tractors.Count == 0)
                    break;

                var best = tractors
                    .OrderByDescending(component => component.PairCount)
                    .ThenByDescending(component => component.HighestCard, _comparer)
                    .First();

                components.Add(best);
                RemovePairRepresentatives(pairRepresentatives, best.Cards);
            }

            foreach (var pair in pairRepresentatives)
            {
                components.Add(new ComparableComponent(
                    PatternType.Pair,
                    1,
                    pair,
                    origin,
                    new List<Card> { pair, pair }));
            }

            foreach (var single in singles.OrderByDescending(card => card, _comparer))
            {
                components.Add(new ComparableComponent(
                    PatternType.Single,
                    0,
                    single,
                    origin,
                    new List<Card> { single }));
            }

            return components
                .OrderByDescending(component => GetPatternPriority(component.Type))
                .ThenByDescending(component => component.HighestCard, _comparer)
                .ToList();
        }

        private List<ComparableComponent> FindAllTractors(List<Card> pairRepresentatives, ComponentOrigin origin)
        {
            var tractors = new List<ComparableComponent>();
            if (pairRepresentatives.Count < 2)
                return tractors;

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

                var sortedPairs = candidatePairs.OrderBy(card => card, _comparer).ToList();
                tractors.Add(new ComparableComponent(
                    PatternType.Tractor,
                    pairCount,
                    sortedPairs[^1],
                    origin,
                    candidateCards));
            }

            return tractors;
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

        private int FindBestMatchIndex(
            List<ComparableComponent> candidates,
            bool[] used,
            LeadComponent leadComponent)
        {
            int bestIndex = -1;

            for (int i = 0; i < candidates.Count; i++)
            {
                if (used[i] || !IsCompatible(candidates[i], leadComponent))
                    continue;

                if (bestIndex < 0 ||
                    CompareComponentStrength(candidates[i], candidates[bestIndex]) > 0)
                {
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private bool IsCompatible(ComparableComponent candidate, LeadComponent leadComponent)
        {
            return candidate.Type == leadComponent.Type
                && candidate.PairCount == leadComponent.PairCount;
        }

        private bool IsTrumpCandidateStronger(ComparableComponent trumpCandidate, ComparableComponent? leadSystemCandidate)
        {
            if (leadSystemCandidate == null)
                return true;

            return CompareComponentStrength(trumpCandidate, leadSystemCandidate) > 0;
        }

        private int CompareProfiles(ComparisonProfile left, ComparisonProfile right)
        {
            if (left.IsComplete != right.IsComplete)
                return left.IsComplete ? 1 : -1;

            for (int i = 0; i < left.Components.Count; i++)
            {
                int componentCompare = CompareMatchedComponents(left.Components[i], right.Components[i]);
                if (componentCompare != 0)
                    return componentCompare;
            }

            return 0;
        }

        private int CompareMatchedComponents(MatchedComponent left, MatchedComponent right)
        {
            if (left.Origin != right.Origin)
                return left.Origin.CompareTo(right.Origin);

            if (left.Origin == ComponentOrigin.None || left.HighestCard == null || right.HighestCard == null)
                return 0;

            return _comparer.Compare(left.HighestCard, right.HighestCard);
        }

        private int CompareComponentStrength(ComparableComponent left, ComparableComponent right)
        {
            if (left.Origin != right.Origin)
                return left.Origin.CompareTo(right.Origin);

            return _comparer.Compare(left.HighestCard, right.HighestCard);
        }

        private enum ComponentOrigin
        {
            None = 0,
            Suit = 1,
            Trump = 2
        }

        private sealed class LeadComponent
        {
            public PatternType Type { get; }
            public int PairCount { get; }
            public Card HighestCard { get; }

            public LeadComponent(PatternType type, int pairCount, Card highestCard)
            {
                Type = type;
                PairCount = pairCount;
                HighestCard = highestCard;
            }
        }

        private sealed class ComparableComponent
        {
            public PatternType Type { get; }
            public int PairCount { get; }
            public Card HighestCard { get; }
            public ComponentOrigin Origin { get; }
            public List<Card> Cards { get; }

            public ComparableComponent(
                PatternType type,
                int pairCount,
                Card highestCard,
                ComponentOrigin origin,
                List<Card> cards)
            {
                Type = type;
                PairCount = pairCount;
                HighestCard = highestCard;
                Origin = origin;
                Cards = cards;
            }
        }

        private sealed class MatchedComponent
        {
            public static MatchedComponent None { get; } = new(ComponentOrigin.None, null);

            public ComponentOrigin Origin { get; }
            public Card? HighestCard { get; }

            public MatchedComponent(ComponentOrigin origin, Card? highestCard)
            {
                Origin = origin;
                HighestCard = highestCard;
            }
        }

        private sealed class ComparisonProfile
        {
            public bool IsComplete { get; }
            public List<MatchedComponent> Components { get; }

            public ComparisonProfile(bool isComplete, List<MatchedComponent> components)
            {
                IsComplete = isComplete;
                Components = components;
            }
        }
    }
}
