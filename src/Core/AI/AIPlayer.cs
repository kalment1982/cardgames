using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI
{
    /// <summary>
    /// AI玩家角色
    /// </summary>
    public enum AIRole
    {
        /// <summary>坐庄玩家（拥有特权）</summary>
        Dealer,
        /// <summary>庄家队友</summary>
        DealerPartner,
        /// <summary>闲家</summary>
        Opponent
    }

    /// <summary>
    /// AI难度等级
    /// </summary>
    public enum AIDifficulty
    {
        /// <summary>简单（随机性30-40%）</summary>
        Easy = 1,
        /// <summary>中等（随机性15-25%）</summary>
        Medium = 3,
        /// <summary>困难（随机性5-10%）</summary>
        Hard = 6,
        /// <summary>专家（随机性0-5%）</summary>
        Expert = 9
    }

    /// <summary>
    /// AI玩家
    /// </summary>
    public class AIPlayer
    {
        private readonly GameConfig _config;
        private readonly Random _random;
        private readonly AIDifficulty _difficulty;
        private readonly CardMemory _memory;
        private readonly AIStrategyParameters _strategy;

        public AIPlayer(
            GameConfig config,
            AIDifficulty difficulty = AIDifficulty.Medium,
            int seed = 0,
            AIStrategyParameters? strategyParameters = null)
        {
            _config = config;
            _difficulty = difficulty;
            _random = seed > 0 ? new Random(seed) : new Random();
            _memory = new CardMemory(config);
            _strategy = (strategyParameters ?? AIStrategyParameters.CreatePreset(difficulty)).Normalize();
        }

        /// <summary>
        /// 记录一墩牌（用于记牌系统）
        /// </summary>
        public void RecordTrick(List<TrickPlay> plays)
        {
            // 简单难度不记牌
            if (_difficulty == AIDifficulty.Easy)
                return;

            _memory.RecordTrick(plays);
        }

        /// <summary>
        /// 重置记牌（新局开始）
        /// </summary>
        public void ResetMemory()
        {
            _memory.Reset();
        }

        /// <summary>
        /// 获取随机决策概率（根据难度）
        /// </summary>
        private double GetRandomnessRate()
        {
            return _difficulty switch
            {
                AIDifficulty.Easy => _strategy.EasyRandomnessRate,
                AIDifficulty.Medium => _strategy.MediumRandomnessRate,
                AIDifficulty.Hard => _strategy.HardRandomnessRate,
                AIDifficulty.Expert => _strategy.ExpertRandomnessRate,
                _ => _strategy.MediumRandomnessRate
            };
        }

        /// <summary>
        /// 判断是否使用随机决策
        /// </summary>
        private bool ShouldUseRandomDecision()
        {
            return _random.NextDouble() < GetRandomnessRate();
        }

        /// <summary>
        /// 首家出牌：根据角色和难度选择最优策略
        /// </summary>
        /// <param name="hand">当前手牌</param>
        /// <param name="role">AI角色</param>
        /// <param name="myPosition">我的位置（用于记牌评估）</param>
        /// <param name="opponentPositions">对手位置列表（用于甩牌评估）</param>
        public List<Card> Lead(List<Card> hand, AIRole role = AIRole.Opponent,
            int myPosition = -1, List<int>? opponentPositions = null)
        {
            if (hand == null || hand.Count == 0)
                return new List<Card>();

            // 简单难度：随机选择
            if (_difficulty == AIDifficulty.Easy && ShouldUseRandomDecision())
            {
                var comparer = new CardComparer(_config);
                var validator = new PlayValidator(_config);
                var validCards = hand.Where(c => validator.IsValidPlay(hand, new List<Card> { c })).ToList();
                if (validCards.Count > 0)
                    return new List<Card> { validCards[_random.Next(validCards.Count)] };
            }

            var cardComparer = new CardComparer(_config);
            var playValidator = new PlayValidator(_config);

            var candidates = BuildLeadCandidates(hand, cardComparer, role, myPosition, opponentPositions)
                .Where(c => playValidator.IsValidPlay(hand, c))
                .ToList();

            if (candidates.Count == 0)
                return new List<Card> { hand.OrderByDescending(c => c, cardComparer).First() };

            return SelectBestLeadCandidate(candidates, cardComparer, role);
        }

        /// <summary>
        /// 跟牌：根据角色和当前局势选择最优策略
        /// </summary>
        /// <param name="hand">当前手牌</param>
        /// <param name="leadCards">首家出牌</param>
        /// <param name="currentWinningCards">当前赢牌的牌组（用于判断能否赢）</param>
        /// <param name="role">AI角色</param>
        /// <param name="partnerWinning">对家是否当前赢牌</param>
        public List<Card> Follow(List<Card> hand, List<Card> leadCards,
            List<Card>? currentWinningCards = null,
            AIRole role = AIRole.Opponent,
            bool partnerWinning = false)
        {
            if (hand == null || hand.Count == 0 || leadCards == null || leadCards.Count == 0)
                return new List<Card>();

            int need = leadCards.Count;
            if (hand.Count <= need)
                return new List<Card>(hand);

            // 如果没有提供当前赢牌，默认为首家出牌
            if (currentWinningCards == null || currentWinningCards.Count == 0)
                currentWinningCards = leadCards;

            // 简单难度：基本跟牌
            if (_difficulty == AIDifficulty.Easy && ShouldUseRandomDecision())
            {
                var comparer = new CardComparer(_config);
                var validator = new FollowValidator(_config);
                var leadCategory = _config.GetCardCategory(leadCards[0]);
                var leadSuit = leadCards[0].Suit;
                var sameCategoryCards = hand.Where(c => MatchesLeadCategory(c, leadSuit, leadCategory)).ToList();

                if (sameCategoryCards.Count >= need)
                {
                    var shuffled = sameCategoryCards.OrderBy(x => _random.Next()).Take(need).ToList();
                    if (validator.IsValidFollow(hand, leadCards, shuffled))
                        return shuffled;
                }
            }

            var cardComparer = new CardComparer(_config);
            var followValidator = new FollowValidator(_config);

            var leadCategory2 = _config.GetCardCategory(leadCards[0]);
            var leadSuit2 = leadCards[0].Suit;
            var sameCategoryCards2 = hand.Where(c => MatchesLeadCategory(c, leadSuit2, leadCategory2)).ToList();

            var candidates = new List<List<Card>>();

            // 有足够同类牌：根据角色和局势选择策略
            if (sameCategoryCards2.Count >= need)
            {
                candidates.AddRange(BuildSameCategoryFollowCandidates(
                    sameCategoryCards2, leadCards, need, cardComparer, role, partnerWinning));
            }
            else
            {
                // 同类牌不足：先出尽同类牌，剩余根据角色选择
                var mustFollow = sameCategoryCards2.OrderByDescending(c => c, cardComparer).ToList();
                int missing = need - mustFollow.Count;

                var remaining = RemoveCards(hand, mustFollow);
                var filler = new List<Card>();

                // 缺门时的策略
                if (partnerWinning)
                {
                    // 对家赢牌：优先送分牌
                    var pointCards = remaining.Where(c => GetCardPoints(c) > 0)
                        .OrderByDescending(c => GetCardPoints(c))
                        .ToList();
                    filler.AddRange(pointCards.Take(missing));
                }
                else if (leadCategory2 == CardCategory.Suit)
                {
                    // 对手赢牌且是副牌：尝试用主牌毙牌
                    var trumpCards = remaining.Where(_config.IsTrump)
                        .OrderByDescending(c => c, cardComparer)
                        .ToList();
                    var cutPriority = ResolveRoleWeighted01(_strategy.FollowTrumpCutPriority, role, neutral: 0.6);
                    var maxCuts = (int)System.Math.Ceiling(missing * cutPriority);
                    if (maxCuts > 0)
                        filler.AddRange(trumpCards.Take(System.Math.Min(missing, maxCuts)));
                }

                // 填充剩余
                if (filler.Count < missing)
                {
                    var used = RemoveCards(remaining, filler);
                    filler.AddRange(used.OrderBy(c => c, cardComparer).Take(missing - filler.Count));
                }

                candidates.Add(mustFollow.Concat(filler).ToList());
            }

            // 兜底候选：按花色优先级选择
            var fallbackCandidate = BuildFallbackFollowCandidate(hand, leadCards, need, cardComparer, followValidator);
            candidates.Add(fallbackCandidate);

            var validCandidates = DeduplicateCandidates(candidates)
                .Where(c => c.Count == need && followValidator.IsValidFollow(hand, leadCards, c))
                .ToList();

            // [P1修复] 如果所有候选都无效，使用兜底候选并确保合法
            if (validCandidates.Count == 0)
            {
                var emergencyCandidate = BuildFallbackFollowCandidate(hand, leadCards, need, cardComparer, followValidator);
                if (followValidator.IsValidFollow(hand, leadCards, emergencyCandidate))
                    return emergencyCandidate;

                // 最后的兜底：直接取前N张（理论上不应该到这里）
                return hand.Take(need).ToList();
            }

            return SelectBestFollowCandidate(validCandidates, currentWinningCards, cardComparer, role, partnerWinning);
        }

        /// <summary>
        /// 构建兜底跟牌候选（确保合法）
        /// </summary>
        private List<Card> BuildFallbackFollowCandidate(List<Card> hand, List<Card> leadCards,
            int need, CardComparer comparer, FollowValidator validator)
        {
            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand.Where(c => MatchesLeadCategory(c, leadSuit, leadCategory)).ToList();

            // 优先同花色
            if (sameCategoryCards.Count >= need)
                return sameCategoryCards.OrderBy(c => c, comparer).Take(need).ToList();

            // 同花色不足，先出同花色，再补其他
            var result = new List<Card>(sameCategoryCards);
            var remaining = RemoveCards(hand, sameCategoryCards);
            result.AddRange(remaining.OrderBy(c => c, comparer).Take(need - result.Count));

            return result;
        }

        /// <summary>
        /// 获取牌的分值
        /// </summary>
        private int GetCardPoints(Card card)
        {
            if (card.Rank == Rank.Five) return 5;
            if (card.Rank == Rank.Ten || card.Rank == Rank.King) return 10;
            return 0;
        }

        /// <summary>
        /// 计算赢牌余量（越小说明用最小的牌赢）
        /// </summary>
        private int CalculateWinMargin(List<Card> candidate, List<Card> currentWinning, CardComparer comparer)
        {
            int margin = 0;
            for (int i = 0; i < candidate.Count && i < currentWinning.Count; i++)
            {
                int cmp = comparer.Compare(candidate[i], currentWinning[i]);
                if (cmp > 0)
                    margin += (GetCardValue(candidate[i]) - GetCardValue(currentWinning[i]));
            }
            return margin;
        }

        /// <summary>
        /// 扣底（坐庄玩家专属）
        /// 输入：手牌（推荐33张：25张手牌 + 8张底牌，但也支持其他数量）
        /// 输出：8张要扣底的牌
        /// </summary>
        public List<Card> BuryBottom(List<Card> hand)
        {
            // [P2修复] 空参保护
            if (hand == null || hand.Count < 8)
                return new List<Card>();

            var comparer = new CardComparer(_config);

            // [P1修复] 支持任意数量的手牌（兼容现有API测试）
            // 如果手牌正好是33张，使用智能策略
            // 如果手牌少于33张，简单取最小的8张
            if (hand.Count == 33)
            {
                // 智能埋底策略
                var cardScores = hand.Select(c => new
                {
                    Card = c,
                    Score = EvaluateCardForBurying(c, hand, comparer)
                }).OrderBy(x => x.Score).ToList();

                var selected = cardScores.Take(8).Select(x => x.Card).ToList();
                ProtectPointCardsInBurySelection(selected, cardScores.Select(x => x.Card).ToList());
                return selected;
            }
            else
            {
                // 简单策略：取最小的8张
                return hand.OrderBy(c => c, comparer).Take(8).ToList();
            }
        }

        /// <summary>
        /// 评估牌的埋底价值（分数越低越适合埋）
        /// </summary>
        private int EvaluateCardForBurying(Card card, List<Card> hand, CardComparer comparer)
        {
            int score = 0;
            var protectionWeight = _strategy.PointCardProtectionWeight;
            var denyWeight = _strategy.OpponentDenyPointBias;

            // 1. 分牌惩罚（最不想埋）
            int points = GetCardPoints(card);
            if (points > 0)
                score += (int)System.Math.Round(1000 * protectionWeight + points * 100 * denyWeight);

            // 2. 主牌惩罚
            if (_config.IsTrump(card))
                score += 500;

            // 3. 对子惩罚（对子比单张更不想埋）
            int sameCount = hand.Count(c => c.Suit == card.Suit && c.Rank == card.Rank);
            if (sameCount >= 2)
                score += 200;

            // 4. 花色长度考虑（短门优先埋）
            var suitCards = hand.Where(c => !_config.IsTrump(c) && c.Suit == card.Suit).ToList();
            if (suitCards.Count <= 3 && suitCards.Count > 0)
                score -= 100; // 短门优先

            // 5. 牌力考虑（小牌优先埋）
            score += GetCardValue(card) / 10;

            return score;
        }

        /// <summary>
        /// 在满足8张扣底前提下尽量避免埋分牌，优先替换为非分牌。
        /// </summary>
        private void ProtectPointCardsInBurySelection(List<Card> selected, List<Card> orderedCards)
        {
            if (selected.Count != 8)
                return;

            var selectedPointCards = selected.Where(c => GetCardPoints(c) > 0).ToList();
            if (selectedPointCards.Count == 0)
                return;

            var replacements = orderedCards
                .Where(c => GetCardPoints(c) == 0 && !selected.Contains(c))
                .ToList();

            foreach (var pointCard in selectedPointCards)
            {
                if (replacements.Count == 0)
                    break;

                var replacement = replacements[0];
                replacements.RemoveAt(0);
                selected.Remove(pointCard);
                selected.Add(replacement);
            }
        }

        private List<List<Card>> BuildLeadCandidates(List<Card> hand, CardComparer comparer, AIRole role,
            int myPosition, List<int>? opponentPositions)
        {
            var groups = new List<List<Card>>();

            var trumpCards = hand.Where(_config.IsTrump).ToList();
            if (trumpCards.Count > 0)
                groups.Add(trumpCards);

            foreach (var suitGroup in hand.Where(c => !_config.IsTrump(c)).GroupBy(c => c.Suit))
                groups.Add(suitGroup.ToList());

            var candidates = new List<List<Card>>();

            foreach (var group in groups)
            {
                var sorted = group.OrderByDescending(c => c, comparer).ToList();

                bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

                // 评估甩牌（多张）
                if (sorted.Count >= 3)
                {
                    // 使用记牌系统评估甩牌成功率
                    bool canThrow = CanSafelyThrow(sorted, hand, myPosition, opponentPositions);
                    var dealerThrowAggressiveness = Math.Min(1.0, _strategy.LeadThrowAggressiveness + 0.15);

                    if (canThrow)
                    {
                        // 甩牌成功率高，可以尝试
                        candidates.Add(sorted);
                    }
                    else if (isDealerSide && sorted.Count >= 4 && _random.NextDouble() < dealerThrowAggressiveness * 0.35)
                    {
                        // 庄家先手可适度激进，尝试中等规模甩牌以发挥底牌优势。
                        candidates.Add(sorted.Take(4).ToList());
                    }
                    else if (!isDealerSide && sorted.Count >= 5)
                    {
                        // 闲家且牌很多，即使风险也可以尝试（激进策略）
                        candidates.Add(sorted);
                    }
                }

                var pair = FindStrongestPair(sorted, comparer);
                if (pair != null)
                    candidates.Add(pair);

                // 尝试不同长度拖拉机，优先更长拖拉机
                for (int len = sorted.Count - (sorted.Count % 2); len >= 4; len -= 2)
                {
                    var tractor = FindStrongestTractor(sorted, len, comparer);
                    if (tractor != null)
                    {
                        candidates.Add(tractor);
                        break;
                    }
                }

                // 单张兜底
                candidates.Add(new List<Card> { sorted[0] });
            }

            return DeduplicateCandidates(candidates);
        }

        /// <summary>
        /// 判断是否可以安全甩牌
        /// </summary>
        private bool CanSafelyThrow(List<Card> throwCards, List<Card> hand,
            int myPosition, List<int>? opponentPositions)
        {
            // 简单难度：不评估，随机决定
            if (_difficulty == AIDifficulty.Easy)
                return _random.NextDouble() > 0.5;

            // 没有对手信息，保守处理
            if (opponentPositions == null || opponentPositions.Count == 0)
                return throwCards.Count <= 2; // 只允许甩2张以下

            // 使用记牌系统评估
            double successProbability = _memory.EvaluateThrowSuccessProbability(
                throwCards, hand, myPosition, opponentPositions);

            // 根据难度设置阈值
            double threshold = _difficulty switch
            {
                AIDifficulty.Medium => 0.6,  // 60%成功率
                AIDifficulty.Hard => 0.7,    // 70%成功率
                AIDifficulty.Expert => 0.8,  // 80%成功率
                _ => 0.5
            };

            return successProbability >= threshold;
        }

        private List<List<Card>> BuildSameCategoryFollowCandidates(
            List<Card> sameCategoryCards,
            List<Card> leadCards,
            int need,
            CardComparer comparer,
            AIRole role,
            bool partnerWinning)
        {
            var candidates = new List<List<Card>>();
            var sorted = sameCategoryCards.OrderByDescending(c => c, comparer).ToList();
            var leadPattern = new CardPattern(leadCards, _config);

            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            if (leadPattern.Type == PatternType.Tractor && need >= 4 && need % 2 == 0)
            {
                var tractor = FindStrongestTractor(sorted, need, comparer);
                if (tractor != null)
                    candidates.Add(tractor);
            }

            if (leadPattern.Type == PatternType.Pair && need == 2)
            {
                var pair = FindStrongestPair(sorted, comparer);
                if (pair != null)
                    candidates.Add(pair);
            }

            // 根据角色和局势选择策略
            if (partnerWinning)
            {
                // 对家赢牌：出小牌保留实力，或送分牌
                candidates.Add(sorted.OrderBy(c => c, comparer).Take(need).ToList());

                // 如果有分牌，优先送分牌
                var pointCards = sorted.Where(c => GetCardPoints(c) > 0)
                    .OrderByDescending(c => GetCardPoints(c))
                    .ToList();
                if (pointCards.Count >= need)
                    candidates.Add(pointCards.Take(need).ToList());
            }
            else
            {
                // 对手赢牌：尝试用大牌争胜
                candidates.Add(sorted.Take(need).ToList());

                // 如果无法赢，出小牌保留
                candidates.Add(sorted.OrderBy(c => c, comparer).Take(need).ToList());
            }

            return DeduplicateCandidates(candidates);
        }

        private List<Card> SelectBestLeadCandidate(List<List<Card>> candidates, CardComparer comparer, AIRole role)
        {
            // 随机决策
            if (ShouldUseRandomDecision() && candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];

            var best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareLeadCandidates(candidates[i], best, comparer, role) > 0)
                    best = candidates[i];
            }
            return best;
        }

        private int CompareLeadCandidates(List<Card> a, List<Card> b, CardComparer comparer, AIRole role)
        {
            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            // 庄家：优先控制（拖拉机/对子），闲家：优先多张（甩牌）
            if (!isDealerSide)
            {
                // 闲家优先多张
                if (a.Count != b.Count)
                    return a.Count.CompareTo(b.Count);
            }

            int pa = GetPatternPriority(new CardPattern(a, _config).Type);
            int pb = GetPatternPriority(new CardPattern(b, _config).Type);
            if (pa != pb)
                return pa.CompareTo(pb);

            if (isDealerSide)
            {
                // 庄家优先多张（在牌型相同的情况下）
                if (a.Count != b.Count)
                    return a.Count.CompareTo(b.Count);
            }

            int pairA = CountPairs(a);
            int pairB = CountPairs(b);
            if (pairA != pairB)
                return pairA.CompareTo(pairB);

            int cmp = CompareCardSets(a, b, comparer);
            if (cmp != 0)
                return cmp;

            return _random.Next(2) == 0 ? -1 : 1;
        }

        private List<Card> SelectBestFollowCandidate(List<List<Card>> candidates, List<Card> currentWinningCards,
            CardComparer comparer, AIRole role, bool partnerWinning)
        {
            // 随机决策
            if (ShouldUseRandomDecision() && candidates.Count > 0)
                return candidates[_random.Next(candidates.Count)];

            var best = candidates[0];
            for (int i = 1; i < candidates.Count; i++)
            {
                if (CompareFollowCandidates(candidates[i], best, currentWinningCards, comparer, role, partnerWinning) > 0)
                    best = candidates[i];
            }
            return best;
        }

        private int CompareFollowCandidates(List<Card> a, List<Card> b, List<Card> currentWinningCards,
            CardComparer comparer, AIRole role, bool partnerWinning)
        {
            // [P1修复] 与当前赢牌比较，而不是首引牌
            bool beatA = CanBeatCards(currentWinningCards, a);
            bool beatB = CanBeatCards(currentWinningCards, b);

            bool isDealerSide = role == AIRole.Dealer || role == AIRole.DealerPartner;

            // 对家赢牌时的策略
            if (partnerWinning)
            {
                // 1. 优先送分牌（使用细粒度参数）
                int pointsA = a.Sum(c => GetCardPoints(c));
                int pointsB = b.Sum(c => GetCardPoints(c));
                if (pointsA != pointsB)
                {
                    var givePriority = _strategy.PartnerWinning_GivePointsPriority;
                    if (givePriority >= 0.5)
                        return pointsA.CompareTo(pointsB); // 送分优先
                }

                // 2. 其次垫小牌（保留大牌）
                var discardSmallPriority = _strategy.PartnerWinning_DiscardSmallPriority;
                if (discardSmallPriority >= 0.5)
                {
                    int minValueA = a.Min(c => GetCardValue(c));
                    int minValueB = b.Min(c => GetCardValue(c));
                    if (minValueA != minValueB)
                        return minValueA.CompareTo(minValueB); // 小牌优先
                }

                // 3. 避免垫主牌
                var avoidTrumpPriority = _strategy.PartnerWinning_AvoidTrumpPriority;
                if (avoidTrumpPriority >= 0.5)
                {
                    int trumpCountA = a.Count(_config.IsTrump);
                    int trumpCountB = b.Count(_config.IsTrump);
                    if (trumpCountA != trumpCountB)
                        return trumpCountA.CompareTo(trumpCountB); // 少垫主牌
                }

                // 4. 保留对子
                var keepPairsPriority = _strategy.PartnerWinning_KeepPairsPriority;
                if (keepPairsPriority >= 0.5)
                {
                    int pairCountA = CountPairs(a);
                    int pairCountB = CountPairs(b);
                    if (pairCountA != pairCountB)
                        return pairCountB.CompareTo(pairCountA); // 少垫对子
                }
            }
            else
            {
                // 对手赢牌时：优先争胜
                if (beatA != beatB)
                {
                    var beatBias = ResolveRoleWeighted01(_strategy.FollowBeatAttemptBias, role, neutral: 0.5);
                    if (beatBias >= 0.5)
                    {
                        // 如果都能赢，选择"用最小牌赢"（保留大牌控制下轮）
                        if (beatA && beatB)
                        {
                            var useMinimalPriority = _strategy.WinAttempt_UseMinimalCardsPriority;
                            if (useMinimalPriority >= 0.5)
                            {
                                // 比较"赢牌余量"（越小越好）
                                int marginA = CalculateWinMargin(a, currentWinningCards, comparer);
                                int marginB = CalculateWinMargin(b, currentWinningCards, comparer);
                                if (marginA != marginB)
                                    return marginA.CompareTo(marginB); // 余量小的优先
                            }
                        }
                        return beatA ? 1 : -1;
                    }

                    return beatA ? -1 : 1;
                }

                // 无法赢时：优先垫小牌和非分牌
                if (!beatA && !beatB)
                {
                    // 1. 优先垫非分牌
                    var avoidPointsPriority = _strategy.CannotWin_AvoidPointsPriority;
                    if (avoidPointsPriority >= 0.5)
                    {
                        int pointsA = a.Sum(c => GetCardPoints(c));
                        int pointsB = b.Sum(c => GetCardPoints(c));
                        if (pointsA != pointsB)
                            return pointsB.CompareTo(pointsA); // 少送分优先
                    }

                    // 2. 其次垫小牌
                    var discardSmallPriority = _strategy.CannotWin_DiscardSmallPriority;
                    if (discardSmallPriority >= 0.5)
                    {
                        int avgValueA = (int)a.Average(c => GetCardValue(c));
                        int avgValueB = (int)b.Average(c => GetCardValue(c));
                        if (avgValueA != avgValueB)
                            return avgValueA.CompareTo(avgValueB); // 小牌优先
                    }

                    // 3. 避免垫主牌
                    var avoidTrumpPriority = _strategy.CannotWin_AvoidTrumpPriority;
                    if (avoidTrumpPriority >= 0.5)
                    {
                        int trumpCountA = a.Count(_config.IsTrump);
                        int trumpCountB = b.Count(_config.IsTrump);
                        if (trumpCountA != trumpCountB)
                            return trumpCountA.CompareTo(trumpCountB); // 少垫主牌
                    }

                    // 4. 庄家额外保守
                    if (isDealerSide)
                    {
                        int cmpSmall = CompareCardSets(b, a, comparer);
                        if (cmpSmall != 0)
                            return cmpSmall;
                    }
                }
            }

            // 收官阶段优先稳健：在牌力接近时尽量保留高牌和主牌控制力。
            var endgameStability = ResolveRoleWeighted01(_strategy.EndgameStabilityBias, role, neutral: 0.6);
            if (endgameStability > 0.5)
            {
                var volatilityA = EstimateVolatility(a);
                var volatilityB = EstimateVolatility(b);
                if (volatilityA != volatilityB)
                    return volatilityB.CompareTo(volatilityA);
            }

            var endgameFinish = ResolveRoleWeighted01(_strategy.EndgameFinishBias, role, neutral: 0.6);
            if (endgameFinish > 0.6 && beatA && beatB)
            {
                var pointsA = a.Sum(GetCardPoints);
                var pointsB = b.Sum(GetCardPoints);
                if (pointsA != pointsB)
                    return pointsA.CompareTo(pointsB);
            }

            int pa = GetPatternPriority(new CardPattern(a, _config).Type);
            int pb = GetPatternPriority(new CardPattern(b, _config).Type);
            if (pa != pb)
                return pa.CompareTo(pb);

            int pairA = CountPairs(a);
            int pairB = CountPairs(b);
            if (pairA != pairB)
                return pairA.CompareTo(pairB);

            int cmp = CompareCardSets(a, b, comparer);
            if (cmp != 0)
                return cmp;

            return _random.Next(2) == 0 ? -1 : 1;
        }

        /// <summary>
        /// 判断followCards能否赢过currentWinningCards
        /// </summary>
        private bool CanBeatCards(List<Card> currentWinningCards, List<Card> followCards)
        {
            if (currentWinningCards.Count != followCards.Count)
                return false;

            var judge = new TrickJudge(_config);
            var plays = new List<TrickPlay>
            {
                new TrickPlay(0, currentWinningCards),
                new TrickPlay(1, followCards)
            };

            return judge.DetermineWinner(plays) == 1;
        }

        private bool CanBeatLead(List<Card> leadCards, List<Card> followCards)
        {
            // 保留此方法以兼容旧代码，内部调用新方法
            return CanBeatCards(leadCards, followCards);
        }

        private List<Card>? FindStrongestPair(List<Card> cards, CardComparer comparer)
        {
            var groups = cards.GroupBy(c => (c.Suit, c.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => g.Take(2).ToList())
                .ToList();

            if (groups.Count == 0)
                return null;

            var best = groups[0];
            for (int i = 1; i < groups.Count; i++)
            {
                if (comparer.Compare(groups[i][0], best[0]) > 0)
                    best = groups[i];
            }
            return best;
        }

        /// <summary>
        /// 查找最强拖拉机（优化版，避免组合爆炸）
        /// [P2修复] 使用贪心算法代替组合枚举，降低复杂度
        /// </summary>
        private List<Card>? FindStrongestTractor(List<Card> cards, int neededCount, CardComparer comparer)
        {
            if (neededCount < 4 || neededCount % 2 != 0)
                return null;

            int pairCount = neededCount / 2;

            // 找出所有对子
            var pairUnits = cards.GroupBy(c => (c.Suit, c.Rank))
                .Where(g => g.Count() >= 2)
                .Select(g => g.Take(2).ToList())
                .OrderByDescending(p => p[0], comparer)
                .ToList();

            if (pairUnits.Count < pairCount)
                return null;

            // [P2优化] 使用贪心算法：从最大的对子开始，尝试构建连续拖拉机
            // 这样避免了组合枚举，复杂度从O(C(n,k))降到O(n)
            for (int startIdx = 0; startIdx <= pairUnits.Count - pairCount; startIdx++)
            {
                var candidate = new List<Card>();
                for (int i = 0; i < pairCount; i++)
                {
                    candidate.AddRange(pairUnits[startIdx + i]);
                }

                var pattern = new CardPattern(candidate, _config);
                if (pattern.IsTractor(candidate))
                    return candidate;
            }

            // 如果没有找到连续拖拉机，且对子数量不多（<=10），尝试组合搜索
            // 这是为了处理断档拖的情况
            if (pairUnits.Count <= 10)
            {
                foreach (var combo in Combinations(pairUnits, pairCount))
                {
                    var candidate = combo.SelectMany(x => x).ToList();
                    var pattern = new CardPattern(candidate, _config);
                    if (pattern.IsTractor(candidate))
                        return candidate;
                }
            }

            return null;
        }

        private IEnumerable<List<T>> Combinations<T>(List<T> items, int choose)
        {
            var buffer = new List<T>();
            foreach (var combo in CombinationsCore(items, choose, 0, buffer))
                yield return combo;
        }

        private IEnumerable<List<T>> CombinationsCore<T>(List<T> items, int choose, int start, List<T> buffer)
        {
            if (buffer.Count == choose)
            {
                yield return new List<T>(buffer);
                yield break;
            }

            int needed = choose - buffer.Count;
            for (int i = start; i <= items.Count - needed; i++)
            {
                buffer.Add(items[i]);
                foreach (var combo in CombinationsCore(items, choose, i + 1, buffer))
                    yield return combo;
                buffer.RemoveAt(buffer.Count - 1);
            }
        }

        private int CompareCardSets(List<Card> a, List<Card> b, CardComparer comparer)
        {
            var sa = a.OrderByDescending(c => GetCardValue(c)).ToList();
            var sb = b.OrderByDescending(c => GetCardValue(c)).ToList();

            int n = System.Math.Min(sa.Count, sb.Count);
            for (int i = 0; i < n; i++)
            {
                int cmp = comparer.Compare(sa[i], sb[i]);
                if (cmp != 0)
                    return cmp;

                int va = GetCardValue(sa[i]);
                int vb = GetCardValue(sb[i]);
                if (va != vb)
                    return va.CompareTo(vb);
            }

            return sa.Count.CompareTo(sb.Count);
        }

        private int GetCardValue(Card card)
        {
            if (card.Rank == Rank.BigJoker) return 1000;
            if (card.Rank == Rank.SmallJoker) return 900;

            bool isLevel = card.Rank == _config.LevelRank;
            bool isTrumpSuit = _config.TrumpSuit.HasValue && card.Suit == _config.TrumpSuit.Value;

            if (isLevel && isTrumpSuit) return 800;
            if (isLevel) return 700;
            if (_config.IsTrump(card)) return 600 + (int)card.Rank;
            return 100 + (int)card.Rank;
        }

        private int CountPairs(List<Card> cards)
        {
            return cards.GroupBy(c => (c.Suit, c.Rank)).Sum(g => g.Count() / 2);
        }

        private int GetPatternPriority(PatternType type)
        {
            return type switch
            {
                PatternType.Tractor => 3,
                PatternType.Pair => 2,
                PatternType.Mixed => 1,
                PatternType.Single => 0,
                _ => 0
            };
        }

        private bool MatchesLeadCategory(Card card, Suit leadSuit, CardCategory leadCategory)
        {
            if (leadCategory == CardCategory.Trump)
                return _config.IsTrump(card);

            return !_config.IsTrump(card) && card.Suit == leadSuit;
        }

        private List<Card> RemoveCards(List<Card> source, List<Card> toRemove)
        {
            var copy = new List<Card>(source);
            foreach (var card in toRemove)
            {
                var found = copy.FirstOrDefault(c => c.Equals(card));
                if (found != null)
                    copy.Remove(found);
            }
            return copy;
        }

        private List<List<Card>> DeduplicateCandidates(List<List<Card>> candidates)
        {
            var deduped = new List<List<Card>>();
            var seen = new HashSet<string>();

            foreach (var candidate in candidates)
            {
                if (candidate == null || candidate.Count == 0)
                    continue;

                string key = BuildCandidateKey(candidate);
                if (seen.Add(key))
                    deduped.Add(candidate);
            }

            return deduped;
        }

        private string BuildCandidateKey(List<Card> cards)
        {
            return string.Join(",", cards
                .OrderBy(c => (int)c.Suit)
                .ThenBy(c => (int)c.Rank)
                .Select(c => $"{(int)c.Suit}-{(int)c.Rank}"));
        }

        private static double ResolveRoleWeighted01(double value, AIRole role, double neutral)
        {
            var clamped = System.Math.Clamp(value, 0, 1);
            if (role == AIRole.Opponent)
                return clamped;

            // 非防守侧采用保守插值，避免参数过拟合影响庄家链路。
            return neutral + (clamped - neutral) * 0.4;
        }

        private int EstimateVolatility(List<Card> cards)
        {
            if (cards == null || cards.Count == 0)
                return 0;

            var trumpWeight = cards.Count(_config.IsTrump) * 12;
            var highCardWeight = cards.Sum(GetCardValue) / 60;
            var pointRisk = cards.Sum(GetCardPoints) * 8;
            return trumpWeight + highCardWeight + pointRisk;
        }
    }
}
