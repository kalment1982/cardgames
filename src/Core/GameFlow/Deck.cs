using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 牌堆管理
    /// </summary>
    public class Deck
    {
        private List<Card> _cards = new List<Card>();
        private Random _random = new Random();

        public Deck()
        {
            _random = new Random();
            Initialize();
        }

        public Deck(int seed)
        {
            _random = new Random(seed);
            Initialize();
        }

        private void Initialize()
        {
            _cards = new List<Card>();

            // 2副牌，每副54张
            for (int deck = 0; deck < 2; deck++)
            {
                // 大小王
                _cards.Add(new Card(Suit.Joker, Rank.BigJoker));
                _cards.Add(new Card(Suit.Joker, Rank.SmallJoker));

                // 四种花色，每种13张
                foreach (Suit suit in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
                {
                    foreach (Rank rank in Enum.GetValues(typeof(Rank)))
                    {
                        if (rank != Rank.SmallJoker && rank != Rank.BigJoker)
                        {
                            _cards.Add(new Card(suit, rank));
                        }
                    }
                }
            }
        }

        public void Shuffle()
        {
            int n = _cards.Count;
            for (int i = n - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                var temp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = temp;
            }
        }

        public Card DrawCard()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("牌堆已空");

            var card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }

        public int RemainingCards => _cards.Count;
    }
}
