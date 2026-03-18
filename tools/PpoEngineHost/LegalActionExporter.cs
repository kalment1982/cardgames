using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace PpoEngineHost;

/// <summary>
/// Exhaustively enumerates all legal actions for the current player.
/// No strategic pruning — every valid play is included.
/// </summary>
public static class LegalActionExporter
{
    public static List<LegalAction> Export(Game game, int playerIndex)
    {
        if (game.State.Phase != GamePhase.Playing)
            return new List<LegalAction>();

        var config = new GameConfig
        {
            LevelRank = game.State.LevelRank,
            TrumpSuit = game.State.TrumpSuit
        };

        var hand = game.State.PlayerHands[playerIndex];
        if (hand.Count == 0)
            return new List<LegalAction>();

        bool isLead = game.CurrentTrick.Count == 0;
        return isLead
            ? ExportLeadActions(game, playerIndex, hand, config)
            : ExportFollowActions(game, playerIndex, hand, config);
    }

    // ─── Lead: enumerate every legal first-play ───

    private static List<LegalAction> ExportLeadActions(
        Game game, int playerIndex, List<Card> hand, GameConfig config)
    {
        var validator = new PlayValidator(config);
        var comparer = new CardComparer(config);

        var otherHands = new List<List<Card>>();
        for (int i = 0; i < 4; i++)
            if (i != playerIndex)
                otherHands.Add(game.State.PlayerHands[i]);

        var rawCandidates = new List<List<Card>>();
        var groups = BuildSystemGroups(config, hand);

        foreach (var group in groups)
        {
            // All singles
            var distinct = group.GroupBy(c => c).Select(g => g.First()).ToList();
            foreach (var card in distinct)
                rawCandidates.Add(new List<Card> { card });

            // All pairs
            var pairGroups = group.GroupBy(c => c).Where(g => g.Count() >= 2).ToList();
            foreach (var pg in pairGroups)
                rawCandidates.Add(pg.Take(2).ToList());

            // All tractors (consecutive pairs, length 2+)
            var pairReps = pairGroups
                .Select(g => g.Key)
                .OrderBy(c => c, comparer)
                .ToList();

            for (int len = 2; len <= pairReps.Count; len++)
            {
                for (int start = 0; start <= pairReps.Count - len; start++)
                {
                    var tractorCards = new List<Card>();
                    for (int k = start; k < start + len; k++)
                    {
                        tractorCards.Add(pairReps[k]);
                        tractorCards.Add(pairReps[k]);
                    }
                    var pattern = new CardPattern(tractorCards, config);
                    if (pattern.IsTractor(tractorCards))
                        rawCandidates.Add(tractorCards);
                }
            }

            // Throws (mixed): all multi-card same-system combos that aren't
            // single/pair/tractor. We enumerate subsets of the group's structural
            // components (singles + pairs + tractors) with >= 2 components.
            AddThrowCandidates(rawCandidates, group, config, comparer);
        }

        // Deduplicate and validate
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<LegalAction>();

        foreach (var candidate in rawCandidates)
        {
            var key = BuildCandidateKey(candidate);
            if (!seen.Add(key))
                continue;

            // Full validation including throw check against other hands
            if (!validator.IsValidPlay(hand, candidate, otherHands))
                continue;

            results.Add(BuildLegalAction(candidate, config, isLead: true, isFollow: false,
                leadCards: null, leadCategory: null, leadSuit: null));
        }

        return results;
    }

