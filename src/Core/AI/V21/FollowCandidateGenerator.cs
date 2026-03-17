using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 生成跟牌阶段的有限策略候选。
    /// </summary>
    public sealed class FollowCandidateGenerator
    {
        private readonly GameConfig _config;

        public FollowCandidateGenerator(GameConfig config)
        {
            _config = config;
        }

        public List<List<Card>> Generate(RuleAIContext context)
        {
            if (context.MyHand.Count == 0 || context.LeadCards.Count == 0)
                return new List<List<Card>>();

            var hand = context.MyHand;
            var leadCards = context.LeadCards;
            var currentWinningCards = context.CurrentWinningCards.Count > 0 ? context.CurrentWinningCards : leadCards;
            var comparer = new CardComparer(_config);
            var validator = new FollowValidator(_config);
            int need = leadCards.Count;

            if (hand.Count <= need)
                return new List<List<Card>> { new List<Card>(hand) };

            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand
                .Where(card => RuleAIUtility.MatchesLeadCategory(_config, card, leadSuit, leadCategory))
                .ToList();

            var candidates = new List<List<Card>>();
            if (sameCategoryCards.Count >= need)
            {
                BuildEnoughSameSuitCandidates(candidates, context, sameCategoryCards, currentWinningCards, need, comparer);
            }
            else
            {
                BuildShortageCandidates(candidates, context, sameCategoryCards, currentWinningCards, need, comparer);
            }

            candidates.Add(BuildFallbackFollowCandidate(hand, leadCards, need, comparer));

            return RuleAIUtility.DeduplicateCandidates(candidates)
                .Where(candidate => candidate.Count == need && validator.IsValidFollow(hand, leadCards, candidate))
                .ToList();
        }

        private void BuildEnoughSameSuitCandidates(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> sameCategoryCards,
            List<Card> currentWinningCards,
            int need,
            CardComparer comparer)
        {
            candidates.Add(sameCategoryCards
                .OrderBy(card => RuleAIUtility.EstimateSingleCardDiscardCost(_config, sameCategoryCards, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(need)
                .ToList());

            candidates.Add(sameCategoryCards.OrderBy(card => card, comparer).Take(need).ToList());
            candidates.Add(sameCategoryCards.OrderByDescending(card => card, comparer).Take(need).ToList());

            if (context.PartnerWinning)
            {
                candidates.Add(sameCategoryCards
                    .OrderByDescending(card => card.Score)
                    .ThenBy(card => RuleAIUtility.EstimateSingleCardDiscardCost(_config, sameCategoryCards, card, comparer))
                    .ThenBy(card => RuleAIUtility.GetCardValue(card, _config))
                    .ThenBy(card => card, comparer)
                    .Take(need)
                    .ToList());
            }

            if (need == 1)
            {
                var threatAnalyzer = new FollowThreatAnalyzer(_config);
                var winningSingles = sameCategoryCards
                    .Where(card => RuleAIUtility.CanBeatCards(_config, currentWinningCards, new List<Card> { card }))
                    .OrderBy(card => card, comparer)
                    .ToList();
                if (winningSingles.Count > 0)
                {
                    AddSingletonCandidate(candidates, winningSingles[0]);

                    int lowerMidIndex = System.Math.Max(0, (winningSingles.Count / 2) - 1);
                    AddSingletonCandidate(candidates, winningSingles[lowerMidIndex]);
                    AddSingletonCandidate(candidates, winningSingles[winningSingles.Count / 2]);

                    var firstStable = winningSingles
                        .Select(card => new
                        {
                            Card = card,
                            Assessment = threatAnalyzer.Analyze(context, new List<Card> { card }, currentWinningCards)
                        })
                        .Where(item => item.Assessment.SecurityLevel >= WinSecurityLevel.Stable)
                        .OrderBy(item => item.Card, comparer)
                        .FirstOrDefault();
                    if (firstStable != null)
                        AddSingletonCandidate(candidates, firstStable.Card);

                    var strongestNonControl = winningSingles
                        .Where(card => RuleAIUtility.CountHighControlCards(_config, new[] { card }) == 0)
                        .OrderByDescending(card => card, comparer)
                        .FirstOrDefault();
                    if (strongestNonControl != null)
                        AddSingletonCandidate(candidates, strongestNonControl);

                    var highControlWinner = winningSingles
                        .Where(card => RuleAIUtility.CountHighControlCards(_config, new[] { card }) > 0)
                        .OrderByDescending(card => card, comparer)
                        .FirstOrDefault();
                    if (highControlWinner != null)
                        AddSingletonCandidate(candidates, highControlWinner);

                    AddSingletonCandidate(candidates, winningSingles[^1]);
                }

                return;
            }

            if (need == 2)
            {
                var smallPair = RuleAIUtility.FindSmallestPair(sameCategoryCards, comparer);
                if (smallPair != null)
                    candidates.Add(smallPair);

                var bigPair = RuleAIUtility.FindStrongestPair(sameCategoryCards, comparer);
                if (bigPair != null)
                    candidates.Add(bigPair);

                if (currentWinningCards.Count == 2 && CardPattern.IsPair(currentWinningCards))
                {
                    var pairs = sameCategoryCards
                        .GroupBy(card => card)
                        .Where(group => group.Count() >= 2)
                        .Select(group => group.Take(2).ToList())
                        .Where(pair => RuleAIUtility.CanBeatCards(_config, currentWinningCards, pair))
                        .OrderBy(pair => pair[0], comparer)
                        .ToList();
                    if (pairs.Count > 0)
                    {
                        candidates.Add(pairs[0]);
                        candidates.Add(pairs[^1]);
                    }
                }

                return;
            }

            if (need == 3)
            {
                // 对子 + 最小单张
                var bigPair = RuleAIUtility.FindStrongestPair(sameCategoryCards, comparer);
                if (bigPair != null)
                {
                    var rest = RuleAIUtility.RemoveCards(sameCategoryCards, bigPair);
                    if (rest.Count > 0)
                        candidates.Add(bigPair.Concat(new[] { rest.OrderBy(c => c, comparer).First() }).ToList());
                }

                var smallPair = RuleAIUtility.FindSmallestPair(sameCategoryCards, comparer);
                if (smallPair != null)
                {
                    var rest = RuleAIUtility.RemoveCards(sameCategoryCards, smallPair);
                    if (rest.Count > 0)
                        candidates.Add(smallPair.Concat(new[] { rest.OrderByDescending(c => c.Score).ThenBy(c => c, comparer).First() }).ToList());
                }

                return;
            }

            if (need >= 4 && need % 2 == 0)
            {
                var smallTractor = RuleAIUtility.FindSmallestTractor(_config, sameCategoryCards, need, comparer);
                if (smallTractor != null)
                    candidates.Add(smallTractor);

                var strongTractor = RuleAIUtility.FindStrongestTractor(_config, sameCategoryCards, need, comparer);
                if (strongTractor != null)
                    candidates.Add(strongTractor);
            }
        }

        private void BuildShortageCandidates(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> sameCategoryCards,
            List<Card> currentWinningCards,
            int need,
            CardComparer comparer)
        {
            var mustFollow = sameCategoryCards.OrderBy(card => card, comparer).ToList();
            int missing = need - mustFollow.Count;
            var remaining = RuleAIUtility.RemoveCards(context.MyHand, mustFollow);

            candidates.Add(AppendFiller(
                mustFollow,
                remaining.OrderBy(card => _config.IsTrump(card) ? 1 : 0)
                    .ThenBy(card => RuleAIUtility.EstimateSingleCardDiscardCost(_config, remaining, card, comparer))
                    .ThenBy(card => card, comparer)
                    .ToList(),
                missing));

            candidates.Add(AppendFiller(
                mustFollow,
                remaining.OrderBy(card => RuleAIUtility.EstimateSingleCardDiscardCost(_config, remaining, card, comparer))
                    .ThenBy(card => card, comparer)
                    .ToList(),
                missing));

            candidates.Add(AppendFiller(
                mustFollow,
                remaining.OrderBy(card => card.Score)
                    .ThenBy(card => RuleAIUtility.GetCardValue(card, _config))
                    .ThenBy(card => card, comparer)
                    .ToList(),
                missing));

            if (context.PartnerWinning)
            {
                candidates.Add(AppendFiller(
                    mustFollow,
                    remaining.OrderBy(card => _config.IsTrump(card) ? 1 : 0)
                        .ThenByDescending(card => card.Score)
                        .ThenBy(card => card, comparer)
                        .ToList(),
                    missing));
                // 不 return，继续生成切主候选（队友赢低分墩时对手可能反超）
            }
            else
            {
                candidates.Add(AppendFiller(
                    mustFollow,
                    remaining.OrderByDescending(card => card.Score)
                        .ThenBy(card => _config.IsTrump(card) ? 0 : 1)
                        .ThenBy(card => card, comparer)
                        .ToList(),
                    missing));
            }

            AddWinningTrumpCutCandidates(
                candidates,
                context,
                mustFollow,
                remaining,
                currentWinningCards,
                need,
                missing,
                comparer);
        }

        private static List<Card> AppendFiller(List<Card> mustFollow, List<Card> orderedFiller, int missing)
        {
            var result = new List<Card>(mustFollow);
            result.AddRange(orderedFiller.Take(missing));
            return result;
        }

        private static void AddSingletonCandidate(List<List<Card>> candidates, Card? card)
        {
            if (card != null)
                candidates.Add(new List<Card> { card });
        }

        private void AddWinningTrumpCutCandidates(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> mustFollow,
            List<Card> remaining,
            List<Card> currentWinningCards,
            int need,
            int missing,
            CardComparer comparer)
        {
            // 非主首引时，只在完全断门的情况下才可能整手主牌毙牌获胜。
            if (missing <= 0 || mustFollow.Count != 0 || currentWinningCards.Count != need)
                return;

            var trumpCards = remaining
                .Where(_config.IsTrump)
                .OrderBy(card => card, comparer)
                .ToList();
            if (trumpCards.Count < missing)
                return;

            var heuristicPool = new List<List<Card>>();
            AddCandidateIfSized(heuristicPool, trumpCards.Take(missing).ToList(), missing);
            AddCandidateIfSized(heuristicPool, trumpCards.OrderByDescending(card => card, comparer).Take(missing).ToList(), missing);
            AddCandidateIfSized(heuristicPool, trumpCards
                .OrderBy(card => EvaluateWinningTrumpCardCost(remaining, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(missing)
                .ToList(), missing);
            AddCandidateIfSized(heuristicPool, trumpCards
                .OrderByDescending(card => card.Score)
                .ThenBy(card => EvaluateWinningTrumpCardCost(remaining, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(missing)
                .ToList(), missing);

            int windowSize = System.Math.Min(
                trumpCards.Count,
                System.Math.Max(missing + 2, System.Math.Min(missing + 4, 8)));
            AddWindowCombos(heuristicPool, trumpCards, missing, comparer, windowSize);

            long combinationCount = EstimateCombinationCount(trumpCards.Count, missing);
            if (missing <= 3 || combinationCount <= 256)
            {
                int explored = 0;
                foreach (var combo in Combinations(trumpCards, missing))
                {
                    heuristicPool.Add(combo);
                    explored++;
                    if (explored >= 512)
                        break;
                }
            }

            var winningCombos = RuleAIUtility.DeduplicateCandidates(heuristicPool)
                .Where(candidate => candidate.Count == missing &&
                    RuleAIUtility.CanBeatCards(_config, currentWinningCards, candidate))
                .ToList();
            if (winningCombos.Count == 0)
                return;

            var representativeCombos = new List<List<Card>>
            {
                winningCombos
                    .OrderBy(candidate => EvaluateWinningTrumpCutCost(context.MyHand, candidate, comparer))
                    .ThenByDescending(candidate => candidate.Sum(card => card.Score))
                    .ThenBy(candidate => RuleAIUtility.BuildCandidateKey(candidate))
                    .First(),
                winningCombos
                    .OrderByDescending(candidate => candidate.Sum(card => card.Score))
                    .ThenBy(candidate => EvaluateWinningTrumpCutCost(context.MyHand, candidate, comparer))
                    .ThenBy(candidate => RuleAIUtility.BuildCandidateKey(candidate))
                    .First(),
                winningCombos
                    .OrderBy(candidate => RuleAIUtility.EstimateStructureLoss(_config, context.MyHand, candidate, comparer))
                    .ThenBy(candidate => EvaluateWinningTrumpCutCost(context.MyHand, candidate, comparer))
                    .ThenBy(candidate => RuleAIUtility.BuildCandidateKey(candidate))
                    .First(),
                PickStrongestCandidate(winningCombos, comparer)
            };

            foreach (var combo in RuleAIUtility.DeduplicateCandidates(representativeCombos))
                candidates.Add(combo);
        }

        private void AddWindowCombos(
            List<List<Card>> candidatePool,
            List<Card> trumpCards,
            int choose,
            CardComparer comparer,
            int windowSize)
        {
            if (windowSize < choose)
                return;

            var lowWindow = trumpCards.Take(windowSize).ToList();
            var highWindow = trumpCards
                .OrderByDescending(card => card, comparer)
                .Take(windowSize)
                .ToList();
            var preserveWindow = trumpCards
                .OrderBy(card => EvaluateWinningTrumpCardCost(trumpCards, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(windowSize)
                .ToList();
            var scoreWindow = trumpCards
                .OrderByDescending(card => card.Score)
                .ThenBy(card => EvaluateWinningTrumpCardCost(trumpCards, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(windowSize)
                .ToList();

            foreach (var pool in new[] { lowWindow, highWindow, preserveWindow, scoreWindow })
            {
                foreach (var combo in Combinations(pool, choose))
                    candidatePool.Add(combo);
            }
        }

        private void AddCandidateIfSized(List<List<Card>> candidatePool, List<Card> candidate, int expectedSize)
        {
            if (candidate.Count == expectedSize)
                candidatePool.Add(candidate);
        }

        private double EvaluateWinningTrumpCardCost(List<Card> source, Card card, CardComparer comparer)
        {
            return RuleAIUtility.EstimateSingleCardDiscardCost(_config, source, card, comparer);
        }

        private double EvaluateWinningTrumpCutCost(List<Card> source, List<Card> candidate, CardComparer comparer)
        {
            return RuleAIUtility.EstimateStructureLoss(_config, source, candidate, comparer) * 12.0 +
                RuleAIUtility.CountHighControlCards(_config, candidate) * 8.0 +
                candidate.Sum(card => RuleAIUtility.GetCardValue(card, _config)) / 80.0 -
                candidate.Sum(card => card.Score) * 1.2;
        }

        private List<Card> PickStrongestCandidate(List<List<Card>> candidates, CardComparer comparer)
        {
            var best = candidates[0];
            foreach (var candidate in candidates.Skip(1))
            {
                if (RuleAIUtility.CompareCardSets(_config, candidate, best, comparer) > 0)
                    best = candidate;
            }

            return best;
        }

        private static long EstimateCombinationCount(int n, int k)
        {
            if (k < 0 || k > n)
                return 0;
            if (k == 0 || k == n)
                return 1;

            k = System.Math.Min(k, n - k);
            long result = 1;
            for (int i = 1; i <= k; i++)
                result = result * (n - k + i) / i;
            return result;
        }

        private static IEnumerable<List<Card>> Combinations(List<Card> items, int choose)
        {
            var buffer = new List<Card>(choose);
            foreach (var combo in CombinationsCore(items, choose, 0, buffer))
                yield return combo;
        }

        private static IEnumerable<List<Card>> CombinationsCore(List<Card> items, int choose, int start, List<Card> buffer)
        {
            if (buffer.Count == choose)
            {
                yield return new List<Card>(buffer);
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

        private List<Card> BuildFallbackFollowCandidate(List<Card> hand, List<Card> leadCards, int need, CardComparer comparer)
        {
            var leadCategory = _config.GetCardCategory(leadCards[0]);
            var leadSuit = leadCards[0].Suit;
            var sameCategoryCards = hand
                .Where(card => RuleAIUtility.MatchesLeadCategory(_config, card, leadSuit, leadCategory))
                .ToList();

            if (sameCategoryCards.Count >= need)
                return sameCategoryCards.OrderBy(card => card, comparer).Take(need).ToList();

            var result = new List<Card>(sameCategoryCards.OrderBy(card => card, comparer));
            var remaining = RuleAIUtility.RemoveCards(hand, sameCategoryCards);
            result.AddRange(remaining.OrderBy(card => card, comparer).Take(need - result.Count));
            return result;
        }
    }
}
