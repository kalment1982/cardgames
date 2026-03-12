using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;
using TractorGame.Core.AI;

namespace TractorGame.Tests
{
    public class GameSimulationTests
    {
        [Fact]
        public void FullGame_Setup_CompletesSuccessfully()
        {
            var game = new Game(1);

            // 发牌
            game.StartGame();
            Assert.Equal(GamePhase.Bidding, game.State.Phase);
            Assert.Equal(25, game.State.PlayerHands[0].Count);
            Assert.Equal(25, game.State.PlayerHands[1].Count);
            Assert.Equal(25, game.State.PlayerHands[2].Count);
            Assert.Equal(25, game.State.PlayerHands[3].Count);

            // 亮主
            game.FinalizeTrump(Suit.Spade);
            Assert.Equal(GamePhase.Burying, game.State.Phase);
            Assert.Equal(Suit.Spade, game.State.TrumpSuit);

            // 扣底
            var cardsToBury = game.State.PlayerHands[0].GetRange(0, 8);
            bool buryResult = game.BuryBottom(cardsToBury);
            Assert.True(buryResult);
            Assert.Equal(GamePhase.Playing, game.State.Phase);
            Assert.Equal(25, game.State.PlayerHands[0].Count);
            Assert.Equal(8, game.State.BuriedCards.Count);

            // 首家出牌
            var card = game.State.PlayerHands[0][0];
            bool playResult = game.PlayCards(0, new List<Card> { card });
            Assert.True(playResult);
            Assert.Equal(24, game.State.PlayerHands[0].Count);
            Assert.Equal(1, game.State.CurrentPlayer);
        }
    }
}
