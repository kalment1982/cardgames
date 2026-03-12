using System;

namespace TractorGame.Core.Models
{
    /// <summary>
    /// 卡牌类
    /// </summary>
    public class Card : IEquatable<Card>
    {
        public Suit Suit { get; }
        public Rank Rank { get; }

        public Card(Suit suit, Rank rank)
        {
            Suit = suit;
            Rank = rank;
        }

        /// <summary>
        /// 是否为王
        /// </summary>
        public bool IsJoker => Rank == Rank.SmallJoker || Rank == Rank.BigJoker;

        /// <summary>
        /// 是否为分牌
        /// </summary>
        public bool IsScoreCard => Rank == Rank.Five || Rank == Rank.Ten || Rank == Rank.King;

        /// <summary>
        /// 获取分值
        /// </summary>
        public int Score
        {
            get
            {
                if (Rank == Rank.Five) return 5;
                if (Rank == Rank.Ten || Rank == Rank.King) return 10;
                return 0;
            }
        }

        public bool Equals(Card other)
        {
            if (other == null) return false;
            return Suit == other.Suit && Rank == other.Rank;
        }

        public override bool Equals(object obj) => Equals(obj as Card);

        public override int GetHashCode() => HashCode.Combine(Suit, Rank);

        public override string ToString()
        {
            if (Rank == Rank.SmallJoker) return "小王";
            if (Rank == Rank.BigJoker) return "大王";

            string suitStr = Suit switch
            {
                Suit.Spade => "♠",
                Suit.Heart => "♥",
                Suit.Club => "♣",
                Suit.Diamond => "♦",
                _ => ""
            };

            string rankStr = Rank switch
            {
                Rank.Jack => "J",
                Rank.Queen => "Q",
                Rank.King => "K",
                Rank.Ace => "A",
                _ => ((int)Rank).ToString()
            };

            return $"{suitStr}{rankStr}";
        }
    }
}
