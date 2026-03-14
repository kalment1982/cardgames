using System;
using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.GameFlow
{
    public sealed class DealStepResult
    {
        public int StepIndex { get; set; }
        public bool IsBottomCard { get; set; }
        public int PlayerIndex { get; set; } = -1;
        public int PlayerCardCount { get; set; }
        public Card Card { get; set; } = new Card(Suit.Spade, Rank.Two);
        public int RemainingDeck { get; set; }
    }

    /// <summary>
    /// 发牌阶段（逐张发牌）
    /// </summary>
    public class DealingPhase
    {
        private readonly Deck _deck;
        private readonly List<Card>[] _playerHands;
        private readonly List<Card> _bottomCards;
        private int _dealStep;

        private const int BottomHeadCount = 6;
        private const int BottomTailCount = 2;
        private const int PlayerCardCount = 25;
        private const int PlayerCount = 4;
        private const int TotalCards = 108;

        public DealingPhase(Deck deck)
        {
            _deck = deck;
            _playerHands = new List<Card>[PlayerCount];
            for (int i = 0; i < PlayerCount; i++)
            {
                _playerHands[i] = new List<Card>();
            }
            _bottomCards = new List<Card>();
            _dealStep = 0;
            _deck.Shuffle();
        }

        public bool IsComplete => _dealStep >= TotalCards;
        public int DealStep => _dealStep;

        public DealStepResult DealNext()
        {
            if (IsComplete)
                throw new InvalidOperationException("发牌已完成");

            var card = _deck.DrawCard();
            var sequence = _dealStep;
            var isBottom = IsBottomStep(sequence);
            var result = new DealStepResult
            {
                StepIndex = sequence + 1,
                IsBottomCard = isBottom,
                Card = card,
                RemainingDeck = _deck.RemainingCards
            };

            if (isBottom)
            {
                _bottomCards.Add(card);
            }
            else
            {
                var playerIndex = ResolvePlayerIndex(sequence);
                _playerHands[playerIndex].Add(card);
                result.PlayerIndex = playerIndex;
                result.PlayerCardCount = _playerHands[playerIndex].Count;
            }

            _dealStep++;
            return result;
        }

        public List<Card> GetPlayerHand(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= PlayerCount)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            return new List<Card>(_playerHands[playerIndex]);
        }

        public List<Card> GetBottomCards()
        {
            return new List<Card>(_bottomCards);
        }

        private static bool IsBottomStep(int sequence)
        {
            if (sequence < BottomHeadCount)
                return true;

            var mainDealEndExclusive = BottomHeadCount + PlayerCardCount * PlayerCount;
            return sequence >= mainDealEndExclusive && sequence < TotalCards;
        }

        private static int ResolvePlayerIndex(int sequence)
        {
            var playerSequence = sequence - BottomHeadCount;
            return playerSequence % PlayerCount;
        }
    }
}
