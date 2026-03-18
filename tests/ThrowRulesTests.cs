using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class ThrowRulesTests
    {
        [Fact]
        public void PlayValidator_ReturnsThrowNotMax_WhenOpponentCanBeatDominantPair()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var validator = new PlayValidator(config);

            var hand = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Ten)
            };

            var otherHands = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Nine)
                }
            };

            var result = validator.IsValidPlayEx(hand, hand, otherHands);

            Assert.False(result.Success);
            Assert.Equal(ReasonCodes.ThrowNotMax, result.ReasonCode);
        }

        [Fact]
        public void ThrowValidator_AllSingles_FailsWhenOpponentCanCoverWeakestSingle()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var validator = new ThrowValidator(config);

            var throwCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King),
                new Card(Suit.Spade, Rank.Nine)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.Queen),
                    new Card(Suit.Spade, Rank.Jack),
                    new Card(Suit.Spade, Rank.Ten)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void ThrowValidator_MixedThrow_FailsWhenAnySingleComponentCanBeBeaten()
        {
            var config = new GameConfig { LevelRank = Rank.Five, TrumpSuit = Suit.Heart };
            var validator = new ThrowValidator(config);

            var throwCards = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Spade, Rank.Four)
            };
            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.King),
                    new Card(Suit.Spade, Rank.Queen),
                    new Card(Suit.Spade, Rank.Jack)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void ThrowValidator_MixedThrow_FailsWhenSingleInThrowCanBeBeaten_EvenIfPairsCannot()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var validator = new ThrowValidator(config);

            var throwCards = new List<Card>
            {
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Seven)
            };

            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Heart, Rank.Queen),
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Seven),
                    new Card(Suit.Heart, Rank.Five),
                    new Card(Suit.Heart, Rank.Five)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void ThrowValidator_MixedThrow_FailsWhenDifferentFollowersBlockDifferentComponents()
        {
            var config = new GameConfig { LevelRank = Rank.Two, TrumpSuit = Suit.Diamond };
            var validator = new ThrowValidator(config);

            var throwCards = new List<Card>
            {
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.Eight),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Five)
            };

            var followPlays = new List<List<Card>>
            {
                new List<Card>
                {
                    new Card(Suit.Heart, Rank.Ace),
                    new Card(Suit.Club, Rank.Three)
                },
                new List<Card>
                {
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Nine),
                    new Card(Suit.Heart, Rank.Four),
                    new Card(Suit.Heart, Rank.Three)
                }
            };

            Assert.False(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void Game_PlayCardsEx_MixedThrowBlockedBySingle_FallsBackToMinimalLegalPattern()
        {
            var game = new Game(seed: 1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Diamond);

            var dealerCardsToBury = game.State.PlayerHands[0].Take(8).ToList();
            Assert.True(game.BuryBottom(dealerCardsToBury));

            var throwAttempt = new List<Card>
            {
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Seven)
            };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Five),
                new Card(Suit.Heart, Rank.Five)
            };
            game.State.PlayerHands[2] = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King)
            };
            game.State.PlayerHands[3] = new List<Card>
            {
                new Card(Suit.Spade, Rank.Ace),
                new Card(Suit.Spade, Rank.King)
            };

            game.CurrentTrick.Clear();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;

            var result = game.PlayCardsEx(0, throwAttempt);

            Assert.True(result.Success);
            Assert.Single(game.CurrentTrick);
            Assert.Single(game.CurrentTrick[0].Cards);
            Assert.Equal(new Card(Suit.Heart, Rank.Seven), game.CurrentTrick[0].Cards[0]);
            Assert.Equal(5, game.State.PlayerHands[0].Count);
            Assert.Contains(new Card(Suit.Heart, Rank.Ten), game.State.PlayerHands[0]);
            Assert.Equal(3, game.State.CurrentPlayer);
        }

        [Fact]
        public void Game_PlayCardsEx_ThrowFail_FallsBackToMinimalLegalPattern()
        {
            var game = new Game(seed: 1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var dealerCardsToBury = game.State.PlayerHands[0].Take(8).ToList();
            Assert.True(game.BuryBottom(dealerCardsToBury));

            var throwAttempt = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten)
            };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card>
            {
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Nine)
            };
            game.State.PlayerHands[2] = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King)
            };
            game.State.PlayerHands[3] = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.King)
            };

            game.CurrentTrick.Clear();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;

            var result = game.PlayCardsEx(0, throwAttempt);

            Assert.True(result.Success);
            Assert.Single(game.CurrentTrick);
            Assert.Single(game.CurrentTrick[0].Cards);
            Assert.Equal(new Card(Suit.Heart, Rank.Queen), game.CurrentTrick[0].Cards[0]);
            Assert.Equal(4, game.State.PlayerHands[0].Count);
            Assert.Contains(new Card(Suit.Heart, Rank.Ten), game.State.PlayerHands[0]);
            Assert.Equal(3, game.State.CurrentPlayer);
        }

        [Fact]
        public void Game_PlayCardsEx_ThrowFail_WhenNoSingles_FallsBackToSmallestPair()
        {
            var game = new Game(seed: 1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var dealerCardsToBury = game.State.PlayerHands[0].Take(8).ToList();
            Assert.True(game.BuryBottom(dealerCardsToBury));

            var throwAttempt = new List<Card>
            {
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Queen),
                new Card(Suit.Heart, Rank.Nine),
                new Card(Suit.Heart, Rank.Nine)
            };

            game.State.PlayerHands[0] = throwAttempt.ToList();
            game.State.PlayerHands[1] = new List<Card>
            {
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Heart, Rank.Ace),
                new Card(Suit.Heart, Rank.King),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Three)
            };
            game.State.PlayerHands[2] = new List<Card>
            {
                new Card(Suit.Club, Rank.Ace),
                new Card(Suit.Club, Rank.King)
            };
            game.State.PlayerHands[3] = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.King)
            };

            game.CurrentTrick.Clear();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;

            var result = game.PlayCardsEx(0, throwAttempt);

            Assert.True(result.Success);
            Assert.Single(game.CurrentTrick);
            Assert.Equal(2, game.CurrentTrick[0].Cards.Count);
            Assert.All(game.CurrentTrick[0].Cards, card => Assert.Equal(new Card(Suit.Heart, Rank.Nine), card));
            Assert.Equal(4, game.State.PlayerHands[0].Count);
            Assert.Contains(new Card(Suit.Heart, Rank.King), game.State.PlayerHands[0]);
            Assert.Contains(new Card(Suit.Heart, Rank.Queen), game.State.PlayerHands[0]);
            Assert.Equal(3, game.State.CurrentPlayer);
        }

        private static void DealToEnd(Game game)
        {
            while (!game.IsDealingComplete)
            {
                var step = game.DealNextCardEx();
                Assert.True(step.Success);
            }
        }
    }
}
