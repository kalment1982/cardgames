using Xunit;
using System.Collections.Generic;
using TractorGame.Core.Models;
using TractorGame.Core.GameFlow;
using TractorGame.Core.Logging;

namespace TractorGame.Tests
{
    public class GameTests
    {
        [Fact]
        public void StartGame_InitializesCorrectly()
        {
            var game = new Game(1);
            game.StartGame();

            Assert.Equal(GamePhase.Bidding, game.State.Phase);
            Assert.False(game.IsDealingComplete);
            Assert.Equal(0, game.State.PlayerHands[0].Count);
            Assert.Equal(0, game.State.PlayerHands[1].Count);
        }

        [Fact]
        public void BidTrump_ValidBid_Success()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);

            var cards = new List<Card> { new Card(Suit.Spade, Rank.Two) };
            bool result = game.BidTrump(0, cards);

            Assert.True(result);
        }

        [Fact]
        public void FinalizeTrump_SetsTrumpSuit()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Heart);

            Assert.Equal(Suit.Heart, game.State.TrumpSuit);
            Assert.Equal(GamePhase.Burying, game.State.Phase);
        }

        [Fact]
        public void BuryBottom_Valid_Success()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var hand = game.State.PlayerHands[0];
            var cardsToBury = hand.GetRange(0, 8);

            bool result = game.BuryBottom(cardsToBury);

            Assert.True(result);
            Assert.Equal(GamePhase.Playing, game.State.Phase);
            Assert.Equal(25, game.State.PlayerHands[0].Count);
        }

        [Fact]
        public void PlayCards_ValidPlay_Success()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            // 扣底：选择后8张牌
            var hand = game.State.PlayerHands[0];
            var cardsToBury = hand.GetRange(hand.Count - 8, 8);
            bool buryResult = game.BuryBottom(cardsToBury);

            Assert.True(buryResult);
            Assert.Equal(GamePhase.Playing, game.State.Phase);
            Assert.Equal(0, game.State.CurrentPlayer);
            Assert.Equal(25, game.State.PlayerHands[0].Count);

            // 出牌：确保卡牌在手中
            var cardToPlay = game.State.PlayerHands[0][0];
            Assert.Contains(cardToPlay, game.State.PlayerHands[0]);

            bool result = game.PlayCards(0, new List<Card> { cardToPlay });

            Assert.True(result);
            Assert.Equal(24, game.State.PlayerHands[0].Count);
            Assert.Equal(1, game.State.CurrentPlayer);
        }

        [Fact]
        public void PlayCards_WrongPlayer_Fails()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var hand = game.State.PlayerHands[0];
            game.BuryBottom(hand.GetRange(0, 8));

            var card = game.State.PlayerHands[1][0];
            bool result = game.PlayCards(1, new List<Card> { card });

            Assert.False(result);
        }

        [Fact]
        public void LastCompletedTrick_AvailableAfterTrickFinished()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);
            game.FinalizeTrump(Suit.Spade);

            var hand = game.State.PlayerHands[0];
            Assert.True(game.BuryBottom(hand.GetRange(0, 8)));

            game.State.PlayerHands[0] = new List<Card> { new Card(Suit.Heart, Rank.Ace) };
            game.State.PlayerHands[1] = new List<Card> { new Card(Suit.Heart, Rank.King) };
            game.State.PlayerHands[2] = new List<Card> { new Card(Suit.Heart, Rank.Queen) };
            game.State.PlayerHands[3] = new List<Card> { new Card(Suit.Heart, Rank.Jack) };
            game.State.Phase = GamePhase.Playing;
            game.State.CurrentPlayer = 0;
            game.CurrentTrick.Clear();

            Assert.True(game.PlayCards(0, new List<Card> { new Card(Suit.Heart, Rank.Ace) }));
            Assert.True(game.PlayCards(1, new List<Card> { new Card(Suit.Heart, Rank.King) }));
            Assert.True(game.PlayCards(2, new List<Card> { new Card(Suit.Heart, Rank.Queen) }));
            Assert.True(game.PlayCards(3, new List<Card> { new Card(Suit.Heart, Rank.Jack) }));

            Assert.Empty(game.CurrentTrick);
            Assert.Equal(1, game.LastCompletedTrickNo);
            Assert.Equal(4, game.LastCompletedTrick.Count);
            Assert.Equal(0, game.LastCompletedTrick[0].PlayerIndex);
            Assert.Equal(new Card(Suit.Heart, Rank.Ace), game.LastCompletedTrick[0].Cards[0]);
            Assert.Equal(3, game.LastCompletedTrick[3].PlayerIndex);
            Assert.Equal(new Card(Suit.Heart, Rank.Jack), game.LastCompletedTrick[3].Cards[0]);
        }

        [Fact]
        public void CanBidTrumpEx_ReturnsPriorityTooLow_WhenSingleCannotOvertakeSingle()
        {
            var game = new Game(1);
            game.StartGame();
            DealToEnd(game);

            Assert.True(game.BidTrump(1, new List<Card> { new Card(Suit.Heart, Rank.Two) }));

            var check = game.CanBidTrumpEx(0, new List<Card> { new Card(Suit.Spade, Rank.Two) });

            Assert.False(check.Success);
            Assert.Equal(ReasonCodes.BidPriorityTooLow, check.ReasonCode);
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
