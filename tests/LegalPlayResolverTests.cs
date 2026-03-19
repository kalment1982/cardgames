using System.Collections.Generic;
using TractorGame.Core.AI;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;
using Xunit;

namespace TractorGame.Tests
{
    public class LegalPlayResolverTests
    {
        [Fact]
        public void TryResolve_FollowFindsLegalDiamondResponse_ForThreePairTractorLead()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Club
            };

            var game = new Game();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 2;
            game.State.DealerIndex = 1;
            game.State.LevelRank = Rank.Two;
            game.State.TrumpSuit = Suit.Club;

            game.State.PlayerHands[2] = new List<Card>
            {
                new Card(Suit.Diamond, Rank.Queen), new Card(Suit.Diamond, Rank.Queen),
                new Card(Suit.Club, Rank.Six),
                new Card(Suit.Spade, Rank.Eight),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Club, Rank.Seven),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Three), new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Diamond, Rank.Six),
                new Card(Suit.Diamond, Rank.Two),
                new Card(Suit.Diamond, Rank.Ten),
                new Card(Suit.Diamond, Rank.Five),
                new Card(Suit.Diamond, Rank.Four)
            };

            game.CurrentTrick.Add(new TrickPlay(1, new List<Card>
            {
                new Card(Suit.Diamond, Rank.Nine), new Card(Suit.Diamond, Rank.Nine),
                new Card(Suit.Diamond, Rank.Eight), new Card(Suit.Diamond, Rank.Eight),
                new Card(Suit.Diamond, Rank.Seven), new Card(Suit.Diamond, Rank.Seven)
            }));

            var ok = LegalPlayResolver.TryResolve(game, 2, config, out var cards);
            var validator = new FollowValidator(config);

            Assert.True(ok);
            Assert.Equal(6, cards.Count);
            Assert.True(validator.IsValidFollow(game.State.PlayerHands[2], game.CurrentTrick[0].Cards, cards));
        }

        [Fact]
        public void TryResolve_FollowFindsLegalTrumpResponse_ForJokerMixedTrumpLead()
        {
            var config = new GameConfig
            {
                LevelRank = Rank.Two,
                TrumpSuit = Suit.Spade
            };

            var game = new Game();
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 2;
            game.State.DealerIndex = 0;
            game.State.LevelRank = Rank.Two;
            game.State.TrumpSuit = Suit.Spade;

            game.State.PlayerHands[2] = new List<Card>
            {
                new Card(Suit.Club, Rank.Two),
                new Card(Suit.Heart, Rank.Two),
                new Card(Suit.Spade, Rank.Queen),
                new Card(Suit.Spade, Rank.Ten),
                new Card(Suit.Spade, Rank.Nine),
                new Card(Suit.Spade, Rank.Six),
                new Card(Suit.Spade, Rank.Five),
                new Card(Suit.Spade, Rank.Three),
                new Card(Suit.Club, Rank.Eight),
                new Card(Suit.Heart, Rank.Seven),
                new Card(Suit.Heart, Rank.Six),
                new Card(Suit.Heart, Rank.Four),
                new Card(Suit.Heart, Rank.Three),
                new Card(Suit.Diamond, Rank.Ace),
                new Card(Suit.Diamond, Rank.Jack),
                new Card(Suit.Diamond, Rank.Three),
                new Card(Suit.Heart, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten),
                new Card(Suit.Club, Rank.Jack),
                new Card(Suit.Heart, Rank.Ten)
            };

            game.CurrentTrick.Add(new TrickPlay(3, new List<Card>
            {
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.SmallJoker),
                new Card(Suit.Joker, Rank.BigJoker)
            }));

            var ok = LegalPlayResolver.TryResolve(game, 2, config, out var cards);
            var validator = new FollowValidator(config);

            Assert.True(ok);
            Assert.Equal(3, cards.Count);
            Assert.All(cards, card => Assert.True(config.IsTrump(card)));
            Assert.True(validator.IsValidFollow(game.State.PlayerHands[2], game.CurrentTrick[0].Cards, cards));
        }
    }
}
