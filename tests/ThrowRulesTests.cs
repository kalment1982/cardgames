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
        public void ThrowValidator_MixedPairDominant_IgnoresSinglesComparison()
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

            Assert.True(validator.IsThrowSuccessful(throwCards, followPlays));
        }

        [Fact]
        public void Game_PlayCardsEx_ThrowFail_FallsBackToSmallestSingle()
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
            Assert.Equal(1, game.State.CurrentPlayer);
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
