using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.GameFlow
{
    /// <summary>
    /// 游戏主控制器
    /// </summary>
    public class Game
    {
        private readonly GameState _state;
        private readonly GameConfig _config;
        private readonly Deck _deck;
        private DealingPhase _dealing;
        private TrumpBidding _bidding;
        private BottomBurying _burying;
        private readonly TrickJudge _judge;
        private readonly ScoreCalculator _scoreCalc;

        private List<TrickPlay> _currentTrick;
        private int _trickLeader;
        private List<Card> _lastTrickCards; // 最后一墩的牌（用于抠底计算）

        public GameState State => _state;

        public Game(int seed = 0)
        {
            _state = new GameState
            {
                DealerIndex = 0,
                LevelRank = Rank.Two,
                Phase = GamePhase.Dealing
            };
            _config = new GameConfig { LevelRank = Rank.Two };
            _deck = seed > 0 ? new Deck(seed) : new Deck();
            _judge = new TrickJudge(_config);
            _scoreCalc = new ScoreCalculator(_config);
            _currentTrick = new List<TrickPlay>();
        }

        public void StartGame()
        {
            // 发牌
            _dealing = new DealingPhase(_deck);
            _dealing.Deal();
            for (int i = 0; i < 4; i++)
            {
                _state.PlayerHands[i] = _dealing.GetPlayerHand(i);
            }
            _state.Phase = GamePhase.Bidding;
        }

        public bool BidTrump(int playerIndex, List<Card> cards)
        {
            if (_state.Phase != GamePhase.Bidding)
                return false;

            if (_bidding == null)
                _bidding = new TrumpBidding();

            return _bidding.TryBid(playerIndex, _state.LevelRank, cards);
        }

        public void FinalizeTrump(Suit? trumpSuit = null)
        {
            if (trumpSuit.HasValue)
            {
                _state.TrumpSuit = trumpSuit;
            }
            else if (_bidding != null && _bidding.TrumpSuit.HasValue)
            {
                _state.TrumpSuit = _bidding.TrumpSuit;
            }
            else
            {
                _state.TrumpSuit = Suit.Spade; // 默认黑桃
            }

            _config.TrumpSuit = _state.TrumpSuit;
            _state.Phase = GamePhase.Burying;
        }

        public bool BuryBottom(List<Card> cardsToBury)
        {
            if (_state.Phase != GamePhase.Burying)
                return false;

            var bottom = _dealing.GetBottomCards();

            // 底牌加入庄家手牌
            _state.PlayerHands[_state.DealerIndex].AddRange(bottom);

            _burying = new BottomBurying(bottom);

            if (_burying.BuryCards(_state.PlayerHands[_state.DealerIndex], cardsToBury))
            {
                _state.BuriedCards = _burying.BuriedCards;

                // 从庄家手牌中移除扣底的牌
                foreach (var card in cardsToBury)
                {
                    _state.PlayerHands[_state.DealerIndex].Remove(card);
                }

                _state.Phase = GamePhase.Playing;
                _state.CurrentPlayer = _state.DealerIndex;
                _trickLeader = _state.DealerIndex;
                return true;
            }
            return false;
        }

        public bool PlayCards(int playerIndex, List<Card> cards)
        {
            if (_state.Phase != GamePhase.Playing)
                return false;
            if (playerIndex != _state.CurrentPlayer)
                return false;

            var validator = _currentTrick.Count == 0
                ? new PlayValidator(_config)
                : (object)new FollowValidator(_config);

            bool valid = _currentTrick.Count == 0
                ? ((PlayValidator)validator).IsValidPlay(_state.PlayerHands[playerIndex], cards)
                : ((FollowValidator)validator).IsValidFollow(
                    _state.PlayerHands[playerIndex],
                    _currentTrick[0].Cards,
                    cards);

            if (!valid)
                return false;

            // 出牌
            _currentTrick.Add(new TrickPlay(playerIndex, cards));
            foreach (var card in cards)
            {
                _state.PlayerHands[playerIndex].Remove(card);
            }

            // 下一个玩家
            _state.CurrentPlayer = (playerIndex + 1) % 4;

            // 一墩结束
            if (_currentTrick.Count == 4)
            {
                FinishTrick();
            }

            return true;
        }

        private void FinishTrick()
        {
            int winner = _judge.DetermineWinner(_currentTrick);

            // 计算得分
            int score = 0;
            foreach (var play in _currentTrick)
            {
                score += play.Cards.Sum(c => c.Score);
            }

            // 闲家得分
            if (winner % 2 != _state.DealerIndex % 2)
            {
                _state.DefenderScore += score;
            }

            // 保存最后一墩首张牌（用于抠底倍数计算）
            _lastTrickCards = _currentTrick[0].Cards;

            _currentTrick.Clear();
            _state.CurrentPlayer = winner;
            _trickLeader = winner;

            // 检查游戏是否结束
            if (_state.PlayerHands[0].Count == 0)
            {
                FinishGame(winner);
            }
        }

        private void FinishGame(int lastWinner)
        {
            // 抠底：最后一墩赢家是闲家队，则闲家得底牌分数
            if (lastWinner % 2 != _state.DealerIndex % 2)
            {
                // 用最后一墩的首张牌作为牌型参考
                var lastTrickLead = _lastTrickCards ?? new List<Card> { new Card(Suit.Spade, Rank.Two) };
                int bottomScore = _scoreCalc.CalculateBottomScore(_state.BuriedCards, lastTrickLead);
                _state.DefenderScore += bottomScore;
            }

            _state.Phase = GamePhase.Finished;
        }
    }
}
