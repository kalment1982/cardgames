using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class TrickJudgeTrumpCutWinnerSuiteTests
    {
        public static IEnumerable<object[]> Cases()
        {
            yield return Case("CUT-S-01", 2,
                new[] { Spade(Rank.Ace) },
                new[] { Spade(Rank.King) },
                new[] { Diamond(Rank.Three) },
                new[] { Spade(Rank.Queen) });

            yield return Case("CUT-S-02", 2,
                new[] { Spade(Rank.Ace) },
                new[] { Diamond(Rank.Three) },
                new[] { Joker(Rank.SmallJoker) },
                new[] { Spade(Rank.King) });

            yield return Case("CUT-P-01", 2,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace) },
                new[] { Spade(Rank.King), Spade(Rank.King) },
                new[] { Diamond(Rank.Three), Diamond(Rank.Three) },
                new[] { Spade(Rank.Queen), Spade(Rank.Queen) });

            yield return Case("CUT-P-02", 0,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace) },
                new[] { Diamond(Rank.Three), Diamond(Rank.Four) },
                new[] { Spade(Rank.King), Spade(Rank.King) },
                new[] { Spade(Rank.Queen), Spade(Rank.Queen) });

            yield return Case("CUT-T-01", 2,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace), Spade(Rank.King), Spade(Rank.King) },
                new[] { Spade(Rank.Queen), Spade(Rank.Queen), Spade(Rank.Jack), Spade(Rank.Jack) },
                new[] { Diamond(Rank.Five), Diamond(Rank.Five), Diamond(Rank.Four), Diamond(Rank.Four) },
                new[] { Spade(Rank.Nine), Spade(Rank.Nine), Spade(Rank.Eight), Spade(Rank.Eight) });

            yield return Case("CUT-T-02", 0,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace), Spade(Rank.King), Spade(Rank.King) },
                new[] { Diamond(Rank.Five), Diamond(Rank.Five), Diamond(Rank.Three), Diamond(Rank.Four) },
                new[] { Spade(Rank.Queen), Spade(Rank.Queen), Spade(Rank.Jack), Spade(Rank.Jack) },
                new[] { Spade(Rank.Nine), Spade(Rank.Nine), Spade(Rank.Eight), Spade(Rank.Eight) });

            yield return Case("CUT-M-01", 1,
                new[] { Spade(Rank.Queen), Spade(Rank.Jack), Spade(Rank.Jack) },
                new[] { Diamond(Rank.Three), Diamond(Rank.Three), Joker(Rank.SmallJoker) },
                new[] { Spade(Rank.Nine), Spade(Rank.Eight), Spade(Rank.Eight) },
                new[] { Spade(Rank.Seven), Spade(Rank.Six), Spade(Rank.Six) });

            yield return Case("CUT-M-04", 0,
                new[] { Spade(Rank.Queen), Spade(Rank.Jack) },
                new[] { Club(Rank.Ace), Heart(Rank.Ace) },
                new[] { Heart(Rank.Ten), Heart(Rank.Five) },
                new[] { Spade(Rank.Five), Diamond(Rank.Four) });

            yield return Case("CUT-M-05", 0,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace), Spade(Rank.King) },
                new[] { Diamond(Rank.Five), Diamond(Rank.Five), Heart(Rank.Nine) },
                new[] { Spade(Rank.Queen), Spade(Rank.Queen), Spade(Rank.Jack) },
                new[] { Spade(Rank.Eight), Spade(Rank.Eight), Spade(Rank.Seven) });

            yield return Case("CUT-R-01", 3,
                new[] { Spade(Rank.Ace) },
                new[] { Diamond(Rank.Three) },
                new[] { Joker(Rank.SmallJoker) },
                new[] { Joker(Rank.BigJoker) });

            yield return Case("CUT-R-02", 2,
                new[] { Spade(Rank.Ace), Spade(Rank.Ace) },
                new[] { Diamond(Rank.Five), Diamond(Rank.Five) },
                new[] { Club(Rank.Two), Club(Rank.Two) },
                new[] { Spade(Rank.King), Spade(Rank.King) });

            yield return Case("TRUMP-L-01", 2,
                new[] { Diamond(Rank.Ace) },
                new[] { Heart(Rank.Two) },
                new[] { Joker(Rank.SmallJoker) },
                new[] { Club(Rank.Two) });

            yield return Case("CUT_THROW_OK_01", 2,
                new[]
                {
                    Spade(Rank.Ace), Spade(Rank.King),
                    Spade(Rank.Queen), Spade(Rank.Queen),
                    Spade(Rank.Jack), Spade(Rank.Jack),
                    Spade(Rank.Nine), Spade(Rank.Nine)
                },
                new[]
                {
                    Heart(Rank.Ace), Heart(Rank.King),
                    Heart(Rank.Queen), Heart(Rank.Queen),
                    Heart(Rank.Jack), Heart(Rank.Jack),
                    Heart(Rank.Nine), Heart(Rank.Nine)
                },
                new[]
                {
                    Joker(Rank.BigJoker), Joker(Rank.SmallJoker),
                    Diamond(Rank.Ace), Diamond(Rank.Ace),
                    Diamond(Rank.King), Diamond(Rank.King),
                    Diamond(Rank.Five), Diamond(Rank.Five)
                },
                new[]
                {
                    Club(Rank.Ace), Club(Rank.King),
                    Club(Rank.Queen), Club(Rank.Queen),
                    Club(Rank.Jack), Club(Rank.Jack),
                    Club(Rank.Nine), Club(Rank.Nine)
                });

            yield return Case("CUT_THROW_OK_02", 0,
                new[]
                {
                    Spade(Rank.Ace), Spade(Rank.King),
                    Spade(Rank.Queen), Spade(Rank.Queen),
                    Spade(Rank.Jack), Spade(Rank.Jack),
                    Spade(Rank.Nine), Spade(Rank.Nine)
                },
                new[]
                {
                    Joker(Rank.SmallJoker),
                    Diamond(Rank.Ten), Diamond(Rank.Ten),
                    Diamond(Rank.Nine), Diamond(Rank.Nine),
                    Diamond(Rank.Six), Diamond(Rank.Five), Diamond(Rank.Four)
                },
                new[]
                {
                    Heart(Rank.Ace), Heart(Rank.King),
                    Heart(Rank.Queen), Heart(Rank.Queen),
                    Heart(Rank.Jack), Heart(Rank.Jack),
                    Heart(Rank.Nine), Heart(Rank.Nine)
                },
                new[]
                {
                    Joker(Rank.BigJoker),
                    Diamond(Rank.Ace), Diamond(Rank.Ace),
                    Diamond(Rank.King), Diamond(Rank.King),
                    Diamond(Rank.Jack), Diamond(Rank.Eight), Diamond(Rank.Seven)
                });

            yield return Case("CUT_THROW_NG_01", 0,
                new[]
                {
                    Spade(Rank.Ace), Spade(Rank.King),
                    Spade(Rank.Queen), Spade(Rank.Queen),
                    Spade(Rank.Jack), Spade(Rank.Jack),
                    Spade(Rank.Nine), Spade(Rank.Nine)
                },
                new[]
                {
                    Spade(Rank.Eight), Diamond(Rank.Ace),
                    Spade(Rank.Ten), Spade(Rank.Ten),
                    Diamond(Rank.King), Diamond(Rank.King),
                    Spade(Rank.Three), Spade(Rank.Three)
                },
                new[]
                {
                    Heart(Rank.Ace), Heart(Rank.King),
                    Heart(Rank.Queen), Heart(Rank.Queen),
                    Heart(Rank.Jack), Heart(Rank.Jack),
                    Heart(Rank.Nine), Heart(Rank.Nine)
                },
                new[]
                {
                    Club(Rank.Ace), Club(Rank.King),
                    Club(Rank.Queen), Club(Rank.Queen),
                    Club(Rank.Jack), Club(Rank.Jack),
                    Club(Rank.Nine), Club(Rank.Nine)
                });

            yield return Case("CUT_THROW_NG_02", 0,
                new[]
                {
                    Spade(Rank.Ace), Spade(Rank.King),
                    Spade(Rank.Queen), Spade(Rank.Queen),
                    Spade(Rank.Jack), Spade(Rank.Jack),
                    Spade(Rank.Nine), Spade(Rank.Nine)
                },
                new[]
                {
                    Joker(Rank.BigJoker),
                    Diamond(Rank.Ace),
                    Diamond(Rank.King), Diamond(Rank.King),
                    Diamond(Rank.Ten), Diamond(Rank.Nine),
                    Diamond(Rank.Eight), Diamond(Rank.Seven)
                },
                new[]
                {
                    Heart(Rank.Ace), Heart(Rank.King),
                    Heart(Rank.Queen), Heart(Rank.Queen),
                    Heart(Rank.Jack), Heart(Rank.Jack),
                    Heart(Rank.Nine), Heart(Rank.Nine)
                },
                new[]
                {
                    Club(Rank.Ace), Club(Rank.King),
                    Club(Rank.Queen), Club(Rank.Queen),
                    Club(Rank.Jack), Club(Rank.Jack),
                    Club(Rank.Nine), Club(Rank.Nine)
                });
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void TrumpCutWinnerSuite_FollowAndWinner_MatchExpectation(string id, TestCaseData data)
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Diamond
            };

            var validator = new FollowValidator(config);
            var judge = new TrickJudge(config);
            var lead = data.Plays[0].Cards;

            for (int playerIndex = 1; playerIndex < data.Plays.Count; playerIndex++)
            {
                var hand = new List<Card>(data.Hands[playerIndex]);
                var follow = data.Plays[playerIndex].Cards;
                var result = validator.IsValidFollowEx(hand, lead, follow);
                Assert.True(result.Success, $"{id} player{playerIndex} follow should be valid, actual={result.ReasonCode}");
            }

            var winner = judge.DetermineWinner(data.Plays);
            Assert.Equal(data.ExpectedWinner, winner);
        }

        private static object[] Case(string id, int expectedWinner, IEnumerable<Card> south, IEnumerable<Card> east, IEnumerable<Card> north, IEnumerable<Card> west)
        {
            var plays = new List<TrickPlay>
            {
                new(0, new List<Card>(south)),
                new(1, new List<Card>(east)),
                new(2, new List<Card>(north)),
                new(3, new List<Card>(west))
            };

            var hands = new List<List<Card>>
            {
                new(plays[0].Cards),
                new(plays[1].Cards),
                new(plays[2].Cards),
                new(plays[3].Cards)
            };

            return new object[] { id, new TestCaseData(expectedWinner, hands, plays) };
        }

        private static Card Spade(Rank rank) => new(Suit.Spade, rank);
        private static Card Heart(Rank rank) => new(Suit.Heart, rank);
        private static Card Club(Rank rank) => new(Suit.Club, rank);
        private static Card Diamond(Rank rank) => new(Suit.Diamond, rank);
        private static Card Joker(Rank rank) => new(Suit.Joker, rank);

        public sealed class TestCaseData
        {
            public int ExpectedWinner { get; }
            public List<List<Card>> Hands { get; }
            public List<TrickPlay> Plays { get; }

            public TestCaseData(int expectedWinner, List<List<Card>> hands, List<TrickPlay> plays)
            {
                ExpectedWinner = expectedWinner;
                Hands = hands;
                Plays = plays;
            }
        }
    }
}
