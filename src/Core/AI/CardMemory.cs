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
        private readonly Dictionary<int, HashSet<string>> _playerNoPairSystems; // 玩家在某体系下"无对子"证据
        private readonly int _totalDecks = 2; // 两副牌
        private const Suit TrumpVoidMarker = (Suit)(-1);

        public CardMemory(GameConfig config)
        {
            _config = config;
            _playedCards = new Dictionary<(Suit, Rank), int>();
            _playerVoidSuits = new Dictionary<int, HashSet<Suit>>();
            _playerNoPairSystems = new Dictionary<int, HashSet<string>>();
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
                            _playerVoidSuits[play.PlayerIndex].Add(TrumpVoidMarker);
                        }
                        else
                        {
                            _playerVoidSuits[play.PlayerIndex].Add(leadSuit);
                        }
                    }
                }
            }

            TrackNoPairEvidence(plays);
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

            return _playerVoidSuits[playerPosition].Contains(TrumpVoidMarker);
        }

        /// <summary>
        /// 甩牌安全评估结果
        /// </summary>
        public sealed class ThrowSafetyAssessment
        {
            public bool IsDeterministicallySafe { get; }
            public double SuccessProbability { get; }
            public int BlockingComponentCount { get; }

            public ThrowSafetyAssessment(bool isDeterministicallySafe, double successProbability, int blockingComponentCount)
            {
                IsDeterministicallySafe = isDeterministicallySafe;
                SuccessProbability = Clamp01(successProbability);
                BlockingComponentCount = blockingComponentCount;
            }

            private static double Clamp01(double value)
            {
                if (value < 0) return 0;
                if (value > 1) return 1;
                return value;
            }
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
            return EvaluateThrowSafety(throwCards, hand, myPosition, opponentPositions).SuccessProbability;
        }

        /// <summary>
        /// 评估甩牌安全性（先做总池保守判定，再做概率估计）
        /// </summary>
        /// <param name="knownBottomCards">可选：仅当前玩家可见的已知底牌（通常仅庄家）</param>
        public ThrowSafetyAssessment EvaluateThrowSafety(
            List<Card> throwCards,
            List<Card> hand,
            int myPosition,
            List<int> opponentPositions,
            List<Card>? knownBottomCards = null)
        {
            if (throwCards == null || throwCards.Count == 0)
                return new ThrowSafetyAssessment(false, 0, 0);
            if (hand == null)
                hand = new List<Card>();

            var others = (opponentPositions ?? new List<int>())
                .Where(pos => pos != myPosition)
                .Distinct()
                .ToList();

            if (!IsSameSuitOrTrump(throwCards))
                return new ThrowSafetyAssessment(false, 0, 0);

            var comparer = new CardComparer(_config);
            var throwSystem = ResolveThrowSystem(throwCards[0]);
            var components = DecomposeThrowComponents(throwCards, comparer);
            if (components.Count == 0)
            {
                return new ThrowSafetyAssessment(false, 0, 0);
            }

            var remainingPool = BuildUnknownPool(hand, knownBottomCards);
            var systemPool = remainingPool
                .Where(card => IsCardInSystem(card, throwSystem))
                .ToList();

            bool deterministicSafe = true;
            int blockingComponents = 0;
            double successProbability = 1.0;

            foreach (var component in components)
            {
                var risk = EvaluateComponentRisk(component, throwSystem, systemPool, others, comparer);
                if (risk.CanBeBlocked)
                {
                    deterministicSafe = false;
                    blockingComponents++;
                }

                successProbability *= (1.0 - risk.BlockProbability);
            }

            if (deterministicSafe)
                successProbability = 1.0;

            return new ThrowSafetyAssessment(deterministicSafe, successProbability, blockingComponents);
        }

        private ComponentRisk EvaluateComponentRisk(
            ThrowComponent component,
            ThrowSystem throwSystem,
            List<Card> systemPool,
            List<int> opponentPositions,
            CardComparer comparer)
        {
            if (component.Cards.Count == 0)
                return ComponentRisk.Safe;

            if (component.Type == ThrowComponentType.Single)
                return EvaluateSingleRisk(component, throwSystem, systemPool, opponentPositions, comparer);

            if (component.Type == ThrowComponentType.Pair)
                return EvaluatePairRisk(component, throwSystem, systemPool, opponentPositions, comparer);

            return EvaluateTractorRisk(component, throwSystem, systemPool, opponentPositions, comparer);
        }

        private ComponentRisk EvaluateSingleRisk(
            ThrowComponent component,
            ThrowSystem throwSystem,
            List<Card> systemPool,
            List<int> opponentPositions,
            CardComparer comparer)
        {
            int biggerSingles = systemPool.Count(card => comparer.Compare(card, component.HighestCard) > 0);
            int eligiblePlayers = CountEligibleSingleBlockers(opponentPositions, throwSystem);
            bool canBeBlocked = biggerSingles > 0 && eligiblePlayers > 0;

            if (!canBeBlocked)
                return ComponentRisk.Safe;

            double poolSize = Math.Max(1, systemPool.Count);
            double density = biggerSingles / poolSize;
            double spread = 1.0 - Math.Pow(0.72, eligiblePlayers);
            double blockProbability = Clamp01(0.20 + density * 0.55 + spread * 0.35);
            return new ComponentRisk(true, blockProbability);
        }

        private ComponentRisk EvaluatePairRisk(
            ThrowComponent component,
            ThrowSystem throwSystem,
            List<Card> systemPool,
            List<int> opponentPositions,
            CardComparer comparer)
        {
            // 历史硬证据：已知其他玩家都无该体系对子，则对子组件必然不会被拦截
            if (HasNoPairEvidenceForAll(opponentPositions, throwSystem.Key))
                return ComponentRisk.Safe;

            int biggerPairUnits = CountBiggerPairUnits(systemPool, component.HighestCard, comparer);
            int eligiblePlayers = CountEligiblePairBlockers(opponentPositions, throwSystem);
            bool canBeBlocked = biggerPairUnits > 0 && eligiblePlayers > 0;
            if (!canBeBlocked)
                return ComponentRisk.Safe;

            int totalPairUnits = CountTotalPairUnits(systemPool);
            double density = biggerPairUnits / (double)Math.Max(1, totalPairUnits);
            double spread = 1.0 - Math.Pow(0.68, eligiblePlayers);
            double blockProbability = Clamp01(0.25 + density * 0.55 + spread * 0.30);
            return new ComponentRisk(true, blockProbability);
        }

        private ComponentRisk EvaluateTractorRisk(
            ThrowComponent component,
            ThrowSystem throwSystem,
            List<Card> systemPool,
            List<int> opponentPositions,
            CardComparer comparer)
        {
            // 历史硬证据：若所有其他玩家都无对子，则不可能组成拖拉机
            if (HasNoPairEvidenceForAll(opponentPositions, throwSystem.Key))
                return ComponentRisk.Safe;

            int eligiblePlayers = CountEligiblePairBlockers(opponentPositions, throwSystem);
            if (eligiblePlayers == 0)
                return ComponentRisk.Safe;

            int blockerOptions = CountBiggerTractorOptions(systemPool, throwSystem, component.PairCount, component.HighestCard, comparer);
            if (blockerOptions == 0)
                return ComponentRisk.Safe;

            int totalOptions = CountTotalTractorOptions(systemPool, throwSystem, component.PairCount);
            double density = blockerOptions / (double)Math.Max(1, totalOptions);
            double spread = 1.0 - Math.Pow(0.65, eligiblePlayers);
            double blockProbability = Clamp01(0.30 + density * 0.45 + spread * 0.30);
            return new ComponentRisk(true, blockProbability);
        }

        private List<Card> BuildUnknownPool(List<Card> hand, List<Card>? knownBottomCards)
        {
            var unknownPool = new List<Card>();
            var handLookup = BuildCountLookup(hand);
            var bottomLookup = BuildCountLookup(knownBottomCards ?? new List<Card>());

            foreach (var cardType in EnumerateDeckCardTypes())
            {
                int played = GetPlayedCount(cardType);
                int inHand = handLookup.TryGetValue((cardType.Suit, cardType.Rank), out int handCount) ? handCount : 0;
                int inBottom = bottomLookup.TryGetValue((cardType.Suit, cardType.Rank), out int bottomCount) ? bottomCount : 0;
                int remaining = Math.Max(0, _totalDecks - played - inHand - inBottom);

                for (int i = 0; i < remaining; i++)
                    unknownPool.Add(cardType);
            }

            return unknownPool;
        }

        private Dictionary<(Suit, Rank), int> BuildCountLookup(List<Card> cards)
        {
            var lookup = new Dictionary<(Suit, Rank), int>();
            foreach (var card in cards)
            {
                var key = (card.Suit, card.Rank);
                if (!lookup.ContainsKey(key))
                    lookup[key] = 0;
                lookup[key]++;
            }

            return lookup;
        }

        private IEnumerable<Card> EnumerateDeckCardTypes()
        {
            var normalSuits = new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond };
            var normalRanks = Enum.GetValues(typeof(Rank))
                .Cast<Rank>()
                .Where(rank => rank != Rank.SmallJoker && rank != Rank.BigJoker);

            foreach (var suit in normalSuits)
            {
                foreach (var rank in normalRanks)
                {
                    yield return new Card(suit, rank);
                }
            }

            yield return new Card(Suit.Joker, Rank.SmallJoker);
            yield return new Card(Suit.Joker, Rank.BigJoker);
        }

        private ThrowSystem ResolveThrowSystem(Card card)
        {
            var category = _config.GetCardCategory(card);
            if (category == CardCategory.Trump)
            {
                return new ThrowSystem(CardCategory.Trump, Suit.Joker, "Trump");
            }

            return new ThrowSystem(CardCategory.Suit, card.Suit, $"Suit:{card.Suit}");
        }

        private bool IsCardInSystem(Card card, ThrowSystem system)
        {
            if (system.Category == CardCategory.Trump)
                return _config.IsTrump(card);

            return !_config.IsTrump(card) && card.Suit == system.Suit;
        }

        private bool IsSameSuitOrTrump(List<Card> cards)
        {
            if (cards.Count == 0)
                return false;

            bool allTrump = cards.All(_config.IsTrump);
            if (allTrump)
                return true;

            bool allSuit = cards.All(c => !_config.IsTrump(c));
            if (!allSuit)
                return false;

            var firstSuit = cards[0].Suit;
            return cards.All(c => c.Suit == firstSuit);
        }

        private List<ThrowComponent> DecomposeThrowComponents(List<Card> throwCards, CardComparer comparer)
        {
            var result = new List<ThrowComponent>();
            var validator = new ThrowValidator(_config);
            var components = validator.DecomposeThrow(throwCards);
            if (components.Count == 0)
                return result;

            foreach (var componentCards in components)
            {
                if (componentCards.Count == 0)
                    continue;

                var sorted = componentCards.OrderByDescending(card => card, comparer).ToList();
                var highest = sorted[0];

                if (componentCards.Count == 1)
                {
                    result.Add(new ThrowComponent(ThrowComponentType.Single, componentCards, highest, 0));
                    continue;
                }

                if (componentCards.Count == 2 && CardPattern.IsPair(componentCards))
                {
                    result.Add(new ThrowComponent(ThrowComponentType.Pair, componentCards, highest, 1));
                    continue;
                }

                var pattern = new CardPattern(componentCards, _config);
                if (pattern.IsTractor(componentCards))
                {
                    result.Add(new ThrowComponent(ThrowComponentType.Tractor, componentCards, highest, componentCards.Count / 2));
                    continue;
                }

                // 兜底：不认识的组件拆成单张保守处理
                foreach (var card in componentCards)
                {
                    result.Add(new ThrowComponent(ThrowComponentType.Single, new List<Card> { card }, card, 0));
                }
            }

            return result;
        }

        private int CountEligibleSingleBlockers(List<int> opponentPositions, ThrowSystem system)
        {
            return opponentPositions.Count(pos => !IsPlayerVoidInSystem(pos, system));
        }

        private int CountEligiblePairBlockers(List<int> opponentPositions, ThrowSystem system)
        {
            return opponentPositions.Count(pos =>
                !IsPlayerVoidInSystem(pos, system) &&
                !HasNoPairEvidence(pos, system.Key));
        }

        private bool IsPlayerVoidInSystem(int playerPosition, ThrowSystem system)
        {
            if (system.Category == CardCategory.Trump)
                return IsPlayerVoidTrump(playerPosition);

            return IsPlayerVoid(playerPosition, system.Suit);
        }

        private bool HasNoPairEvidence(int playerPosition, string systemKey)
        {
            if (!_playerNoPairSystems.TryGetValue(playerPosition, out var set))
                return false;

            return set.Contains(systemKey);
        }

        private bool HasNoPairEvidenceForAll(List<int> opponentPositions, string systemKey)
        {
            if (opponentPositions.Count == 0)
                return false;

            return opponentPositions.All(pos => HasNoPairEvidence(pos, systemKey));
        }

        private int CountBiggerPairUnits(List<Card> systemPool, Card target, CardComparer comparer)
        {
            return systemPool
                .GroupBy(card => card)
                .Where(group => group.Count() >= 2 && comparer.Compare(group.Key, target) > 0)
                .Sum(group => group.Count() / 2);
        }

        private int CountTotalPairUnits(List<Card> systemPool)
        {
            return systemPool
                .GroupBy(card => card)
                .Sum(group => group.Count() / 2);
        }

        private int CountBiggerTractorOptions(
            List<Card> systemPool,
            ThrowSystem system,
            int pairCount,
            Card targetHighest,
            CardComparer comparer)
        {
            var windows = BuildTractorWindows(systemPool, system, pairCount);
            return windows.Count(window => comparer.Compare(window.Highest, targetHighest) > 0);
        }

        private int CountTotalTractorOptions(List<Card> systemPool, ThrowSystem system, int pairCount)
        {
            return BuildTractorWindows(systemPool, system, pairCount).Count;
        }

        private List<TractorWindow> BuildTractorWindows(List<Card> systemPool, ThrowSystem system, int pairCount)
        {
            var windows = new List<TractorWindow>();
            if (pairCount < 2)
                return windows;

            var chain = BuildSystemOrderChain(system);
            if (chain.Count < pairCount)
                return windows;

            var pairAvailability = BuildPairAvailability(systemPool);
            for (int start = 0; start <= chain.Count - pairCount; start++)
            {
                bool ok = true;
                for (int offset = 0; offset < pairCount; offset++)
                {
                    var key = (chain[start + offset].Suit, chain[start + offset].Rank);
                    if (!pairAvailability.TryGetValue(key, out int count) || count < 1)
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    windows.Add(new TractorWindow(chain[start]));
                }
            }

            return windows;
        }

        private Dictionary<(Suit, Rank), int> BuildPairAvailability(List<Card> cards)
        {
            return cards
                .GroupBy(card => (card.Suit, card.Rank))
                .ToDictionary(group => group.Key, group => group.Count() / 2);
        }

        private List<Card> BuildSystemOrderChain(ThrowSystem system)
        {
            var chain = new List<Card>();
            var rankOrder = new[]
            {
                Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten,
                Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five,
                Rank.Four, Rank.Three, Rank.Two
            };

            if (system.Category == CardCategory.Suit)
            {
                foreach (var rank in rankOrder)
                {
                    if (rank == _config.LevelRank)
                        continue;
                    chain.Add(new Card(system.Suit, rank));
                }

                return chain;
            }

            chain.Add(new Card(Suit.Joker, Rank.BigJoker));
            chain.Add(new Card(Suit.Joker, Rank.SmallJoker));

            // 主牌体系中的级牌位：只取一个代表牌，按比较器大小均等对待
            var levelSuit = _config.TrumpSuit ?? Suit.Spade;
            chain.Add(new Card(levelSuit, _config.LevelRank));

            if (!_config.TrumpSuit.HasValue)
                return chain;

            foreach (var rank in rankOrder)
            {
                if (rank == _config.LevelRank)
                    continue;
                chain.Add(new Card(_config.TrumpSuit.Value, rank));
            }

            return chain;
        }

        private void TrackNoPairEvidence(List<TrickPlay> plays)
        {
            if (plays.Count == 0)
                return;

            var leadCards = plays[0].Cards;
            if (leadCards == null || leadCards.Count != 2 || !CardPattern.IsPair(leadCards))
                return;

            var leadSystem = ResolveThrowSystem(leadCards[0]);
            foreach (var play in plays)
            {
                if (play.PlayerIndex == plays[0].PlayerIndex)
                    continue;

                bool followedPair = HasPairInSystem(play.Cards, leadSystem);
                if (!followedPair)
                    MarkPlayerNoPairEvidence(play.PlayerIndex, leadSystem.Key);
            }
        }

        private bool HasPairInSystem(List<Card> cards, ThrowSystem system)
        {
            return cards
                .Where(card => IsCardInSystem(card, system))
                .GroupBy(card => card)
                .Any(group => group.Count() >= 2);
        }

        private void MarkPlayerNoPairEvidence(int playerPosition, string systemKey)
        {
            if (!_playerNoPairSystems.ContainsKey(playerPosition))
                _playerNoPairSystems[playerPosition] = new HashSet<string>();

            _playerNoPairSystems[playerPosition].Add(systemKey);
        }

        private static double Clamp01(double value)
        {
            if (value < 0) return 0;
            if (value > 1) return 1;
            return value;
        }

        private sealed class ThrowSystem
        {
            public CardCategory Category { get; }
            public Suit Suit { get; }
            public string Key { get; }

            public ThrowSystem(CardCategory category, Suit suit, string key)
            {
                Category = category;
                Suit = suit;
                Key = key;
            }
        }

        private enum ThrowComponentType
        {
            Single,
            Pair,
            Tractor
        }

        private sealed class ThrowComponent
        {
            public ThrowComponentType Type { get; }
            public List<Card> Cards { get; }
            public Card HighestCard { get; }
            public int PairCount { get; }

            public ThrowComponent(ThrowComponentType type, List<Card> cards, Card highestCard, int pairCount)
            {
                Type = type;
                Cards = cards;
                HighestCard = highestCard;
                PairCount = pairCount;
            }
        }

        private sealed class ComponentRisk
        {
            public static readonly ComponentRisk Safe = new(false, 0);

            public bool CanBeBlocked { get; }
            public double BlockProbability { get; }

            public ComponentRisk(bool canBeBlocked, double blockProbability)
            {
                CanBeBlocked = canBeBlocked;
                BlockProbability = Clamp01(blockProbability);
            }
        }

        private sealed class TractorWindow
        {
            public Card Highest { get; }

            public TractorWindow(Card highest)
            {
                Highest = highest;
            }
        }

        /// <summary>
        /// 重置记牌（新局开始）
        /// </summary>
        public void Reset()
        {
            _playedCards.Clear();
            _playerVoidSuits.Clear();
            _playerNoPairSystems.Clear();
        }
    }
}