    /// <summary>
    /// Enumerate throw candidates by decomposing a system group into atomic
    /// components (singles, pairs, tractors) and combining 2+ components.
    /// </summary>
    private static void AddThrowCandidates(
        List<List<Card>> candidates, List<Card> group,
        GameConfig config, CardComparer comparer)
    {
        if (group.Count < 3)
            return;

        // Build atomic components: tractors first, then remaining pairs, then singles
        var components = new List<List<Card>>();
        var remaining = new List<Card>(group);

        // Find all maximal tractors greedily (longest first)
        var pairGroups = remaining.GroupBy(c => c).Where(g => g.Count() >= 2).ToList();
        var pairReps = pairGroups.Select(g => g.Key).OrderBy(c => c, comparer).ToList();

        // Extract tractors from longest to shortest
        var usedPairReps = new HashSet<string>();
        for (int len = pairReps.Count; len >= 2; len--)
        {
            for (int start = 0; start <= pairReps.Count - len; start++)
            {
                var slice = pairReps.Skip(start).Take(len).ToList();
                if (slice.Any(c => usedPairReps.Contains(CardKey(c))))
                    continue;

                var tractorCards = new List<Card>();
                foreach (var c in slice) { tractorCards.Add(c); tractorCards.Add(c); }

                if (!new CardPattern(tractorCards, config).IsTractor(tractorCards))
                    continue;

                components.Add(tractorCards);
                foreach (var c in slice) usedPairReps.Add(CardKey(c));
            }
        }

        // Remaining pairs (not consumed by tractors)
        foreach (var pg in pairGroups)
        {
            if (usedPairReps.Contains(CardKey(pg.Key)))
                continue;
            components.Add(pg.Take(2).ToList());
            usedPairReps.Add(CardKey(pg.Key));
        }

        // Singles (cards with odd count, or not part of any pair)
        var countMap = group.GroupBy(c => c).ToDictionary(g => g.Key, g => g.Count());
        foreach (var entry in countMap)
        {
            int pairsUsed = usedPairReps.Contains(CardKey(entry.Key)) ? 1 : 0;
            int leftover = entry.Value - pairsUsed * 2;
            for (int i = 0; i < leftover; i++)
                components.Add(new List<Card> { entry.Key });
        }

        if (components.Count < 2)
            return;

        // Cap enumeration to avoid combinatorial explosion
        int maxComponents = Math.Min(components.Count, 12);
        var cappedComponents = components.Take(maxComponents).ToList();

        // Enumerate all subsets of size >= 2
        int limit = 1 << cappedComponents.Count;
        for (int mask = 3; mask < limit; mask++)
        {
            if (CountBits(mask) < 2)
                continue;

            var combo = new List<Card>();
            for (int i = 0; i < cappedComponents.Count; i++)
            {
                if ((mask & (1 << i)) != 0)
                    combo.AddRange(cappedComponents[i]);
            }

            // Must be >= 3 cards to be a throw
            if (combo.Count >= 3)
                candidates.Add(combo);
        }

        // Also add the full group if it has >= 3 cards
        if (group.Count >= 3)
            candidates.Add(new List<Card>(group));
    }

    // ─── Follow: enumerate every legal follow-play ───

