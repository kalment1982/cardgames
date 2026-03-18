using TractorGame.Core.Models;

namespace PpoEngineHost;

/// <summary>
/// Maps legal actions to fixed action-space slots (ACTION_DIM = 384).
///   0- 53: singles (54 card faces)
///  54-107: pairs   (54 card faces)
/// 108-367: tractors (5 systems * 13 starts * 4 lengths)
/// 368-383: reserved complex-action slots
/// </summary>
public static class ActionSlotMapper
{
    public const int ActionDim = 384;
    public const int SingleBase = 0;
    public const int PairBase = 54;
    public const int TractorBase = 108;
    public const int ReservedBase = 368;
    public const int ReservedCount = 16;

    // ─── Public API ───

    /// <summary>
    /// Map a single action to its slot index.
    /// Returns -1 for complex actions or unsupported tractors that need
    /// reserved-slot assignment.
    /// </summary>
    public static int MapToSlot(LegalAction action, GameConfig config)
    {
        switch (action.PatternType)
        {
            case "single":
                return SingleBase + CardFaceIndex(action.Cards[0]);

            case "pair":
                return PairBase + CardFaceIndex(action.Cards[0]);

            case "tractor":
                var pairCount = action.Cards.Count / 2;
                if (pairCount < 2 || pairCount > 5)
                    return -1;
                if (IsAmbiguousTrumpTractor(action, config))
                    return -1;
                return MapTractorSlot(action, config);

            default:
                // throw / mixed → reserved slot, caller must use MapAllActions
                return -1;
        }
    }

    /// <summary>
    /// Map all legal actions to slots. Complex actions are sorted and assigned
    /// to reserved slots 368-383. Throws on slot conflict or overflow.
    /// </summary>
    public static List<(int slot, LegalAction action)> MapAllActions(
        List<LegalAction> actions, GameConfig config)
    {
        var result = new List<(int slot, LegalAction action)>();
        var slotMap = new Dictionary<int, LegalAction>();
        var complexActions = new List<LegalAction>();

        // Phase 1: map simple actions (single / pair / tractor)
        foreach (var action in actions)
        {
            int slot = MapToSlot(action, config);
            if (slot == -1)
            {
                complexActions.Add(action);
                continue;
            }

            // Conflict detection
            if (slotMap.TryGetValue(slot, out var existing))
            {
                throw new InvalidOperationException(
                    $"ACTION_SLOT_CONFLICT: slot {slot} mapped by both " +
                    $"[{existing.DebugKey}] and [{action.DebugKey}]");
            }

            slotMap[slot] = action;
            result.Add((slot, action));
        }

        // Phase 2: sort complex actions with stable ordering, assign to reserved slots
        if (complexActions.Count > ReservedCount)
        {
            var keys = string.Join(", ", complexActions.Select(a => a.DebugKey));
            throw new InvalidOperationException(
                $"ACTION_SPACE_OVERFLOW: {complexActions.Count} complex actions " +
                $"exceed reserved slot capacity ({ReservedCount}). Keys: [{keys}]");
        }

        var sorted = complexActions
            .OrderByDescending(a => a.Cards.Count)                    // 1. total card count desc
            .ThenBy(a => ComplexityOrder(a))                          // 2. pattern complexity
            .ThenBy(a => SystemSortKey(a))                            // 3. suit systems before trump
            .ThenByDescending(a => CardStrength(a, config))           // 4. card strength desc
            .ThenBy(a => a.DebugKey, StringComparer.Ordinal)          // 5. debug_key lexicographic
            .ToList();

        for (int i = 0; i < sorted.Count; i++)
        {
            int slot = ReservedBase + i;
            if (slotMap.TryGetValue(slot, out var existing))
            {
                throw new InvalidOperationException(
                    $"ACTION_SLOT_CONFLICT: reserved slot {slot} already occupied by " +
                    $"[{existing.DebugKey}]");
            }
            slotMap[slot] = sorted[i];
            result.Add((slot, sorted[i]));
        }

        return result;
    }

    /// <summary>
    /// Build a 0/1 legal mask of length ACTION_DIM from mapped actions.
    /// </summary>
    public static int[] BuildLegalMask(List<(int slot, LegalAction action)> mapped)
    {
        var mask = new int[ActionDim];
        foreach (var (slot, _) in mapped)
        {
            if (slot >= 0 && slot < ActionDim)
                mask[slot] = 1;
        }
        return mask;
    }

    // ─── Card face index (0-53) ───

    /// <summary>
    /// Stable card-face index: BigJoker=0, SmallJoker=1,
    /// ♠A..♠2 = 2..14, ♥A..♥2 = 15..27, ♣A..♣2 = 28..40, ♦A..♦2 = 41..53.
    /// </summary>
    public static int CardFaceIndex(Card card)
    {
        if (card.Rank == Rank.BigJoker) return 0;
        if (card.Rank == Rank.SmallJoker) return 1;

        int suitOffset = card.Suit switch
        {
            Suit.Spade   => 0,
            Suit.Heart   => 13,
            Suit.Club    => 26,
            Suit.Diamond => 39,
            _ => throw new ArgumentException($"Unexpected suit {card.Suit} for non-joker card")
        };

        // A=0, K=1, Q=2, J=3, 10=4, 9=5, 8=6, 7=7, 6=8, 5=9, 4=10, 3=11, 2=12
        int rankOffset = RankToStartIndex(card.Rank);
        return 2 + suitOffset + rankOffset;
    }

    // ─── System index (0-4) ───

    public static int SystemIndex(string system) => system switch
    {
        "spade"   => 0,
        "heart"   => 1,
        "club"    => 2,
        "diamond" => 3,
        "trump"   => 4,
        _ => throw new ArgumentException($"Unknown system: {system}")
    };

