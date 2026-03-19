using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.Models;
using TractorGame.Core.Rules;

namespace TractorGame.Core.AI.V30.Follow
{
    public sealed class FollowPolicyV30
    {
        private readonly FollowCandidateOverlayV30 _overlay;

        public FollowPolicyV30(FollowCandidateOverlayV30? overlay = null)
        {
            _overlay = overlay ?? new FollowCandidateOverlayV30();
        }

        public FollowDecisionV30 Decide(
            RuleAIContext context,
            IReadOnlyList<List<Card>>? legalCandidates = null)
        {
            var candidates = ResolveLegalCandidates(context, legalCandidates);
            if (candidates.Count == 0)
            {
                return new FollowDecisionV30
                {
                    SelectedCards = new List<Card>(),
                    Intent = FollowOverlayIntentV30.MinimizeLoss,
                    RankedCandidates = new List<FollowCandidateViewV30>(),
                    SelectedReason = "no_legal_follow_candidate"
                };
            }

            var ranked = _overlay.BuildAndRank(context, candidates);
            var selected = ranked.First();
            var intent = _overlay.ResolveIntent(context, ranked);

            return new FollowDecisionV30
            {
                SelectedCards = new List<Card>(selected.Cards),
                Intent = intent,
                RankedCandidates = ranked,
                SelectedReason = selected.Reason
            };
        }

        private List<List<Card>> ResolveLegalCandidates(
            RuleAIContext context,
            IReadOnlyList<List<Card>>? legalCandidates)
        {
            if (legalCandidates != null)
                return CloneCandidates(legalCandidates);

            if (context.LegalActions.Count > 0)
                return ExpandLowLossCoverage(context, CloneCandidates(context.LegalActions));

            return ExpandLowLossCoverage(context, new FollowCandidateGenerator(context.GameConfig).Generate(context));
        }

        private static List<List<Card>> CloneCandidates(IReadOnlyList<List<Card>> source)
        {
            return source.Select(cards => new List<Card>(cards)).ToList();
        }

        private static List<List<Card>> ExpandLowLossCoverage(
            RuleAIContext context,
            List<List<Card>> candidates)
        {
            if (context.MyHand.Count == 0 || context.LeadCards.Count == 0)
                return candidates;

            int need = context.LeadCards.Count;
            if (need <= 0)
                return candidates;

            var validator = new FollowValidator(context.GameConfig);
            var comparer = new CardComparer(context.GameConfig);
            var leadCategory = context.GameConfig.GetCardCategory(context.LeadCards[0]);
            var leadSuit = context.LeadCards[0].Suit;
            var sameCategoryCards = context.MyHand
                .Where(card => RuleAIUtility.MatchesLeadCategory(context.GameConfig, card, leadSuit, leadCategory))
                .ToList();

            var extraCandidates = new List<List<Card>>();
            if (sameCategoryCards.Count >= need)
            {
                AddRepresentativeCombos(
                    context,
                    sameCategoryCards,
                    need,
                    validator,
                    comparer,
                    extraCandidates);
            }
            else
            {
                int missing = need - sameCategoryCards.Count;
                var remaining = RuleAIUtility.RemoveCards(context.MyHand, sameCategoryCards);
                if (missing > 0)
                {
                    AddRepresentativeCombos(
                        context,
                        remaining,
                        missing,
                        validator,
                        comparer,
                        extraCandidates,
                        prefix: sameCategoryCards);
                }
            }

            return RuleAIUtility.DeduplicateCandidates(candidates.Concat(extraCandidates));
        }

        private static void AddRepresentativeCombos(
            RuleAIContext context,
            List<Card> source,
            int choose,
            FollowValidator validator,
            CardComparer comparer,
            List<List<Card>> target,
            List<Card>? prefix = null)
        {
            if (choose <= 0 || source.Count < choose)
                return;

            long combinationCount = EstimateCombinationCount(source.Count, choose);
            if (combinationCount > 120 || choose > 4)
                return;

            var allValid = Combinations(source, choose)
                .Select(combo =>
                {
                    var candidate = prefix == null
                        ? combo
                        : prefix.Concat(combo).ToList();
                    return candidate;
                })
                .Where(candidate => candidate.Count == context.LeadCards.Count &&
                    validator.IsValidFollow(context.MyHand, context.LeadCards, candidate))
                .ToList();
            if (allValid.Count == 0)
                return;

            foreach (var candidate in allValid
                .OrderBy(candidate => candidate.Sum(card => card.Score))
                .ThenBy(candidate => RuleAIUtility.CountHighControlCards(context.GameConfig, candidate))
                .ThenBy(candidate => candidate.Count(context.GameConfig.IsTrump))
                .ThenBy(candidate => RuleAIUtility.EstimateStructureLoss(context.GameConfig, context.MyHand, candidate, comparer))
                .ThenBy(candidate => RuleAIUtility.BuildCandidateKey(candidate))
                .Take(6))
            {
                target.Add(candidate);
            }
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
    }
}
