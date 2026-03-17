using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 首发策略 2.0。
    /// </summary>
    public sealed class LeadPolicy2
    {
        private readonly LeadCandidateGenerator _candidateGenerator;
        private readonly IntentResolver _intentResolver;
        private readonly ActionScorer _actionScorer;
        private readonly DecisionExplainer _explainer;

        public LeadPolicy2(
            LeadCandidateGenerator candidateGenerator,
            IntentResolver intentResolver,
            ActionScorer actionScorer,
            DecisionExplainer explainer)
        {
            _candidateGenerator = candidateGenerator;
            _intentResolver = intentResolver;
            _actionScorer = actionScorer;
            _explainer = explainer;
        }

        public PhaseDecision Decide(RuleAIContext context)
        {
            var candidates = _candidateGenerator.Generate(context);
            var intent = _intentResolver.Resolve(context, candidates);
            var scored = _actionScorer.Score(context, intent, candidates);
            scored = ReorderForStructuredLead(context, intent, scored);
            var explanation = _explainer.Build(context, intent, scored, "LeadPolicy2");

            return new PhaseDecision
            {
                Phase = context.Phase,
                SelectedCards = scored.Count > 0 ? new List<Card>(scored[0].Cards) : new List<Card>(),
                Intent = intent,
                ScoredActions = scored,
                Explanation = explanation
            };
        }

        private static List<ScoredAction> ReorderForStructuredLead(
            RuleAIContext context,
            ResolvedIntent intent,
            List<ScoredAction> scored)
        {
            if (scored.Count <= 1)
                return scored;

            var config = context.GameConfig;
            if (ShouldPreferLowTrumpInLastTwo(context, scored))
            {
                scored = scored
                    .OrderBy(action => RuleAIUtility.GetCardValue(action.Cards[0], config))
                    .ThenBy(action => action.Score)
                    .ToList();
                return scored;
            }

            if (intent.PrimaryIntent == DecisionIntentKind.ProtectBottom ||
                intent.PrimaryIntent == DecisionIntentKind.PrepareEndgame)
                return scored;

            var top = scored[0];

            if (ShouldPreferEarlyDealerSideSuitPressure(context, intent, top, config))
            {
                var strongSideLead = scored
                    .Where(action => GetSideSuitPressureStrength(context, action, config) > 0)
                    .OrderByDescending(action => GetSideSuitPressureStrength(context, action, config))
                    .ThenByDescending(action => action.Score)
                    .FirstOrDefault();

                if (strongSideLead != null)
                {
                    scored.Remove(strongSideLead);
                    scored.Insert(0, strongSideLead);
                }
            }

            var tractor = scored
                .Where(action =>
                {
                    if (action.Cards.Count < 4 || action.Cards.Count % 2 != 0)
                        return false;

                    var pattern = new CardPattern(action.Cards, config);
                    return pattern.IsTractor(action.Cards);
                })
                .OrderByDescending(action => action.Cards.Count)
                .ThenByDescending(action => action.Score)
                .FirstOrDefault();

            if (tractor == null)
                return scored;

            scored.Remove(tractor);
            scored.Insert(0, tractor);
            return scored;
        }

        private static bool ShouldPreferLowTrumpInLastTwo(RuleAIContext context, List<ScoredAction> scored)
        {
            if (context.DecisionFrame.EndgameLevel != EndgameLevel.LastTrickRace)
                return false;

            if (context.MyHand.Count != 2)
                return false;

            if (scored.Count < 2)
                return false;

            if (scored.Any(action => action.Cards.Count != 1))
                return false;

            if (context.MyHand.Any(card => !context.GameConfig.IsTrump(card)))
                return false;

            return true;
        }

        private static bool ShouldPreferEarlyDealerSideSuitPressure(
            RuleAIContext context,
            ResolvedIntent intent,
            ScoredAction top,
            GameConfig config)
        {
            if (context.Role != AIRole.Dealer)
                return false;

            if (context.DecisionFrame.TrickIndex > 2)
                return false;

            if (intent.PrimaryIntent == DecisionIntentKind.ForceTrump)
                return false;

            if (!IsLowTrumpProbe(top, config))
                return false;

            return true;
        }

        private static bool IsLowTrumpProbe(ScoredAction action, GameConfig config)
        {
            if (action.Cards.Count != 1)
                return false;

            var card = action.Cards[0];
            if (!config.IsTrump(card) || card.IsJoker)
                return false;

            if (RuleAIUtility.CountHighControlCards(config, action.Cards) > 0)
                return false;

            return action.Features.GetValueOrDefault("TrickScoreSwing") <= 0 &&
                action.Features.GetValueOrDefault("TrumpConsumptionCost") == 1 &&
                action.Features.GetValueOrDefault("LeadControlValue") <= 1.6;
        }

        private static int GetSideSuitPressureStrength(
            RuleAIContext context,
            ScoredAction action,
            GameConfig config)
        {
            if (action.Cards.Any(config.IsTrump))
                return 0;

            if (action.Cards.Select(card => card.Suit).Distinct().Count() != 1)
                return 0;

            var pattern = new CardPattern(action.Cards, config);
            var remainingSameSuit = RuleAIUtility.RemoveCards(context.MyHand, action.Cards)
                .Where(card => !config.IsTrump(card) && card.Suit == action.Cards[0].Suit)
                .OrderByDescending(card => RuleAIUtility.GetCardValue(card, config))
                .ToList();
            int totalPoints = action.Cards.Sum(card => card.Score);
            int topValue = action.Cards.Max(card => RuleAIUtility.GetCardValue(card, config));
            int supportTop = remainingSameSuit.Count > 0 ? RuleAIUtility.GetCardValue(remainingSameSuit[0], config) : 0;
            int supportSecond = remainingSameSuit.Count > 1 ? RuleAIUtility.GetCardValue(remainingSameSuit[1], config) : 0;
            bool hasPairSupport = remainingSameSuit.GroupBy(card => card).Any(group => group.Count() >= 2);
            bool hasHighSupport = remainingSameSuit.Any(card => RuleAIUtility.GetCardValue(card, config) >= 112 || card.Score > 0);

            if (pattern.IsTractor(action.Cards))
            {
                return 5000 +
                    action.Cards.Count * 100 +
                    topValue +
                    supportTop +
                    totalPoints * 20;
            }

            if (IsExactPair(action))
            {
                int avgValue = action.Cards.Sum(card => RuleAIUtility.GetCardValue(card, config)) / 2;
                bool strongPair = avgValue >= 113 ||
                    totalPoints >= 10 ||
                    (avgValue >= 111 && (supportTop >= 112 || hasPairSupport || hasHighSupport));
                if (!strongPair)
                    return 0;

                return 4000 +
                    avgValue * 2 +
                    supportTop +
                    supportSecond / 2 +
                    totalPoints * 20;
            }

            if (action.Cards.Count == 1)
            {
                bool strongSingle = (topValue >= 114 &&
                        remainingSameSuit.Count >= 2 &&
                        (hasPairSupport || hasHighSupport || supportTop + supportSecond >= 223)) ||
                    (topValue >= 113 &&
                        hasPairSupport &&
                        supportTop >= 111);
                if (!strongSingle)
                    return 0;

                return 3000 +
                    topValue * 2 +
                    supportTop +
                    supportSecond +
                    totalPoints * 20;
            }

            return 0;
        }

        private static bool IsExactPair(ScoredAction action)
        {
            return action.Cards.Count == 2 &&
                action.Cards[0].Suit == action.Cards[1].Suit &&
                action.Cards[0].Rank == action.Cards[1].Rank;
        }
    }
}
