using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// AI记牌系统
    /// 记录已出的牌，推断其他玩家的手牌情况
    /// </summary>
    public class CardMemory
    {
        private readonly GameConfig _config;
        private readonly Dictionary<(Suit, Rank), int> _playedCards;
        private readonly Dictionary<int, HashSet<Suit>> _playerVoidSuits; // 玩家缺门记录
        private readonly int _totalDecks = 2; // 两副牌

        public CardMemory(GameConfig config)
        {
            _config = config;
            _playedCards = new Dictionary<(Suit, Rank), int>();
            _playerVoidSuits = new Dictionary<int, HashSet<Suit>>();
        }

        /// <summary>
        /// 记录一墩牌
        /// </summary>
        public void RecordTrick(List<TrickPlay> plays)
        {
            if (plays == null || plays.Count == 0)
                return;

            var leadCards = plays[0].Cards;
            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;

            foreach (var play in plays)
            {
                // 记录出的牌
                foreach (var card in play.Cards)
                {
                    var key = (card.Suit, card.Rank);
                    if (!_playedCards.ContainsKey(key))
                        _playedCards[key] = 0;
                    _playedCards[key]++;
                }

                // 记录缺门信息（跟牌者没有跟首引花色）
                if (play.PlayerIndex != plays[0].PlayerIndex)
                {
                    bool hasLeadSuit = play.Cards.Any(c =>
                    {
                        if (leadCategory == CardCategory.Trump)
                            return _config.IsTrump(c);
                        else
                            return !_config.IsTrump(c) && c.Suit == leadSuit;
                    });

                    if (!hasLeadSuit)
                    {
                        // 该玩家缺这个花色
                        if (!_playerVoidSuits.ContainsKey(play.PlayerIndex))
                            _playerVoidSuits[play.PlayerIndex] = new HashSet<Suit>();

                        if (leadCategory == CardCategory.Trump)
                        {
                            // 缺主牌（用特殊标记）
                            _playerVoidSuits[play.PlayerIndex].Add((Suit)(-1));
                        }
                        else
                        {
                            _playerVoidSuits[play.PlayerIndex].Add(leadSuit);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 获取某张牌已出的数量
        /// </summary>
        public int GetPlayedCount(Card card)
        {
            var key = (card.Suit, card.Rank);
            return _playedCards.ContainsKey(key) ? _playedCards[key] : 0;
        }

        /// <summary>
        /// 获取某张牌剩余的数量
        /// </summary>
        public int GetRemainingCount(Card card)
        {
            int totalCount = _totalDecks * 1; // 每副牌每种牌1张（除了王）
            if (card.Rank == Rank.BigJoker || card.Rank == Rank.SmallJoker)
                totalCount = _totalDecks * 1; // 每副牌1个大王/小王

            return totalCount - GetPlayedCount(card);
        }

        /// <summary>
        /// 判断某个玩家是否缺某个花色
        /// </summary>
        public bool IsPlayerVoid(int playerPosition, Suit suit)
        {
            if (!_playerVoidSuits.ContainsKey(playerPosition))
                return false;

            return _playerVoidSuits[playerPosition].Contains(suit);
        }

        /// <summary>
        /// 判断某个玩家是否缺主牌
        /// </summary>
        public bool IsPlayerVoidTrump(int playerPosition)
        {
            if (!_playerVoidSuits.ContainsKey(playerPosition))
                return false;

            return _playerVoidSuits[playerPosition].Contains((Suit)(-1));
        }

        /// <summary>
        /// 评估甩牌成功概率
        /// </summary>
        /// <param name="throwCards">要甩的牌</param>
        /// <param name="hand">当前手牌</param>
        /// <param name="myPosition">我的位置</param>
        /// <param name="opponentPositions">对手位置列表</param>
        /// <returns>成功概率（0-1）</returns>
        public double EvaluateThrowSuccessProbability(List<Card> throwCards, List<Card> hand,
            int myPosition, List<int> opponentPositions)
        {
            if (throwCards == null || throwCards.Count == 0)
                return 0;

            var comparer = new CardComparer(_config);
            var throwCategory = _config.GetCardCategory(throwCards[0]);
            var throwSuit = throwCards[0].Suit;

            // 按牌型分组
            var groups = AnalyzeThrowPattern(throwCards);

            double totalProbability = 1.0;

            foreach (var group in groups)
            {
                // 对每个牌型组，评估被压制的概率
                double groupRisk = EvaluateGroupRisk(group, throwCategory, throwSuit,
                    hand, myPosition, opponentPositions, comparer);

                totalProbability *= (1.0 - groupRisk);
            }

            return totalProbability;
        }

        /// <summary>
        /// 分析甩牌的牌型结构
        /// </summary>
        private List<List<Card>> AnalyzeThrowPattern(List<Card> throwCards)
        {
            var groups = new List<List<Card>>();
            var remaining = new List<Card>(throwCards);

            // 找出拖拉机
            var pattern = new CardPattern(throwCards, _config);
            if (pattern.Type == PatternType.Tractor)
            {
                groups.Add(new List<Card>(throwCards));
                return groups;
            }

            // 找出对子
            var pairs = remaining.GroupBy(c => (c.Suit, c.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => g.Take(2).ToList())
                .ToList();

            foreach (var pair in pairs)
            {
                groups.Add(pair);
                foreach (var card in pair)
                    remaining.Remove(card);
            }

            // 剩余单张
            foreach (var card in remaining)
            {
                groups.Add(new List<Card> { card });
            }

            return groups;
        }

        /// <summary>
        /// 评估某个牌型组被压制的风险
        /// </summary>
        private double EvaluateGroupRisk(List<Card> group, CardCategory category, Suit suit,
            List<Card> hand, int myPosition, List<int> opponentPositions, CardComparer comparer)
        {
            if (group.Count == 0)
                return 0;

            var maxCard = group.OrderByDescending(c => c, comparer).First();

            // 计算比这张牌大的牌还有多少张在外面
            int biggerCardsRemaining = CountBiggerCardsRemaining(maxCard, category, suit, hand, comparer);

            if (biggerCardsRemaining == 0)
                return 0; // 没有更大的牌，安全

            // 估算对手持有大牌的概率
            // 简化模型：假设剩余牌均匀分布在对手手中
            int totalOpponents = opponentPositions.Count;

            // 检查对手是否缺门
            int opponentsWithSuit = 0;
            foreach (var pos in opponentPositions)
            {
                if (category == CardCategory.Trump)
                {
                    if (!IsPlayerVoidTrump(pos))
                        opponentsWithSuit++;
                }
                else
                {
                    if (!IsPlayerVoid(pos, suit))
                        opponentsWithSuit++;
                }
            }

            if (opponentsWithSuit == 0)
                return 0; // 所有对手都缺门，安全

            // 简化概率模型：至少有一个对手持有大牌的概率
            // P(至少一个对手有大牌) = 1 - P(所有对手都没有大牌)
            // 假设每个对手有大牌的概率为 biggerCardsRemaining / (剩余牌数 / 对手数)

            // 这里使用简化估算
            double riskPerOpponent = Math.Min(0.8, biggerCardsRemaining * 0.15);
            double totalRisk = 1.0 - Math.Pow(1.0 - riskPerOpponent, opponentsWithSuit);

            return totalRisk;
        }

        /// <summary>
        /// 计算比指定牌大的牌还剩多少张（不在手牌中）
        /// </summary>
        private int CountBiggerCardsRemaining(Card card, CardCategory category, Suit suit,
            List<Card> hand, CardComparer comparer)
        {
            int count = 0;

            // 遍历所有可能的牌
            var allRanks = Enum.GetValues(typeof(Rank)).Cast<Rank>();

            if (category == CardCategory.Trump)
            {
                // 主牌：检查所有主牌
                foreach (var rank in allRanks)
                {
                    // 大小王
                    if (rank == Rank.BigJoker || rank == Rank.SmallJoker)
                    {
                        var testCard = new Card(Suit.Spade, rank);
                        if (comparer.Compare(testCard, card) > 0)
                        {
                            int remaining = GetRemainingCount(testCard);
                            int inHand = hand.Count(c => c.Rank == rank);
                            count += Math.Max(0, remaining - inHand);
                        }
                    }
                    // 级牌
                    else if (rank == _config.LevelRank)
                    {
                        foreach (var s in Enum.GetValues(typeof(Suit)).Cast<Suit>())
                        {
                            var testCard = new Card(s, rank);
                            if (_config.IsTrump(testCard) && comparer.Compare(testCard, card) > 0)
                            {
                                int remaining = GetRemainingCount(testCard);
                                int inHand = hand.Count(c => c.Suit == s && c.Rank == rank);
                                count += Math.Max(0, remaining - inHand);
                            }
                        }
                    }
                    // 主花色其他牌
                    else if (_config.TrumpSuit.HasValue)
                    {
                        var testCard = new Card(_config.TrumpSuit.Value, rank);
                        if (_config.IsTrump(testCard) && comparer.Compare(testCard, card) > 0)
                        {
                            int remaining = GetRemainingCount(testCard);
                            int inHand = hand.Count(c => c.Suit == _config.TrumpSuit.Value && c.Rank == rank);
                            count += Math.Max(0, remaining - inHand);
                        }
                    }
                }
            }
            else
            {
                // 副牌：只检查同花色
                foreach (var rank in allRanks)
                {
                    if (rank == Rank.BigJoker || rank == Rank.SmallJoker)
                        continue;

                    var testCard = new Card(suit, rank);
                    if (!_config.IsTrump(testCard) && comparer.Compare(testCard, card) > 0)
                    {
                        int remaining = GetRemainingCount(testCard);
                        int inHand = hand.Count(c => c.Suit == suit && c.Rank == rank);
                        count += Math.Max(0, remaining - inHand);
                    }
                }
            }

            return count;
        }

        /// <summary>
        /// 重置记牌（新局开始）
        /// </summary>
        public void Reset()
        {
            _playedCards.Clear();
            _playerVoidSuits.Clear();
        }
    }
}
