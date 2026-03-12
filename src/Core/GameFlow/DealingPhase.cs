using System;
using System.Collections.Generic;
using TractorGame.Core.Models;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 发牌阶段
    /// </summary>
    public class DealingPhase
    {
        private readonly Deck _deck;
        private readonly List<Card>[] _playerHands;
        private readonly List<Card> _bottomCards;

        public DealingPhase(Deck deck)
        {
            _deck = deck;
            _playerHands = new List<Card>[4];
            for (int i = 0; i < 4; i++)
            {
                _playerHands[i] = new List<Card>();
            }
            _bottomCards = new List<Card>();
        }

        public void Deal()
        {
            _deck.Shuffle();

            // 发牌：每人25张，底牌8张
            // 先发6张底牌
            for (int i = 0; i < 6; i++)
            {
                _bottomCards.Add(_deck.DrawCard());
            }

            // 轮流发牌给4个玩家，每人25张
            for (int round = 0; round < 25; round++)
            {
                for (int player = 0; player < 4; player++)
                {
                    _playerHands[player].Add(_deck.DrawCard());
                }
            }

            // 最后2张底牌
            for (int i = 0; i < 2; i++)
            {
                _bottomCards.Add(_deck.DrawCard());
            }
        }

        public List<Card> GetPlayerHand(int playerIndex)
        {
            if (playerIndex < 0 || playerIndex >= 4)
                throw new ArgumentOutOfRangeException(nameof(playerIndex));
            return new List<Card>(_playerHands[playerIndex]);
        }

        public List<Card> GetBottomCards()
        {
            return new List<Card>(_bottomCards);
        }
    }
}