    // ─── Rank → start index (A=0 .. 2=12) ───

    public static int RankToStartIndex(Rank rank) => rank switch
    {
        Rank.Ace   => 0,
        Rank.King  => 1,
        Rank.Queen => 2,
        Rank.Jack  => 3,
        Rank.Ten   => 4,
        Rank.Nine  => 5,
        Rank.Eight => 6,
        Rank.Seven => 7,
        Rank.Six   => 8,
        Rank.Five  => 9,
        Rank.Four  => 10,
        Rank.Three => 11,
        Rank.Two   => 12,
        _ => throw new ArgumentException($"Cannot map rank {rank} to start index")
    };

    // ─── Tractor helpers ───

    /// <summary>
    /// Determine the start rank of a tractor action.
    /// For suit tractors: the highest rank among the pairs.
    /// For trump tractors: the highest pair representative under trump ordering.
    /// </summary>
    public static Rank TractorStartRank(LegalAction action, GameConfig config)
        => TractorTopCard(action, config).Rank;

    private static Card TractorTopCard(LegalAction action, GameConfig config)
    {
        var comparer = new CardComparer(config);
        // Get distinct pair representatives sorted descending
        var pairRanks = action.Cards
            .GroupBy(c => c, new CardEqualityComparer())
            .Where(g => g.Count() >= 2)
            .Select(g => g.Key)
            .OrderByDescending(c => c, comparer)
            .ToList();

        if (pairRanks.Count == 0)
            throw new InvalidOperationException("Tractor has no pairs");

        // The highest pair card is the "start" of the tractor
        return pairRanks[0];
    }

    // ─── Private helpers ───

    private static int MapTractorSlot(LegalAction action, GameConfig config)
    {
        int sys = SystemIndex(action.System);
        Rank startRank = TractorStartRank(action, config);
        int start = TractorStartIndex(startRank, action.System, config);
        int length = action.Cards.Count / 2; // number of pairs
        int lengthOffset = length - 2;

        if (lengthOffset < 0 || lengthOffset > 3)
            throw new ArgumentException(
                $"Tractor length {length} out of supported range [2,5]");

        return TractorBase + sys * 52 + start * 4 + lengthOffset;
    }

    private static bool IsAmbiguousTrumpTractor(LegalAction action, GameConfig config)
    {
        if (!string.Equals(action.System, "trump", StringComparison.Ordinal))
            return false;

        var topCard = TractorTopCard(action, config);
        // Trump tractors topped by jokers or level-rank pairs cannot be
        // uniquely decoded from the fixed tractor slot semantics.
        return topCard.Rank == Rank.BigJoker
               || topCard.Rank == Rank.SmallJoker
               || topCard.Rank == config.LevelRank;
    }

    /// <summary>
    /// For trump-system tractors the start index must account for the
    /// special trump ordering. For suit-system tractors it's a direct
    /// rank-to-start mapping.
    /// </summary>
    private static int TractorStartIndex(Rank rank, string system, GameConfig config)
    {
        if (system == "trump")
            return TrumpRankToStartIndex(rank, config);
        return RankToStartIndex(rank);
    }

    /// <summary>
    /// Map a trump-system rank to the stable start index.
    /// Non-ambiguous trump tractors reuse the stable rank position.
    /// Ambiguous joker / level-rank starts are filtered earlier and routed to
    /// reserved slots instead of passing through this mapping.
    /// </summary>
    private static int TrumpRankToStartIndex(Rank rank, GameConfig config)
    {
        if (rank == Rank.BigJoker || rank == Rank.SmallJoker || rank == config.LevelRank)
            throw new ArgumentException(
                $"Ambiguous trump tractor start rank {rank} must use a reserved slot.");

        return RankToStartIndex(rank);
    }

    // ─── Complex action sorting helpers ───

    /// <summary>
    /// Complexity order: mixed (混合首发) = 0, throw (甩牌) = 1, other = 2.
    /// Lower value = higher priority (sorted ascending).
    /// </summary>
    private static int ComplexityOrder(LegalAction action) => action.PatternType switch
    {
        "mixed" => 0,
        "throw" => 1,
        _       => 2,
    };

    /// <summary>
    /// System sort key: suit systems (spade/heart/club/diamond) before trump.
    /// Within suit systems, use natural suit order.
    /// </summary>
    private static int SystemSortKey(LegalAction action) => action.System switch
    {
        "spade"   => 0,
        "heart"   => 1,
        "club"    => 2,
        "diamond" => 3,
        "trump"   => 4,
        _         => 5,
    };

    /// <summary>
    /// Aggregate card strength for tie-breaking: sum of (53 - CardFaceIndex) for
    /// each card. Higher total = stronger hand. Uses face index so that
    /// BigJoker (index 0) has the highest per-card value (53).
    /// </summary>
    private static int CardStrength(LegalAction action, GameConfig config)
    {
        int strength = 0;
        var comparer = new CardComparer(config);
        // Use comparer-based ordering to get a stable strength value
        // that respects trump > suit and within-system rank ordering.
        foreach (var card in action.Cards)
        {
            // 53 - faceIndex gives BigJoker=53, SmallJoker=52, ♠A=51, ...
            strength += 53 - CardFaceIndex(card);
        }
        return strength;
    }
}

/// <summary>
/// Equality comparer for Card (by Suit+Rank).
/// </summary>
internal class CardEqualityComparer : IEqualityComparer<Card>
{
    public bool Equals(Card? x, Card? y)
    {
        if (x is null || y is null) return x is null && y is null;
        return x.Suit == y.Suit && x.Rank == y.Rank;
    }

    public int GetHashCode(Card obj) => HashCode.Combine(obj.Suit, obj.Rank);
}
