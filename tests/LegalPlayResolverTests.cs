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

            var game = new Game(config);
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
    }
}
