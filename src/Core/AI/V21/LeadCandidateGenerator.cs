using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 生成首发阶段的有限合理候选，避免全量爆搜。
    /// </summary>
    public sealed class LeadCandidateGenerator
    {
        private readonly GameConfig _config;
        private readonly CardMemory? _memory;

        public LeadCandidateGenerator(GameConfig config, CardMemory? memory = null)
        {
            _config = config;
            _memory = memory;
        }

        public List<List<Card>> Generate(RuleAIContext context)
        {
            if (context.MyHand.Count == 0)
                return new List<List<Card>>();

            var hand = context.MyHand;
            var comparer = new CardComparer(_config);
            var validator = new PlayValidator(_config);
            var candidates = new List<List<Card>>();

            foreach (var group in RuleAIUtility.BuildSystemGroups(_config, hand))
            {
                var sortedSmall = group.OrderBy(card => card, comparer).ToList();
                var sortedBig = group.OrderByDescending(card => card, comparer).ToList();

                candidates.Add(new List<Card> { sortedSmall[0] });
                candidates.Add(new List<Card> { sortedBig[0] });

                var smallPair = RuleAIUtility.FindSmallestPair(group, comparer);
                if (smallPair != null)
                    candidates.Add(smallPair);

                var bigPair = RuleAIUtility.FindStrongestPair(group, comparer);
                if (bigPair != null)
                    candidates.Add(bigPair);

                int pairUnits = group.GroupBy(card => (card.Suit, card.Rank)).Count(g => g.Count() >= 2);
                // 从最长到最短，每种长度都加入候选（不 break），避免漏掉较短但更安全的拖拉机
                bool addedStrong = false;
                for (int tractorPairs = pairUnits; tractorPairs >= 2; tractorPairs--)
                {
                    var strongTractor = RuleAIUtility.FindStrongestTractor(_config, group, tractorPairs * 2, comparer);
                    if (strongTractor != null)
                    {
                        candidates.Add(strongTractor);
                        if (!addedStrong) addedStrong = true;
                        else break; // 最长和次长各加一个即可，避免候选爆炸
                    }
                }

                bool addedSmall = false;
                for (int tractorPairs = 2; tractorPairs <= pairUnits; tractorPairs++)
                {
                    var smallTractor = RuleAIUtility.FindSmallestTractor(_config, group, tractorPairs * 2, comparer);
                    if (smallTractor != null)
                    {
                        candidates.Add(smallTractor);
                        if (!addedSmall) addedSmall = true;
                        else break;
                    }
                }

                if (group.Count >= 3 &&
                    ShouldIncludeFullGroupMixedThrow(context, group) &&
                    ShouldIncludeMixedThrowCandidate(context, group, validator))
                    candidates.Add(new List<Card>(group));

                AddMixedSubsetThrowCandidates(candidates, context, group, comparer, validator);
            }

            var structureProtectSingle = hand
                .OrderBy(card => RuleAIUtility.EstimateSingleCardDiscardCost(_config, hand, card, comparer))
                .ThenBy(card => card, comparer)
                .Take(1)
                .ToList();
            if (structureProtectSingle.Count == 1)
                candidates.Add(structureProtectSingle);

            var lowTrump = hand.Where(_config.IsTrump).OrderBy(card => card, comparer).Take(1).ToList();
            if (lowTrump.Count == 1)
                candidates.Add(lowTrump);

            return RuleAIUtility.DeduplicateCandidates(candidates)
                .Where(candidate => validator.IsValidPlay(hand, candidate))
                .ToList();
        }

        private static bool ShouldIncludeFullGroupMixedThrow(RuleAIContext context, List<Card> group)
        {
            if (group.Count <= 4)
                return true;

            return context.DecisionFrame.EndgameLevel != EndgameLevel.None ||
                group.Count == context.MyHand.Count;
        }

        private void AddMixedSubsetThrowCandidates(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> group,
            CardComparer comparer,
            PlayValidator validator)
        {
            if (group.Count < 3)
                return;

            var strongestPair = RuleAIUtility.FindStrongestPair(group, comparer);
            var smallestPair = RuleAIUtility.FindSmallestPair(group, comparer);
            var strongestSingle = group.OrderByDescending(card => card, comparer).FirstOrDefault();
            var smallestSingle = group.OrderBy(card => card, comparer).FirstOrDefault();

            AddMixedCandidate(candidates, context, group, strongestPair, strongestSingle, comparer, validator);
            AddMixedCandidate(candidates, context, group, strongestPair, smallestSingle, comparer, validator);
            AddMixedCandidate(candidates, context, group, smallestPair, strongestSingle, comparer, validator);

            int pairUnits = group.GroupBy(card => (card.Suit, card.Rank)).Count(bucket => bucket.Count() >= 2);
            if (pairUnits >= 2)
            {
                var strongestTractor = RuleAIUtility.FindStrongestTractor(_config, group, 4, comparer);
                var smallestTractor = RuleAIUtility.FindSmallestTractor(_config, group, 4, comparer);

                AddMixedCandidate(candidates, context, group, strongestTractor, strongestSingle, comparer, validator);
                AddMixedCandidate(candidates, context, group, strongestTractor, smallestSingle, comparer, validator);
                AddMixedCandidate(candidates, context, group, strongestTractor, strongestPair, comparer, validator);
                AddMixedCandidate(candidates, context, group, smallestTractor, strongestSingle, comparer, validator);
            }
        }

        private void AddMixedCandidate(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> group,
            List<Card>? baseComponent,
            Card? single,
            CardComparer comparer,
            PlayValidator validator)
        {
            if (baseComponent == null || single == null)
                return;

            var remaining = RuleAIUtility.RemoveCards(group, baseComponent);
            var extra = remaining.FirstOrDefault(card => card.Equals(single)) ??
                remaining.OrderByDescending(card => card, comparer).FirstOrDefault();
            if (extra == null)
                return;

            var mixed = new List<Card>(baseComponent) { extra };
            AddValidatedMixedCandidate(candidates, context, mixed, validator);
        }

        private void AddMixedCandidate(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> group,
            List<Card>? baseComponent,
            List<Card>? extraComponent,
            CardComparer comparer,
            PlayValidator validator)
        {
            if (baseComponent == null || extraComponent == null)
                return;

            var remaining = RuleAIUtility.RemoveCards(group, baseComponent);
            var extra = TryTakeComponent(remaining, extraComponent);
            if (extra.Count != extraComponent.Count)
                return;

            var mixed = new List<Card>(baseComponent);
            mixed.AddRange(extra);
            AddValidatedMixedCandidate(candidates, context, mixed, validator);
        }

        private static List<Card> TryTakeComponent(List<Card> source, List<Card> component)
        {
            var copy = new List<Card>(source);
            var result = new List<Card>();
            foreach (var card in component)
            {
                var match = copy.FirstOrDefault(existing => existing.Equals(card));
                if (match == null)
                    return new List<Card>();

                result.Add(match);
                copy.Remove(match);
            }

            return result;
        }

        private void AddValidatedMixedCandidate(
            List<List<Card>> candidates,
            RuleAIContext context,
            List<Card> candidate,
            PlayValidator validator)
        {
            if (candidate.Count < 3)
                return;

            if (!validator.IsValidPlay(context.MyHand, candidate))
                return;

            var pattern = new CardPattern(candidate, _config);
            if (pattern.Type != PatternType.Mixed)
                return;

            if (ShouldIncludeMixedThrowCandidate(context, candidate, validator))
                candidates.Add(candidate);
        }

        private bool ShouldIncludeMixedThrowCandidate(RuleAIContext context, List<Card> candidate, PlayValidator validator)
        {
            if (!validator.IsValidPlay(context.MyHand, candidate))
                return false;

            var pattern = new CardPattern(candidate, _config);
            // 非 Mixed 牌型（如拖拉机）不走安全性评估，直接拒绝作为 throw 候选
            if (pattern.Type != PatternType.Mixed)
                return false;

            if (_memory == null || context.PlayerIndex < 0)
                return false;

            var opponents = Enumerable.Range(0, 4)
                .Where(index => index != context.PlayerIndex)
                .ToList();
            var safety = _memory.EvaluateThrowSafety(
                candidate,
                context.MyHand,
                context.PlayerIndex,
                opponents,
                context.VisibleBottomCards);

            if (safety.IsDeterministicallySafe)
                return true;

            if (ShouldIncludePressureThrowFromEvidence(context, candidate, safety))
                return true;

            if (context.DecisionFrame.BottomRiskPressure >= RiskLevel.High)
                return false;

            var threshold = context.Difficulty switch
            {
                AIDifficulty.Easy => 0.98,
                AIDifficulty.Medium => 0.90,
                AIDifficulty.Hard => 0.78,
                _ => 0.68
            };

            threshold -= context.StyleProfile.ThrowRiskTolerance * 0.10;
            return safety.SuccessProbability >= threshold;
        }

        private bool ShouldIncludePressureThrowFromEvidence(
            RuleAIContext context,
            List<Card> candidate,
            CardMemory.ThrowSafetyAssessment safety)
        {
            if (_memory == null || context.PlayerIndex < 0)
                return false;

            var components = new ThrowValidator(_config).DecomposeThrow(candidate);
            bool hasPairPressure = components.Any(component => component.Count >= 2);
            if (!hasPairPressure)
                return false;

            string systemKey = ResolveSystemKey(candidate[0]);
            var noPairEvidence = _memory.GetNoPairEvidenceSnapshot();
            int opponentEvidenceCount = Enumerable.Range(0, 4)
                .Where(index => index != context.PlayerIndex)
                .Count(index => noPairEvidence.TryGetValue(index, out var systems) && systems.Contains(systemKey));

            if (opponentEvidenceCount < 2)
                return false;

            bool hasControlSingle = components.Any(component =>
                component.Count == 1 &&
                IsHighControlLead(component[0]));

            return hasControlSingle;
        }

        private string ResolveSystemKey(Card card)
        {
            return _config.GetCardCategory(card) == CardCategory.Trump
                ? "Trump"
                : $"Suit:{card.Suit}";
        }

        private bool IsHighControlLead(Card card)
        {
            if (card.Rank == Rank.BigJoker || card.Rank == Rank.SmallJoker)
                return true;

            if (card.Rank == Rank.Ace && !_config.IsTrump(card))
                return true;

            return _config.IsTrump(card) && RuleAIUtility.GetCardValue(card, _config) >= 700;
        }
    }
}
