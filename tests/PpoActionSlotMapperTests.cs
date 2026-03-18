using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using PpoEngineHost;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests;

public class PpoActionSlotMapperTests
{
    private static readonly GameConfig TrumpTwoConfig = new()
    {
        TrumpSuit = Suit.Spade,
        LevelRank = Rank.Two
    };

    [Fact]
    public void MapToSlot_AmbiguousTrumpTractorWithLevelPair_UsesReservedPath()
    {
        var action = Tractor(
            debugKey: "tractor_trump_2_len2_a",
            new Card(Suit.Spade, Rank.Two),
            new Card(Suit.Spade, Rank.Two),
            new Card(Suit.Heart, Rank.Two),
            new Card(Suit.Heart, Rank.Two));

        var slot = ActionSlotMapper.MapToSlot(action, TrumpTwoConfig);

        Assert.Equal(-1, slot);
    }

    [Fact]
    public void MapToSlot_NonAmbiguousTrumpTractor_UsesFixedSlot()
    {
        var action = Tractor(
            debugKey: "tractor_trump_a_len2",
            new Card(Suit.Spade, Rank.Ace),
            new Card(Suit.Spade, Rank.Ace),
            new Card(Suit.Spade, Rank.King),
            new Card(Suit.Spade, Rank.King));

        var slot = ActionSlotMapper.MapToSlot(action, TrumpTwoConfig);

        Assert.Equal(316, slot);
    }

    [Fact]
    public void MapAllActions_AmbiguousTrumpTractors_DoNotConflict()
    {
        var actions = new List<LegalAction>
        {
            Tractor(
                debugKey: "tractor_trump_2_len2_a",
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two)),
            Tractor(
                debugKey: "tractor_trump_2_len2_b",
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Club, Rank.Two))
        };

        var mapped = ActionSlotMapper.MapAllActions(actions, TrumpTwoConfig);

        Assert.Equal(2, mapped.Count);
        Assert.Equal(new[] { 368, 369 }, mapped.Select(item => item.slot).OrderBy(slot => slot));
        Assert.All(mapped, item => Assert.Equal("tractor", item.action.PatternType));
    }

    [Fact]
    public void SelectCanonicalComplexActions_PreservesReservedTractors()
    {
        var actions = new List<LegalAction>
        {
            Tractor(
                debugKey: "tractor_trump_2_len2_a",
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two)),
            Tractor(
                debugKey: "tractor_trump_2_len2_b",
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Club, Rank.Two)),
            Complex(
                patternType: "throw",
                system: "trump",
                debugKey: "throw_trump_AA_K",
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King)),
            Complex(
                patternType: "throw",
                system: "trump",
                debugKey: "throw_trump_KK_Q",
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen))
        };

        var method = typeof(LegalActionExporter).GetMethod(
            "SelectCanonicalComplexActions",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.NotNull(method);

        var result = (List<LegalAction>)method!.Invoke(null, new object[] { actions, TrumpTwoConfig })!;

        Assert.Equal(3, result.Count);
        Assert.Equal(2, result.Count(action => action.PatternType == "tractor"));
        Assert.Contains(result, action => action.DebugKey == "throw_trump_AA_K");
        Assert.DoesNotContain(result, action => action.DebugKey == "throw_trump_KK_Q");
    }

    private static LegalAction Tractor(string debugKey, params Card[] cards) =>
        new()
        {
            Cards = cards.ToList(),
            PatternType = "tractor",
            System = "trump",
            DebugKey = debugKey
        };

    private static LegalAction Complex(string patternType, string system, string debugKey, params Card[] cards) =>
        new()
        {
            Cards = cards.ToList(),
            PatternType = patternType,
            System = system,
            DebugKey = debugKey
        };
}
