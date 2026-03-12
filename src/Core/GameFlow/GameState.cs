using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.GameFlow
{
    public enum GamePhase
    {
        Dealing,
        Bidding,
        Burying,
        Playing,
        Finished
    }

    public class GameState
    {
        public int DealerIndex { get; set; }
        public Rank LevelRank { get; set; }
        public Suit? TrumpSuit { get; set; }
        public List<Card>[] PlayerHands { get; set; }
        public List<Card> BuriedCards { get; set; }
        public int DefenderScore { get; set; }
        public int CurrentPlayer { get; set; }
        public GamePhase Phase { get; set; }

        public GameState()
        {
            PlayerHands = new List<Card>[4];
            for (int i = 0; i < 4; i++)
            {
                PlayerHands[i] = new List<Card>();
            }
            BuriedCards = new List<Card>();
        }
    }
}