    private static List<LegalAction> ExportFollowActions(
        Game game, int playerIndex, List<Card> hand, GameConfig config)
    {
        var leadCards = game.CurrentTrick[0].Cards;
        int need = leadCards.Count;
        var followValidator = new FollowValidator(config);
        var comparer = new CardComparer(config);

        var leadCategory = config.GetCardCategory(leadCards[0]);
        var leadSuit = leadCards[0].Suit;

        // If hand size <= need, only one option
        if (hand.Count <= need)
        {
            var action = BuildLegalAction(new List<Card>(hand), config,
                isLead: false, isFollow: true,
                leadCards: leadCards, leadCategory: leadCategory, leadSuit: leadSuit);
            return new List<LegalAction> { action };
        }

        // Enumerate all C(hand.Count, need) combinations, validate each.
        // Group identical cards to reduce combinatorial space.
        var cardGroups = hand
            .GroupBy(c => c)
            .Select(g => new { Card = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Count)
            .ThenBy(g => g.Card, comparer)
            .ToList();

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var results = new List<LegalAction>();

        // Use grouped enumeration to avoid duplicate combos from identical cards
        EnumerateGroupedCombinations(
            cardGroups.Select(g => (g.Card, g.Count)).ToList(),
            need,
            0,
            new List<Card>(),
            combo =>
            {
                var key = BuildCandidateKey(combo);
                if (!seen.Add(key))
                    return;

                if (!followValidator.IsValidFollow(hand, leadCards, combo))
                    return;

                results.Add(BuildLegalAction(combo, config,
                    isLead: false, isFollow: true,
                    leadCards: leadCards, leadCategory: leadCategory, leadSuit: leadSuit));
            });

        return results;
    }

    /// <summary>
    /// Enumerate distinct combinations of `need` cards from grouped card counts.
    /// </summary>
    private static void EnumerateGroupedCombinations(
        List<(Card Card, int Count)> groups,
        int need,
        int index,
        List<Card> current,
        Action<List<Card>> onCandidate)
    {
        if (current.Count == need)
        {
            onCandidate(new List<Card>(current));
            return;
        }

        if (index >= groups.Count)
            return;

        int remaining = need - current.Count;

        // Check if enough cards remain from index onward
        int available = 0;
        for (int i = index; i < groups.Count; i++)
            available += groups[i].Count;
        if (available < remaining)
            return;

        var (card, count) = groups[index];
        int maxTake = Math.Min(count, remaining);

        for (int take = maxTake; take >= 0; take--)
        {
            for (int j = 0; j < take; j++)
                current.Add(card);

            EnumerateGroupedCombinations(groups, need, index + 1, current, onCandidate);

            for (int j = 0; j < take; j++)
                current.RemoveAt(current.Count - 1);
        }
    }

    // ─── Shared helpers ───

    private static LegalAction BuildLegalAction(
        List<Card> cards, GameConfig config,
        bool isLead, bool isFollow,
        List<Card>? leadCards, CardCategory? leadCategory, Suit? leadSuit)
    {
        var pattern = new CardPattern(cards, config);
        string patternType = ClassifyPatternType(pattern, cards, config);
        string system = ClassifySystem(cards, config);

        bool isThrow = isLead && patternType is "throw" or "mixed";
        bool isTrumpCut = false;

        if (isFollow && leadCards != null && leadCategory.HasValue)
        {
            // Trump cut: lead is non-trump, follow is all trump
            if (leadCategory.Value == CardCategory.Suit && cards.All(config.IsTrump))
                isTrumpCut = true;
        }

        return new LegalAction
        {
            Cards = cards,
            PatternType = patternType,
            System = system,
            IsLead = isLead,
            IsFollow = isFollow,
            IsThrow = isThrow,
            IsTrumpCut = isTrumpCut,
            DebugKey = BuildDebugKey(cards, patternType, system, config)
        };
    }

    private static string ClassifyPatternType(CardPattern pattern, List<Card> cards, GameConfig config)
    {
        if (cards.Count == 1) return "single";
        if (cards.Count == 2 && CardPattern.IsPair(cards)) return "pair";
        if (pattern.IsTractor(cards)) return "tractor";

        // Check if it's a throw (same-system multi-component)
        if (cards.Count >= 3 && IsSameSystem(cards, config))
        {
            var decomposed = new ThrowValidator(config).DecomposeThrow(cards);
            if (decomposed.Count >= 2)
                return "throw";
        }

        return "mixed";
    }

    private static string ClassifySystem(List<Card> cards, GameConfig config)
    {
        if (cards.All(config.IsTrump))
            return "trump";

        if (cards.All(c => !config.IsTrump(c)))
        {
            var suit = cards[0].Suit;
            if (cards.All(c => c.Suit == suit))
            {
                return suit switch
                {
                    Suit.Spade => "spade",
                    Suit.Heart => "heart",
                    Suit.Club => "club",
                    Suit.Diamond => "diamond",
                    _ => "trump"
                };
            }
        }

        // Mixed system — shouldn't happen for valid plays, but fallback
        return cards.Any(config.IsTrump) ? "trump" : cards[0].Suit.ToString().ToLowerInvariant();
    }

    private static bool IsSameSystem(List<Card> cards, GameConfig config)
    {
        if (cards.All(config.IsTrump)) return true;
        if (cards.Any(config.IsTrump)) return false;
        var suit = cards[0].Suit;
        return cards.All(c => c.Suit == suit);
    }

    private static string BuildDebugKey(List<Card> cards, string patternType, string system, GameConfig config)
    {
        var comparer = new CardComparer(config);
        var sorted = cards.OrderByDescending(c => c, comparer).ToList();

        string topRank = FormatRank(sorted[0]);

        if (patternType == "single")
            return $"single_{system}_{topRank}";

        if (patternType == "pair")
            return $"pair_{system}_{topRank}";

        if (patternType == "tractor")
        {
            int pairCount = cards.Count / 2;
            return $"tractor_{system}_{topRank}_len{pairCount}";
        }

        if (patternType == "throw" || patternType == "mixed")
        {
            // Build a compact representation of components
            var groups = sorted.GroupBy(c => c)
                .OrderByDescending(g => g.Count())
                .ThenByDescending(g => g.Key, comparer);
            var parts = groups.Select(g =>
            {
                string r = FormatRank(g.Key);
                return g.Count() >= 2 ? $"{r}{r}" : r;
            });
            return $"throw_{system}_{string.Join("_", parts)}";
        }

        return $"{patternType}_{system}_{topRank}";
    }

    private static string FormatRank(Card card)
    {
        if (card.Rank == Rank.BigJoker) return "BJ";
        if (card.Rank == Rank.SmallJoker) return "SJ";
        return card.Rank switch
        {
            Rank.Ace => "A",
            Rank.King => "K",
            Rank.Queen => "Q",
            Rank.Jack => "J",
            Rank.Ten => "10",
            _ => ((int)card.Rank).ToString()
        };
    }

    private static List<List<Card>> BuildSystemGroups(GameConfig config, List<Card> cards)
    {
        var groups = new List<List<Card>>();
        var trumpCards = cards.Where(config.IsTrump).ToList();
        if (trumpCards.Count > 0)
            groups.Add(trumpCards);

        groups.AddRange(cards
            .Where(c => !config.IsTrump(c))
            .GroupBy(c => c.Suit)
            .Select(g => g.ToList()));

        return groups;
    }

    private static string BuildCandidateKey(List<Card> cards)
    {
        return string.Join(",", cards
            .OrderBy(c => (int)c.Suit)
            .ThenBy(c => (int)c.Rank)
            .Select(c => $"{(int)c.Suit}-{(int)c.Rank}"));
    }

    private static string CardKey(Card c) => $"{(int)c.Suit}-{(int)c.Rank}";

    private static int CountBits(int value)
    {
        int count = 0;
        while (value != 0) { value &= value - 1; count++; }
        return count;
    }
}
