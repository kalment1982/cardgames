using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.AI;
using TractorGame.Core.AI.V21;
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

    public static LegalAction CreateForCurrentState(Game game, int playerIndex, List<Card> cards)
    {
        var config = new GameConfig
        {
            LevelRank = game.State.LevelRank,
            TrumpSuit = game.State.TrumpSuit
        };

        bool isLead = game.CurrentTrick.Count == 0;
        if (isLead)
        {
            return BuildLegalAction(cards, config, isLead: true, isFollow: false,
                leadCards: null, leadCategory: null, leadSuit: null);
        }

        var leadCards = game.CurrentTrick[0].Cards;
        var leadCategory = config.GetCardCategory(leadCards[0]);
        var leadSuit = leadCards[0].Suit;
        return BuildLegalAction(cards, config, isLead: false, isFollow: true,
            leadCards: leadCards, leadCategory: leadCategory, leadSuit: leadSuit);
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
            AddThrowCandidates(rawCandidates, group);
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
        List<List<Card>> candidates, List<Card> group)
    {
        if (group.Count < 3)
            return;

        // Phase 1 bounded canonical export:
        // export only the full same-system group as the canonical complex action.
        // Singles, pairs, and tractors remain exhaustively exported elsewhere.
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

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var fixedSlotActions = new List<LegalAction>();
        var complexActions = new List<LegalAction>();

        void TryAddCandidate(List<Card> candidate)
        {
            var key = BuildCandidateKey(candidate);
            if (!seen.Add(key))
                return;

            if (!followValidator.IsValidFollow(hand, leadCards, candidate))
                return;

            var action = BuildLegalAction(candidate, config,
                isLead: false, isFollow: true,
                leadCards: leadCards, leadCategory: leadCategory, leadSuit: leadSuit);

            if (ActionSlotMapper.MapToSlot(action, config) == -1)
                complexActions.Add(action);
            else
                fixedSlotActions.Add(action);
        }

        AddExhaustiveFixedSlotFollowCandidates(hand, need, config, comparer, TryAddCandidate);

        foreach (var candidate in BuildCanonicalFollowCandidates(game, playerIndex, hand, leadCards, config))
            TryAddCandidate(candidate);

        if (fixedSlotActions.Count == 0 && complexActions.Count == 0 &&
            LegalPlayResolver.TryResolve(game, playerIndex, config, out var fallback))
        {
            TryAddCandidate(fallback);
        }

        fixedSlotActions.AddRange(SelectCanonicalComplexActions(complexActions, config));
        return fixedSlotActions;
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

    private static void AddExhaustiveFixedSlotFollowCandidates(
        List<Card> hand,
        int need,
        GameConfig config,
        CardComparer comparer,
        Action<List<Card>> onCandidate)
    {
        if (need == 1)
        {
            foreach (var card in hand.GroupBy(c => c).Select(g => g.First()))
                onCandidate(new List<Card> { card });
            return;
        }

        if (need == 2)
        {
            foreach (var pair in hand.GroupBy(c => c).Where(g => g.Count() >= 2))
                onCandidate(pair.Take(2).ToList());
            return;
        }

        if (need < 4 || need % 2 != 0)
            return;

        int pairCount = need / 2;
        foreach (var group in BuildSystemGroups(config, hand))
        {
            var pairReps = group
                .GroupBy(c => c)
                .Where(g => g.Count() >= 2)
                .Select(g => g.Key)
                .OrderBy(c => c, comparer)
                .ToList();

            if (pairReps.Count < pairCount)
                continue;

            for (int start = 0; start <= pairReps.Count - pairCount; start++)
            {
                var tractorCards = new List<Card>(need);
                for (int k = start; k < start + pairCount; k++)
                {
                    tractorCards.Add(pairReps[k]);
                    tractorCards.Add(pairReps[k]);
                }

                var pattern = new CardPattern(tractorCards, config);
                if (pattern.IsTractor(tractorCards))
                    onCandidate(tractorCards);
            }
        }
    }

    private static List<List<Card>> BuildCanonicalFollowCandidates(
        Game game,
        int playerIndex,
        List<Card> hand,
        List<Card> leadCards,
        GameConfig config)
    {
        var currentWinningCards = DetermineCurrentWinningCards(game, config);
        int currentWinner = DetermineCurrentWinner(game, config);

        var context = new RuleAIContext
        {
            Phase = PhaseKind.Follow,
            PlayerIndex = playerIndex,
            MyHand = new List<Card>(hand),
            LeadCards = new List<Card>(leadCards),
            CurrentWinningCards = currentWinningCards,
            GameConfig = config,
            RuleProfile = RuleProfile.FromConfig(config),
            DifficultyProfile = DifficultyProfile.From(AIDifficulty.Hard),
            StyleProfile = StyleProfile.Create(playerIndex * 997 + hand.Count * 31 + leadCards.Count),
            DecisionFrame = new DecisionFrame
            {
                PhaseKind = PhaseKind.Follow,
                CurrentWinningPlayer = currentWinner,
                PartnerWinning = currentWinner >= 0 && currentWinner % 2 == playerIndex % 2,
                LeadCards = new List<Card>(leadCards),
                CurrentWinningCards = currentWinningCards,
                CurrentTrickScore = game.CurrentTrick.Sum(play => play.Cards.Sum(card => card.Score))
            }
        };

        return new FollowCandidateGenerator(config).Generate(context);
    }

    private static List<LegalAction> SelectCanonicalComplexActions(
        List<LegalAction> actions,
        GameConfig config)
    {
        var reservedTractors = actions
            .Where(action => action.PatternType == "tractor")
            .ToList();

        var canonicalOthers = actions
            .Where(action => action.PatternType != "tractor")
            .GroupBy(action => $"{action.PatternType}|{action.System}", StringComparer.Ordinal)
            .Select(group => group
                .OrderByDescending(action => action.Cards.Count)
                .ThenBy(action => ComplexityOrder(action.PatternType))
                .ThenBy(action => SystemSortKey(action.System))
                .ThenByDescending(action => CardStrength(action, config))
                .ThenBy(action => action.DebugKey, StringComparer.Ordinal)
                .First())
            .ToList();

        var canonical = reservedTractors
            .Concat(canonicalOthers)
            .OrderByDescending(action => action.Cards.Count)
            .ThenBy(action => ComplexityOrder(action.PatternType))
            .ThenBy(action => SystemSortKey(action.System))
            .ThenByDescending(action => CardStrength(action, config))
            .ThenBy(action => action.DebugKey, StringComparer.Ordinal)
            .ToList();

        if (canonical.Count > ActionSlotMapper.ReservedCount)
        {
            throw new InvalidOperationException(
                $"ACTION_SPACE_OVERFLOW: canonical complex pool produced {canonical.Count} actions " +
                $"after grouping non-tractor actions by pattern_type+system.");
        }

        return canonical;
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

    private static int DetermineCurrentWinner(Game game, GameConfig config)
    {
        if (game.CurrentTrick.Count == 0)
            return -1;

        var judge = new TrickJudge(config);
        return judge.DetermineWinner(game.CurrentTrick);
    }

    private static List<Card> DetermineCurrentWinningCards(Game game, GameConfig config)
    {
        int winner = DetermineCurrentWinner(game, config);
        if (winner < 0)
            return new List<Card>();

        var play = game.CurrentTrick.FirstOrDefault(trickPlay => trickPlay.PlayerIndex == winner);
        return play?.Cards != null
            ? new List<Card>(play.Cards)
            : new List<Card>();
    }

    private static int ComplexityOrder(string patternType) => patternType switch
    {
        "mixed" => 0,
        "throw" => 1,
        _ => 2
    };

    private static int SystemSortKey(string system) => system switch
    {
        "spade" => 0,
        "heart" => 1,
        "club" => 2,
        "diamond" => 3,
        "trump" => 4,
        _ => 5
    };

    private static int CardStrength(LegalAction action, GameConfig config)
    {
        return action.Cards.Sum(card => 53 - ActionSlotMapper.CardFaceIndex(card));
    }
}
