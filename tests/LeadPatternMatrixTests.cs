using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class LeadPatternMatrixTests
    {
        private static readonly GameConfig Config = new()
        {
            LevelRank = Rank.Two,
            TrumpSuit = Suit.Spade
        };

        private static readonly CardComparer Comparer = new(Config);
        private static readonly ThrowValidator ThrowValidator = new(Config);
        private static readonly PlayValidator PlayValidator = new(Config);
        private static readonly FollowValidator FollowValidator = new(Config);
        private static readonly TrickJudge TrickJudge = new(Config);

        private static readonly IReadOnlyList<Card> SuitPool = BuildSuitPool();
        private static readonly IReadOnlyList<Card> TrumpPool = BuildTrumpPool();
        private static readonly IReadOnlyList<Card> SuitDiscardPool = BuildSuitDiscardPool();
        private static readonly IReadOnlyList<Card> TrumpLeadDiscardPool = BuildTrumpLeadDiscardPool();
        private static readonly IReadOnlyList<Card> LowTrumpSinglesPool = TrumpPool.Reverse().ToList();

        private static readonly IReadOnlyList<(int N, int M, int J)> AllFeasibleCombos = BuildAllFeasibleCombos();
        private static readonly IReadOnlyList<(int N, int M, int J)> SuitFeasibleCombos = AllFeasibleCombos
            .Where(combo => IsSuitFeasible(combo.N, combo.M, combo.J))
            .ToList();
        private static readonly IReadOnlyList<(int N, int M, int J)> RepresentativeCombos = BuildRepresentativeCombos();

        private static readonly Dictionary<(LeadSystemKind Kind, int M, int J, bool PreferHigh, string PoolKey), List<Card>> PairAllocationCache = new();

        private static readonly IReadOnlyList<LeadPatternCase> CoreCaseList = BuildCoreCases();
        private static readonly IReadOnlyList<LeadPatternCase> CutCaseList = BuildCutCases();
        private static readonly IReadOnlyList<LeadPatternCase> AllTrumpWeakerCaseList = BuildAllTrumpWeakerCases();
        private static readonly IReadOnlyList<LeadPatternCase> PartialSuitTrumpCaseList = BuildPartialSuitTrumpCases();
        private static readonly IReadOnlyList<LeadPatternCase> ThrowBlockedCaseList = BuildThrowBlockedCases();
        private static readonly IReadOnlyList<LeadPatternCase> InvalidFollowCaseList = BuildInvalidFollowCases();

        public static IEnumerable<object[]> CoreCases()
        {
            foreach (var testCase in CoreCaseList)
                yield return new object[] { testCase };
        }

        public static IEnumerable<object[]> CutCases()
        {
            foreach (var testCase in CutCaseList)
                yield return new object[] { testCase };
        }

        public static IEnumerable<object[]> AllTrumpWeakerCases()
        {
            foreach (var testCase in AllTrumpWeakerCaseList)
                yield return new object[] { testCase };
        }

        public static IEnumerable<object[]> PartialSuitTrumpCases()
        {
            foreach (var testCase in PartialSuitTrumpCaseList)
                yield return new object[] { testCase };
        }

        public static IEnumerable<object[]> ThrowBlockedCases()
        {
            foreach (var testCase in ThrowBlockedCaseList)
                yield return new object[] { testCase };
        }

        public static IEnumerable<object[]> InvalidFollowCases()
        {
            foreach (var testCase in InvalidFollowCaseList)
                yield return new object[] { testCase };
        }

        [Fact]
        public void CaseCount_CoversAtLeast150ConcreteCases()
        {
            var total = CoreCaseList.Count
                + CutCaseList.Count
                + AllTrumpWeakerCaseList.Count
                + PartialSuitTrumpCaseList.Count
                + ThrowBlockedCaseList.Count
                + InvalidFollowCaseList.Count;

            Assert.True(CoreCaseList.Count >= 150);
            Assert.True(total >= 300);
            Assert.Equal(239, CoreCaseList.Count);
        }

        [Theory]
        [MemberData(nameof(CoreCases))]
        public void CoreMatrix_CoversAllFeasibleNmpjCombinations(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        [Theory]
        [MemberData(nameof(CutCases))]
        public void EffectiveTrumpCut_CasesAreValidated(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        [Theory]
        [MemberData(nameof(AllTrumpWeakerCases))]
        public void AllTrumpButNotEnoughStructure_CasesAreValidated(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        [Theory]
        [MemberData(nameof(PartialSuitTrumpCases))]
        public void PartialSuitWithTrumpFillers_CasesAreValidated(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        [Theory]
        [MemberData(nameof(ThrowBlockedCases))]
        public void ThrowBlockedBySameSystemStrongerCards_CasesAreValidated(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        [Theory]
        [MemberData(nameof(InvalidFollowCases))]
        public void InvalidFollowCases_ReturnExpectedReasonCodes(LeadPatternCase testCase)
        {
            ValidateCase(testCase);
        }

        private static void ValidateCase(LeadPatternCase testCase)
        {
            for (int playerIndex = 1; playerIndex < 4; playerIndex++)
            {
                var result = FollowValidator.IsValidFollowEx(
                    testCase.Hands[playerIndex],
                    testCase.Plays[0].Cards,
                    testCase.Plays[playerIndex].Cards);
                if (testCase.ExpectedFollowSuccess[playerIndex])
                {
                    Assert.True(result.Success, $"{testCase.Id} player{playerIndex} follow should be valid, actual={result.ReasonCode}");
                }
                else
                {
                    Assert.False(result.Success, $"{testCase.Id} player{playerIndex} follow should be invalid");
                    Assert.Equal(testCase.ExpectedFollowReason[playerIndex], result.ReasonCode);
                }
            }

            if (testCase.ExpectedWinner >= 0)
            {
                Assert.Equal(testCase.ExpectedWinner, TrickJudge.DetermineWinner(testCase.Plays.Select(play => new TrickPlay(play.PlayerIndex, play.Cards)).ToList()));
            }

            var otherHands = testCase.Hands.Skip(1).Select(hand => new List<Card>(hand)).ToList();
            Assert.Equal(testCase.ExpectedThrowSuccess, ThrowValidator.IsThrowSuccessful(testCase.Plays[0].Cards, otherHands));

            var playResult = PlayValidator.IsValidPlayEx(testCase.Hands[0], testCase.Plays[0].Cards, otherHands);
            if (IsMixedLead(testCase.Plays[0].Cards))
            {
                Assert.Equal(testCase.ExpectedThrowSuccess, playResult.Success);
                if (!testCase.ExpectedThrowSuccess)
                    Assert.Equal(ReasonCodes.ThrowNotMax, playResult.ReasonCode);
            }
            else
            {
                Assert.True(playResult.Success, $"{testCase.Id} lead play should remain a valid non-throw pattern");
            }
        }

        private static IReadOnlyList<LeadPatternCase> BuildCoreCases()
        {
            var list = new List<LeadPatternCase>();
            foreach (var combo in AllFeasibleCombos)
            {
                if (!TryBuildPatternCards(LeadSystemKind.Suit, combo.N, combo.M, combo.J, preferHigh: true, out var leadCards) &&
                    !TryBuildPatternCards(LeadSystemKind.Trump, combo.N, combo.M, combo.J, preferHigh: true, out leadCards))
                {
                    throw new InvalidOperationException($"Unable to construct core case for n={combo.N}, m={combo.M}, j={combo.J}");
                }

                var systemKind = leadCards.All(Config.IsTrump) ? LeadSystemKind.Trump : LeadSystemKind.Suit;
                var leadCount = leadCards.Count;

                var p1 = BuildResidualFollowerPlay(systemKind, leadCards, leadCount, includeTrumpFillers: false);
                var p2 = BuildOffSystemPlay(systemKind, leadCount, AlternateDiscardStyle.First);
                var p3 = BuildOffSystemPlay(systemKind, leadCount, AlternateDiscardStyle.Second);

                list.Add(new LeadPatternCase(
                    $"core-{systemKind.ToString().ToLowerInvariant()}-n{combo.N}-m{combo.M}-j{combo.J}",
                    combo.N,
                    combo.M,
                    combo.J,
                    systemKind,
                    new[]
                    {
                        new List<Card>(leadCards),
                        new List<Card>(p1),
                        new List<Card>(p2),
                        new List<Card>(p3)
                    },
                    new[]
                    {
                        new TrickPlayView(0, leadCards),
                        new TrickPlayView(1, p1),
                        new TrickPlayView(2, p2),
                        new TrickPlayView(3, p3)
                    },
                    new[] { true, true, true, true },
                    new string?[] { null, null, null, null },
                    expectedWinner: 0,
                    expectedThrowSuccess: true,
                    note: "core-matrix"));
            }

            return list;
        }

        private static IReadOnlyList<LeadPatternCase> BuildCutCases()
        {
            var list = new List<LeadPatternCase>();
            foreach (var combo in RepresentativeCombos.Where(combo => IsSuitFeasible(combo.N, combo.M, combo.J)))
            {
                if (!TryBuildPatternCards(LeadSystemKind.Suit, combo.N, combo.M, combo.J, preferHigh: true, out var leadCards) ||
                    !TryBuildPatternCards(LeadSystemKind.Trump, combo.N, combo.M, combo.J, preferHigh: false, out var trumpCut))
                {
                    continue;
                }

                var leadCount = leadCards.Count;

                list.Add(new LeadPatternCase(
                    $"cut-suit-n{combo.N}-m{combo.M}-j{combo.J}",
                    combo.N,
                    combo.M,
                    combo.J,
                    LeadSystemKind.Suit,
                    new[]
                    {
                        new List<Card>(leadCards),
                        new List<Card>(trumpCut),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.First),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.Second)
                    },
                    new[]
                    {
                        new TrickPlayView(0, leadCards),
                        new TrickPlayView(1, trumpCut),
                        new TrickPlayView(2, BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.First)),
                        new TrickPlayView(3, BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.Second))
                    },
                    new[] { true, true, true, true },
                    new string?[] { null, null, null, null },
                    expectedWinner: 1,
                    expectedThrowSuccess: true,
                    note: "effective-trump-cut"));
            }

            return list;
        }

        private static IReadOnlyList<LeadPatternCase> BuildAllTrumpWeakerCases()
        {
            var list = new List<LeadPatternCase>();
            foreach (var combo in RepresentativeCombos.Where(combo => IsSuitFeasible(combo.N, combo.M, combo.J)
                                                                      && combo.M + combo.J > 0))
            {
                if (!TryBuildPatternCards(LeadSystemKind.Suit, combo.N, combo.M, combo.J, preferHigh: true, out var leadCards))
                    continue;

                if (leadCards.Count > 18)
                    continue;

                var weakTrump = TakeCards(BuildLowTrumpSinglesMultiset(), leadCards.Count);
                list.Add(new LeadPatternCase(
                    $"all-trump-weaker-n{combo.N}-m{combo.M}-j{combo.J}",
                    combo.N,
                    combo.M,
                    combo.J,
                    LeadSystemKind.Suit,
                    new[]
                    {
                        new List<Card>(leadCards),
                        new List<Card>(weakTrump),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.First),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.Second)
                    },
                    new[]
                    {
                        new TrickPlayView(0, leadCards),
                        new TrickPlayView(1, weakTrump),
                        new TrickPlayView(2, BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.First)),
                        new TrickPlayView(3, BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.Second))
                    },
                    new[] { true, true, true, true },
                    new string?[] { null, null, null, null },
                    expectedWinner: 0,
                    expectedThrowSuccess: true,
                    note: "all-trump-but-incomplete"));
            }

            return list;
        }

        private static IReadOnlyList<LeadPatternCase> BuildPartialSuitTrumpCases()
        {
            var list = new List<LeadPatternCase>();
            foreach (var combo in RepresentativeCombos.Where(combo => IsSuitFeasible(combo.N, combo.M, combo.J)
                                                                      && combo.M + combo.J > 0))
            {
                if (!TryBuildPatternCards(LeadSystemKind.Suit, combo.N, combo.M, combo.J, preferHigh: true, out var leadCards))
                    continue;

                var remainingSuit = GetRemainingSystemCards(LeadSystemKind.Suit, leadCards);
                if (remainingSuit.Count == 0 || remainingSuit.Count >= leadCards.Count)
                    continue;

                var p1Suit = SortAscending(remainingSuit).Take(remainingSuit.Count).ToList();
                var trumpFillers = TakeCards(BuildLowTrumpSinglesMultiset(), leadCards.Count - p1Suit.Count);
                var p1 = SortDescending(p1Suit.Concat(trumpFillers));

                list.Add(new LeadPatternCase(
                    $"partial-suit-trump-fill-n{combo.N}-m{combo.M}-j{combo.J}",
                    combo.N,
                    combo.M,
                    combo.J,
                    LeadSystemKind.Suit,
                    new[]
                    {
                        new List<Card>(leadCards),
                        new List<Card>(p1),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.First),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.Second)
                    },
                    new[]
                    {
                        new TrickPlayView(0, leadCards),
                        new TrickPlayView(1, p1),
                        new TrickPlayView(2, BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.First)),
                        new TrickPlayView(3, BuildOffSystemPlay(LeadSystemKind.Suit, leadCards.Count, AlternateDiscardStyle.Second))
                    },
                    new[] { true, true, true, true },
                    new string?[] { null, null, null, null },
                    expectedWinner: 0,
                    expectedThrowSuccess: true,
                    note: "partial-suit-with-trump-fillers"));
            }

            return list;
        }

        private static IReadOnlyList<LeadPatternCase> BuildThrowBlockedCases()
        {
            var list = new List<LeadPatternCase>();
            foreach (var combo in RepresentativeCombos.Where(combo => IsSuitFeasible(combo.N, combo.M, combo.J)
                                                                      && combo.N + combo.M + 2 * combo.J <= 6))
            {
                if (!TryBuildPatternCards(LeadSystemKind.Suit, combo.N, combo.M, combo.J, preferHigh: false, out var leadCards))
                    continue;

                if (!IsMixedLead(leadCards))
                    continue;

                var remainingDistinct = SuitPool.Where(card => !leadCards.Any(lead => lead.Equals(card))).ToList();
                if (remainingDistinct.Count < combo.N + combo.M + 2 * combo.J)
                    continue;

                if (!TryBuildPatternCardsFromPool(remainingDistinct, combo.N, combo.M, combo.J, preferHigh: true, LeadSystemKind.Suit, out var strongFollow))
                    continue;

                var leadCount = leadCards.Count;
                if (strongFollow.Count != leadCount)
                    continue;

                list.Add(new LeadPatternCase(
                    $"throw-blocked-n{combo.N}-m{combo.M}-j{combo.J}",
                    combo.N,
                    combo.M,
                    combo.J,
                    LeadSystemKind.Suit,
                    new[]
                    {
                        new List<Card>(leadCards),
                        new List<Card>(strongFollow),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.First),
                        BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.Second)
                    },
                    new[]
                    {
                        new TrickPlayView(0, leadCards),
                        new TrickPlayView(1, strongFollow),
                        new TrickPlayView(2, BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.First)),
                        new TrickPlayView(3, BuildOffSystemPlay(LeadSystemKind.Suit, leadCount, AlternateDiscardStyle.Second))
                    },
                    new[] { true, true, true, true },
                    new string?[] { null, null, null, null },
                    expectedWinner: 1,
                    expectedThrowSuccess: false,
                    note: "same-system-stronger-blocker"));
            }

            return list;
        }

        private static IReadOnlyList<LeadPatternCase> BuildInvalidFollowCases()
        {
            var list = new List<LeadPatternCase>();

            list.Add(BuildInvalidFollowCase(
                "invalid-pair-required",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Ace)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack)),
                expectedReason: ReasonCodes.FollowPairRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-tractor-required",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace), new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King), new Card(Suit.Heart, Rank.King)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Nine), new Card(Suit.Heart, Rank.Nine)),
                expectedReason: ReasonCodes.FollowTractorRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-follow-suit-mixed",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Eight), new Card(Suit.Heart, Rank.Eight)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Club, Rank.Three)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Club, Rank.Three)),
                expectedReason: ReasonCodes.FollowSuitRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-follow-suit-partial",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Ten)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Heart, Rank.Six),
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Club, Rank.Three)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Eight),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Heart, Rank.Six),
                    new Card(Suit.Club, Rank.Three)),
                expectedReason: ReasonCodes.FollowSuitRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-trump-follow-required",
                lead: Cards(
                    new Card(Suit.Spade, Rank.Ace),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Queen)),
                p1Hand: Cards(
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Ten),
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Club, Rank.Three)),
                p1Play: Cards(
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Club, Rank.Three)),
                expectedReason: ReasonCodes.FollowSuitRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-pair-hidden-in-tractor-still-pair-required",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Ace)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack)),
                expectedReason: ReasonCodes.FollowPairRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-tractor-with-enough-suit-cards",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace), new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King), new Card(Suit.Heart, Rank.King)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.Queen), new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack), new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Heart, Rank.Nine)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Heart, Rank.Nine)),
                expectedReason: ReasonCodes.FollowTractorRequired));

            list.Add(BuildInvalidFollowCase(
                "invalid-follow-count-mismatch",
                lead: Cards(
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.King),
                    new Card(Suit.Heart, Rank.Queen)),
                p1Hand: Cards(
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Ten),
                    new Card(Suit.Heart, Rank.Nine)),
                p1Play: Cards(
                    new Card(Suit.Heart, Rank.Jack),
                    new Card(Suit.Heart, Rank.Ten)),
                expectedReason: ReasonCodes.FollowCountMismatch));

            return list;
        }

        private static LeadPatternCase BuildInvalidFollowCase(
            string id,
            List<Card> lead,
            List<Card> p1Hand,
            List<Card> p1Play,
            string expectedReason)
        {
            var count = lead.Count;
            var p2 = BuildOffSystemPlay(LeadSystemKind.Suit, count, AlternateDiscardStyle.First);
            var p3 = BuildOffSystemPlay(LeadSystemKind.Suit, count, AlternateDiscardStyle.Second);
            return new LeadPatternCase(
                id,
                CountComponents(lead).Singles,
                CountComponents(lead).Pairs,
                CountComponents(lead).Tractors,
                LeadSystemKind.Suit,
                new[]
                {
                    new List<Card>(lead),
                    new List<Card>(p1Hand),
                    new List<Card>(p2),
                    new List<Card>(p3)
                },
                new[]
                {
                    new TrickPlayView(0, lead),
                    new TrickPlayView(1, p1Play),
                    new TrickPlayView(2, p2),
                    new TrickPlayView(3, p3)
                },
                new[] { true, false, true, true },
                new string?[] { null, expectedReason, null, null },
                expectedWinner: -1,
                expectedThrowSuccess: true,
                note: "invalid-follow" );
        }

        private static LeadPatternCase CreateCase(
            string id,
            int n,
            int m,
            int j,
            LeadSystemKind systemKind,
            List<Card> lead,
            List<Card> p1,
            List<Card> p2,
            List<Card> p3,
            int expectedWinner,
            bool expectedThrowSuccess,
            string note)
        {
            return new LeadPatternCase(
                id,
                n,
                m,
                j,
                systemKind,
                new[]
                {
                    new List<Card>(lead),
                    new List<Card>(p1),
                    new List<Card>(p2),
                    new List<Card>(p3)
                },
                new[]
                {
                    new TrickPlayView(0, lead),
                    new TrickPlayView(1, p1),
                    new TrickPlayView(2, p2),
                    new TrickPlayView(3, p3)
                },
                new[] { true, true, true, true },
                new string?[] { null, null, null, null },
                expectedWinner,
                expectedThrowSuccess,
                note);
        }

        private static List<Card> BuildResidualFollowerPlay(LeadSystemKind systemKind, List<Card> leadCards, int leadCount, bool includeTrumpFillers)
        {
            var remainingSystemCards = SortAscending(GetRemainingSystemCards(systemKind, leadCards));
            var takenSystemCards = remainingSystemCards.Take(Math.Min(leadCount, remainingSystemCards.Count)).ToList();
            int fillerCount = leadCount - takenSystemCards.Count;
            var fillers = includeTrumpFillers
                ? TakeCards(BuildLowTrumpSinglesMultiset(), fillerCount)
                : BuildOffSystemFillers(systemKind, fillerCount, AlternateDiscardStyle.First);
            return SortDescending(takenSystemCards.Concat(fillers));
        }

        private static List<Card> BuildOffSystemPlay(LeadSystemKind systemKind, int count, AlternateDiscardStyle style)
        {
            return BuildOffSystemFillers(systemKind, count, style);
        }

        private static List<Card> BuildOffSystemFillers(LeadSystemKind systemKind, int count, AlternateDiscardStyle style)
        {
            if (count <= 0)
                return new List<Card>();

            var pool = systemKind == LeadSystemKind.Suit
                ? (style == AlternateDiscardStyle.First ? SuitDiscardPool : SuitDiscardPool.Reverse().ToList())
                : (style == AlternateDiscardStyle.First ? TrumpLeadDiscardPool : TrumpLeadDiscardPool.Reverse().ToList());
            return TakeCards(pool, count);
        }

        private static List<Card> BuildPatternCards(LeadSystemKind kind, int n, int m, int j, bool preferHigh)
        {
            var pool = kind == LeadSystemKind.Suit ? SuitPool : TrumpPool;
            return BuildPatternCardsFromPool(pool, n, m, j, preferHigh, kind);
        }

        private static bool TryBuildPatternCards(LeadSystemKind kind, int n, int m, int j, bool preferHigh, out List<Card> cards)
        {
            var pool = kind == LeadSystemKind.Suit ? SuitPool : TrumpPool;
            return TryBuildPatternCardsFromPool(pool, n, m, j, preferHigh, kind, out cards);
        }

        private static bool TryBuildPatternCardsFromPool(
            IReadOnlyList<Card> pool,
            int n,
            int m,
            int j,
            bool preferHigh,
            LeadSystemKind kind,
            out List<Card> cards)
        {
            try
            {
                cards = BuildPatternCardsFromPool(pool, n, m, j, preferHigh, kind);
                return true;
            }
            catch (InvalidOperationException)
            {
                cards = new List<Card>();
                return false;
            }
        }

        private static List<Card> BuildPatternCardsFromPool(IReadOnlyList<Card> pool, int n, int m, int j, bool preferHigh, LeadSystemKind kind)
        {
            var pairValues = GetPairValues(pool, kind, m, j, preferHigh);
            var pairValueKeys = pairValues.Select(CardKey).ToHashSet(StringComparer.Ordinal);
            var orderedPool = preferHigh ? pool.ToList() : pool.Reverse().ToList();
            var singles = orderedPool
                .Where(card => !pairValueKeys.Contains(CardKey(card)))
                .Take(n)
                .Select(Clone)
                .ToList();

            if (singles.Count != n)
                throw new InvalidOperationException($"Unable to allocate {n} singles for pool={kind} m={m} j={j}");

            var cards = new List<Card>();
            foreach (var pairValue in pairValues)
            {
                cards.Add(Clone(pairValue));
                cards.Add(Clone(pairValue));
            }
            cards.AddRange(singles);
            return SortDescending(cards);
        }

        private static List<Card> GetPairValues(IReadOnlyList<Card> pool, LeadSystemKind kind, int m, int j, bool preferHigh)
        {
            if (m == 0 && j == 0)
                return new List<Card>();

            var poolKey = string.Join('|', pool.Select(CardKey));
            var cacheKey = (kind, m, j, preferHigh, poolKey);
            if (PairAllocationCache.TryGetValue(cacheKey, out var cached))
                return cached.Select(Clone).ToList();

            int need = m + 2 * j;
            var orderedPool = preferHigh ? pool.ToList() : pool.Reverse().ToList();
            var selected = new List<Card>(need);
            List<Card>? found = null;

            void Search(int start, int remaining)
            {
                if (found != null)
                    return;
                if (remaining == 0)
                {
                    var doubled = new List<Card>();
                    foreach (var card in selected)
                    {
                        doubled.Add(Clone(card));
                        doubled.Add(Clone(card));
                    }

                    var summary = CountComponents(doubled);
                    if (summary.Pairs == m && summary.Tractors == j && summary.Singles == 0)
                        found = selected.Select(Clone).ToList();
                    return;
                }

                for (int index = start; index <= orderedPool.Count - remaining; index++)
                {
                    selected.Add(orderedPool[index]);
                    Search(index + 1, remaining - 1);
                    selected.RemoveAt(selected.Count - 1);
                    if (found != null)
                        return;
                }
            }

            Search(0, need);
            if (found == null)
                throw new InvalidOperationException($"No pair allocation found for kind={kind}, m={m}, j={j}, preferHigh={preferHigh}");

            PairAllocationCache[cacheKey] = found.Select(Clone).ToList();
            return found;
        }

        private static ComponentSummary CountComponents(List<Card> cards)
        {
            var components = ThrowValidator.DecomposeThrow(cards);
            int singles = 0;
            int pairs = 0;
            int tractors = 0;
            foreach (var component in components)
            {
                if (component.Count == 1)
                {
                    singles++;
                    continue;
                }

                var pattern = new CardPattern(component, Config);
                if (pattern.Type == PatternType.Tractor)
                    tractors++;
                else if (pattern.Type == PatternType.Pair)
                    pairs++;
                else
                    singles += component.Count;
            }

            return new ComponentSummary(singles, pairs, tractors);
        }

        private static bool IsMixedLead(List<Card> cards)
        {
            if (cards.Count <= 2)
                return false;
            var pattern = new CardPattern(cards, Config);
            return pattern.Type == PatternType.Mixed;
        }

        private static List<Card> GetRemainingSystemCards(LeadSystemKind kind, List<Card> usedCards)
        {
            var multiset = BuildSystemMultiset(kind);
            foreach (var card in usedCards)
            {
                var index = multiset.FindIndex(candidate => candidate.Equals(card));
                if (index >= 0)
                    multiset.RemoveAt(index);
            }
            return multiset;
        }

        private static List<Card> BuildSystemMultiset(LeadSystemKind kind)
        {
            var pool = kind == LeadSystemKind.Suit ? SuitPool : TrumpPool;
            return pool.SelectMany(card => new[] { Clone(card), Clone(card) }).ToList();
        }

        private static List<Card> BuildLowTrumpSinglesMultiset()
        {
            return LowTrumpSinglesPool.Select(Clone).ToList();
        }

        private static IReadOnlyList<(int N, int M, int J)> BuildAllFeasibleCombos()
        {
            var list = new List<(int N, int M, int J)>();
            for (int n = 0; n <= 10; n++)
            {
                for (int m = 0; m <= 4; m++)
                {
                    for (int j = 0; j <= 4; j++)
                    {
                        if (n == 0 && m == 0 && j == 0)
                            continue;
                        if (n + 2 * m + 4 * j > 25)
                            continue;
                        list.Add((n, m, j));
                    }
                }
            }
            return list;
        }

        private static IReadOnlyList<(int N, int M, int J)> BuildRepresentativeCombos()
        {
            var nValues = new[] { 0, 1, 2, 4, 7, 10 };
            var mValues = new[] { 0, 1, 2, 4 };
            var jValues = new[] { 0, 1, 2, 4 };
            var list = new List<(int N, int M, int J)>();

            foreach (var n in nValues)
            {
                foreach (var m in mValues)
                {
                    foreach (var j in jValues)
                    {
                        if (n == 0 && m == 0 && j == 0)
                            continue;
                        if (n + 2 * m + 4 * j > 25)
                            continue;
                        list.Add((n, m, j));
                    }
                }
            }

            foreach (var manual in new[]
            {
                (3, 1, 1), (5, 2, 1), (6, 0, 2), (8, 1, 2), (0, 4, 4), (9, 4, 0), (10, 3, 1)
            })
            {
                if (!list.Contains(manual) && manual.Item1 + 2 * manual.Item2 + 4 * manual.Item3 <= 25)
                    list.Add(manual);
            }

            return list
                .OrderBy(tuple => tuple.N)
                .ThenBy(tuple => tuple.M)
                .ThenBy(tuple => tuple.J)
                .ToList();
        }

        private static bool IsSuitFeasible(int n, int m, int j)
        {
            return n + m + 2 * j <= 12;
        }

        private static IReadOnlyList<Card> BuildSuitPool()
        {
            var cards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Three)
            };
            return SortDescending(cards);
        }

        private static IReadOnlyList<Card> BuildTrumpPool()
        {
            var cards = new List<Card>
            {
                new Card(Suit.Joker, Rank.BigJoker),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Spade, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Jack),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Spade, Rank.Seven),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Four),
                new Card(Suit.Spade, Rank.Three)
            };
            return SortDescending(cards);
        }

        private static IReadOnlyList<Card> BuildSuitDiscardPool()
        {
            return SortAscending(BuildNonTrumpCards(Suit.Club).Concat(BuildNonTrumpCards(Suit.Diamond)));
        }

        private static IReadOnlyList<Card> BuildTrumpLeadDiscardPool()
        {
            return SortAscending(BuildNonTrumpCards(Suit.Heart).Concat(BuildNonTrumpCards(Suit.Club)).Concat(BuildNonTrumpCards(Suit.Diamond)));
        }

        private static IEnumerable<Card> BuildNonTrumpCards(Suit suit)
        {
            var ranks = new[]
            {
                Rank.Three, Rank.Four, Rank.Five, Rank.Six, Rank.Seven, Rank.Eight,
                Rank.Nine, Rank.Ten, Rank.Jack, Rank.Queen, Rank.King, Rank.Ace
            };

            foreach (var rank in ranks)
            {
                yield return new Card(suit, rank);
                yield return new Card(suit, rank);
            }
        }

        private static List<Card> TakeCards(IEnumerable<Card> pool, int count)
        {
            if (count <= 0)
                return new List<Card>();
            return pool.Take(count).Select(Clone).ToList();
        }

        private static string CardKey(Card card) => $"{card.Suit}-{card.Rank}";

        private static Card Clone(Card card) => new(card.Suit, card.Rank);

        private static List<Card> Cards(params Card[] cards) => cards.Select(Clone).ToList();

        private static List<Card> SortDescending(IEnumerable<Card> cards)
        {
            return cards.OrderByDescending(card => card, Comparer).Select(Clone).ToList();
        }

        private static List<Card> SortAscending(IEnumerable<Card> cards)
        {
            return cards.OrderBy(card => card, Comparer).Select(Clone).ToList();
        }

        public enum LeadSystemKind
        {
            Suit,
            Trump
        }

        public enum AlternateDiscardStyle
        {
            First,
            Second
        }

        public sealed class LeadPatternCase
        {
            public LeadPatternCase(
                string id,
                int n,
                int m,
                int j,
                LeadSystemKind systemKind,
                List<Card>[] hands,
                TrickPlayView[] plays,
                bool[] expectedFollowSuccess,
                string?[] expectedFollowReason,
                int expectedWinner,
                bool expectedThrowSuccess,
                string note)
            {
                Id = id;
                N = n;
                M = m;
                J = j;
                SystemKind = systemKind;
                Hands = hands;
                Plays = plays;
                ExpectedFollowSuccess = expectedFollowSuccess;
                ExpectedFollowReason = expectedFollowReason;
                ExpectedWinner = expectedWinner;
                ExpectedThrowSuccess = expectedThrowSuccess;
                Note = note;
            }

            public string Id { get; }
            public int N { get; }
            public int M { get; }
            public int J { get; }
            public LeadSystemKind SystemKind { get; }
            public List<Card>[] Hands { get; }
            public TrickPlayView[] Plays { get; }
            public bool[] ExpectedFollowSuccess { get; }
            public string?[] ExpectedFollowReason { get; }
            public int ExpectedWinner { get; }
            public bool ExpectedThrowSuccess { get; }
            public string Note { get; }

            public override string ToString() => Id;
        }

        public sealed class TrickPlayView
        {
            public TrickPlayView(int playerIndex, IEnumerable<Card> cards)
            {
                PlayerIndex = playerIndex;
                Cards = cards.Select(Clone).ToList();
            }

            public int PlayerIndex { get; }
            public List<Card> Cards { get; }
        }

        private readonly record struct ComponentSummary(int Singles, int Pairs, int Tractors);
    }
}
