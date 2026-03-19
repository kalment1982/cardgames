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
            var allTrumpCloseout = SelectAllTrumpCloseoutCandidate(context, scored, config);
            if (allTrumpCloseout != null)
            {
                scored.Remove(allTrumpCloseout);
                scored.Insert(0, allTrumpCloseout);
                return scored;
            }

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

            if (intent.PrimaryIntent == DecisionIntentKind.PrepareThrow)
            {
                var mixedThrow = scored
                    .Where(action =>
                    {
                        if (action.Cards.Count < 3)
                            return false;

                        var pattern = new CardPattern(action.Cards, config);
                        return pattern.Type == PatternType.Mixed;
                    })
                    .OrderByDescending(action => action.Cards.Count)
                    .ThenByDescending(action => action.Score)
                    .FirstOrDefault();

                if (mixedThrow != null)
                {
                    scored.Remove(mixedThrow);
                    scored.Insert(0, mixedThrow);
                }

                return scored;
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

            if (tractor != null)
            {
                scored.Remove(tractor);
                scored.Insert(0, tractor);
                return scored;
            }

            var top = scored[0];
            bool preferSideSuitPressure =
                intent.PrimaryIntent == DecisionIntentKind.AttackLongSuit ||
                ShouldPreferEarlyDealerSideSuitPressure(context, intent, top, config) ||
                ShouldPreferSideSuitPressure(context, top, config);

            if (preferSideSuitPressure)
            {
                int topPressureStrength = GetSideSuitPressureStrength(context, top, config);
                var strongSideLead = scored
                    .Select(action => new
                    {
                        Action = action,
                        PressureStrength = GetSideSuitPressureStrength(context, action, config)
                    })
                    .Where(entry => entry.PressureStrength > 0)
                    .OrderByDescending(entry => entry.PressureStrength)
                    .ThenByDescending(entry => entry.Action.Score)
                    .FirstOrDefault();

                if (strongSideLead != null &&
                    strongSideLead.PressureStrength > topPressureStrength)
                {
                    scored.Remove(strongSideLead.Action);
                    scored.Insert(0, strongSideLead.Action);
                }
            }

            scored = ReorderForWinningPriorityLead(context, scored, config);
            return scored;
        }

        private static List<ScoredAction> ReorderForWinningPriorityLead(
            RuleAIContext context,
            List<ScoredAction> scored,
            GameConfig config)
        {
            if (scored.Count <= 1)
                return scored;

            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                return scored;

            bool hasKnownTopSideSuitCandidate = scored.Any(action => GetWinningPriorityBucket(context, action, config) >= 4);
            if (context.DecisionFrame.TrickIndex > 2 && !hasKnownTopSideSuitCandidate)
                return scored;

            if (!IsLowSidePairPressureLead(scored[0], config) &&
                !IsLowSideSinglePressureLead(scored[0], config))
                return scored;

            int topPriority = GetWinningPriorityBucket(context, scored[0], config);
            var strongestWinLead = scored
                .Select(action => new
                {
                    Action = action,
                    Priority = GetWinningPriorityBucket(context, action, config)
                })
                .Where(entry => entry.Priority > 0)
                .OrderByDescending(entry => entry.Priority)
                .ThenByDescending(entry => entry.Action.Score)
                .FirstOrDefault();

            if (strongestWinLead == null || strongestWinLead.Priority <= topPriority)
                return scored;

            scored.Remove(strongestWinLead.Action);
            scored.Insert(0, strongestWinLead.Action);
            return scored;
        }

        private static bool ShouldPreferSideSuitPressure(
            RuleAIContext context,
            ScoredAction top,
            GameConfig config)
        {
            if (context.DecisionFrame.EndgameLevel != EndgameLevel.None)
                return false;

            if (top.Cards.Count != 1)
                return false;

            var card = top.Cards[0];
            if (!config.IsTrump(card) || card.IsJoker)
                return false;

            return true;
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

        private static ScoredAction? SelectAllTrumpCloseoutCandidate(
            RuleAIContext context,
            List<ScoredAction> scored,
            GameConfig config)
        {
            if (context.DecisionFrame.EndgameLevel == EndgameLevel.None)
                return null;

            if (context.MyHand.Count < 2)
                return null;

            if (context.MyHand.Any(card => !config.IsTrump(card)))
                return null;

            if (!AreAllOtherPlayersLikelyOutOfTrump(context))
                return null;

            return scored
                .Where(action => action.Cards.Count == context.MyHand.Count)
                .OrderByDescending(action => action.Cards.Count)
                .ThenByDescending(action => action.Score)
                .FirstOrDefault();
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

        private static bool AreAllOtherPlayersLikelyOutOfTrump(RuleAIContext context)
        {
            if (context.PlayerIndex < 0)
                return false;

            bool hasEvidence = false;
            foreach (int player in Enumerable.Range(0, 4).Where(index => index != context.PlayerIndex))
            {
                bool voidTrump =
                    context.MemorySnapshot.VoidSuitsByPlayer.TryGetValue(player, out var voids) &&
                    voids.Contains("Trump");
                bool zeroTrumpEstimate =
                    context.InferenceSnapshot.EstimatedTrumpCountByPlayer.TryGetValue(player, out var estimate) &&
                    estimate.Upper <= 0.5;

                if (!voidTrump && !zeroTrumpEstimate)
                    return false;

                hasEvidence = true;
            }

            return hasEvidence;
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

            if (ShouldSuppressSideSuitPressureForImmediateCutThreat(context, action, config))
                return 0;

            var pattern = new CardPattern(action.Cards, config);
            var remainingSameSuit = RuleAIUtility.RemoveCards(context.MyHand, action.Cards)
                .Where(card => !config.IsTrump(card) && card.Suit == action.Cards[0].Suit)
                .OrderByDescending(card => RuleAIUtility.GetCardValue(card, config))
                .ToList();
            int suitLength = action.Cards.Count + remainingSameSuit.Count;
            int totalPoints = action.Cards.Sum(card => card.Score);
            int topValue = action.Cards.Max(card => RuleAIUtility.GetCardValue(card, config));
            int supportTop = remainingSameSuit.Count > 0 ? RuleAIUtility.GetCardValue(remainingSameSuit[0], config) : 0;
            int supportSecond = remainingSameSuit.Count > 1 ? RuleAIUtility.GetCardValue(remainingSameSuit[1], config) : 0;
            bool hasPairSupport = remainingSameSuit.GroupBy(card => (card.Suit, card.Rank)).Any(group => group.Count() >= 2);
            bool hasHighSupport = remainingSameSuit.Any(card => RuleAIUtility.GetCardValue(card, config) >= 112 || card.Score > 0);
            bool knownTop = IsKnownTopSideSuitAction(context, action.Cards, config);

            if (pattern.IsTractor(action.Cards))
            {
                return 9000 +
                    (knownTop ? 800 : 0) +
                    action.Cards.Count * 120 +
                    topValue * 3 +
                    supportTop +
                    totalPoints * 20;
            }

            if (IsExactPair(action))
            {
                int avgValue = action.Cards.Sum(card => RuleAIUtility.GetCardValue(card, config)) / 2;
                bool strongPair = knownTop ||
                    avgValue >= 112 ||
                    totalPoints >= 10 ||
                    supportTop >= 112 ||
                    hasPairSupport ||
                    hasHighSupport ||
                    suitLength >= 4;
                if (!strongPair)
                    return 0;

                int highPairBonus = avgValue >= 113 ? 700 :
                    avgValue >= 112 ? 500 :
                    0;
                return 7000 +
                    (knownTop ? 900 : 0) +
                    highPairBonus +
                    avgValue * 6 +
                    supportTop * 2 +
                    supportSecond +
                    suitLength * 50 +
                    totalPoints * 6;
            }

            if (action.Cards.Count == 1)
            {
                bool strongSingle = knownTop ||
                    topValue >= 114 ||
                    (topValue >= 113 && suitLength >= 2) ||
                    (topValue >= 112 && suitLength >= 2 &&
                        (supportTop >= 110 || hasHighSupport || suitLength >= 3)) ||
                    (topValue >= 111 && suitLength >= 3 &&
                        (hasPairSupport || supportTop >= 112));
                if (!strongSingle)
                    return 0;

                int aceBonus = action.Cards[0].Rank == Rank.Ace ? 600 : 0;
                return 5000 +
                    (knownTop ? 1200 : 0) +
                    aceBonus +
                    topValue * 5 +
                    supportTop * 2 +
                    supportSecond +
                    suitLength * 60 +
                    totalPoints * 15;
            }

            return 0;
        }

        private static bool ShouldSuppressSideSuitPressureForImmediateCutThreat(
            RuleAIContext context,
            ScoredAction action,
            GameConfig config)
        {
            if (context.PlayerIndex < 0)
                return false;

            if (!IsValuableSideSuitLead(context, action, config))
                return false;

            int nextPlayer = GetNextPlayerClockwise(context.PlayerIndex);
            if (IsTeammate(context.PlayerIndex, nextPlayer))
                return false;

            string suitKey = action.Cards[0].Suit.ToString();
            if (!context.MemorySnapshot.VoidSuitsByPlayer.TryGetValue(nextPlayer, out var voidSuits) ||
                !voidSuits.Contains(suitKey))
                return false;

            if (voidSuits.Contains("Trump"))
                return false;

            bool hasTrumpThreat = true;
            if (context.InferenceSnapshot.EstimatedTrumpCountByPlayer.TryGetValue(nextPlayer, out var estimate))
                hasTrumpThreat = estimate.Upper > 0.5 || estimate.Estimate > 0.5;

            if (!hasTrumpThreat &&
                context.InferenceSnapshot.HighTrumpRiskByPlayer.TryGetValue(nextPlayer, out var highTrumpRisk))
            {
                hasTrumpThreat = highTrumpRisk.Level >= RiskLevel.Medium;
            }

            if (!hasTrumpThreat)
                return false;

            if (!context.InferenceSnapshot.LeadCutRiskBySystem.TryGetValue(suitKey, out var cutRisk))
                return true;

            return cutRisk.Level >= RiskLevel.Medium || cutRisk.Confidence < 0.5;
        }

        private static bool IsValuableSideSuitLead(
            RuleAIContext context,
            ScoredAction action,
            GameConfig config)
        {
            int totalPoints = action.Cards.Sum(card => card.Score);
            int topValue = action.Cards.Max(card => RuleAIUtility.GetCardValue(card, config));

            if (totalPoints > 0)
                return true;

            if (topValue >= 112)
                return true;

            if (IsExactPair(action))
                return true;

            var pattern = new CardPattern(action.Cards, config);
            if (pattern.IsTractor(action.Cards))
                return true;

            return IsKnownTopSideSuitAction(context, action.Cards, config);
        }

        private static int GetWinningPriorityBucket(
            RuleAIContext context,
            ScoredAction action,
            GameConfig config)
        {
            if (action.Cards.Count == 0)
                return 0;

            var pattern = new CardPattern(action.Cards, config);
            int topValue = action.Cards.Max(card => RuleAIUtility.GetCardValue(card, config));
            bool allTrump = action.Cards.All(config.IsTrump);
            bool knownTopSideSuit = IsKnownTopSideSuitAction(context, action.Cards, config);

            if (knownTopSideSuit)
                return 4;

            if (action.Cards.Any(config.IsTrump))
                return 0;

            if (action.Cards.Select(card => card.Suit).Distinct().Count() != 1)
                return 0;

            var remainingSameSuit = RuleAIUtility.RemoveCards(context.MyHand, action.Cards)
                .Where(card => !config.IsTrump(card) && card.Suit == action.Cards[0].Suit)
                .OrderByDescending(card => RuleAIUtility.GetCardValue(card, config))
                .ToList();
            int supportTop = remainingSameSuit.Count > 0 ? RuleAIUtility.GetCardValue(remainingSameSuit[0], config) : 0;
            bool hasHighSupport = remainingSameSuit.Any(card => RuleAIUtility.GetCardValue(card, config) >= 113);
            int suitLength = action.Cards.Count + remainingSameSuit.Count;

            if (action.Cards.Count == 1)
            {
                if (action.Cards[0].Rank == Rank.Ace)
                    return 3;

                if (topValue >= 113 && (supportTop >= 112 || suitLength >= 3))
                    return 2;

                return 0;
            }

            if (IsExactPair(action))
            {
                int pairValue = action.Cards[0].Rank switch
                {
                    Rank.Ace => 3,
                    Rank.King when supportTop >= 114 || hasHighSupport => 2,
                    Rank.Queen when supportTop >= 114 && suitLength >= 4 => 2,
                    _ => 0
                };
                return pairValue;
            }

            if (pattern.IsTractor(action.Cards))
            {
                if (topValue >= 113)
                    return 3;

                if (topValue >= 112 && hasHighSupport)
                    return 2;
            }

            return 0;
        }

        private static bool IsLowSidePairPressureLead(ScoredAction action, GameConfig config)
        {
            if (!IsExactPair(action))
                return false;

            if (action.Cards.Any(config.IsTrump))
                return false;

            return action.Cards[0].Rank <= Rank.Ten;
        }

        private static bool IsLowSideSinglePressureLead(ScoredAction action, GameConfig config)
        {
            if (action.Cards.Count != 1)
                return false;

            var card = action.Cards[0];
            if (config.IsTrump(card))
                return false;

            if (RuleAIUtility.GetCardValue(card, config) > 110 && card.Score == 0)
                return false;

            return true;
        }

        private static bool IsKnownTopSideSuitAction(
            RuleAIContext context,
            List<Card> cards,
            GameConfig config)
        {
            if (cards.Count == 0 || cards.Any(config.IsTrump))
                return false;

            if (cards.Select(card => card.Suit).Distinct().Count() != 1)
                return false;

            var reference = cards
                .OrderByDescending(card => RuleAIUtility.GetCardValue(card, config))
                .First();

            foreach (var higher in EnumerateHigherSideSuitCards(reference, config))
            {
                if (CountKnownCopies(context, higher) < 2)
                    return false;
            }

            return true;
        }

        private static IEnumerable<Card> EnumerateHigherSideSuitCards(Card reference, GameConfig config)
        {
            var orderedRanks = new[]
            {
                Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine,
                Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two
            };

            int referenceValue = RuleAIUtility.GetCardValue(reference, config);
            foreach (var rank in orderedRanks)
            {
                var candidate = new Card(reference.Suit, rank);
                if (config.IsTrump(candidate))
                    continue;

                if (RuleAIUtility.GetCardValue(candidate, config) > referenceValue)
                    yield return candidate;
            }
        }

        private static int CountKnownCopies(RuleAIContext context, Card card)
        {
            int playedCount = context.MemorySnapshot.PlayedCountByCard.TryGetValue(card.ToString(), out var played)
                ? played
                : 0;
            int handCount = context.MyHand.Count(existing => existing.Equals(card));
            int bottomCount = context.VisibleBottomCards.Count(existing => existing.Equals(card));
            return playedCount + handCount + bottomCount;
        }

        private static bool IsExactPair(ScoredAction action)
        {
            return action.Cards.Count == 2 &&
                action.Cards[0].Suit == action.Cards[1].Suit &&
                action.Cards[0].Rank == action.Cards[1].Rank;
        }

        private static int GetNextPlayerClockwise(int playerIndex)
        {
            return playerIndex switch
            {
                0 => 3,
                3 => 2,
                2 => 1,
                1 => 0,
                _ => ((playerIndex % 4) + 4) % 4
            };
        }

        private static bool IsTeammate(int myPlayerIndex, int otherPlayerIndex)
        {
            if (myPlayerIndex < 0 || otherPlayerIndex < 0)
                return false;

            return ((otherPlayerIndex - myPlayerIndex + 4) % 4) == 2;
        }
    }
}
