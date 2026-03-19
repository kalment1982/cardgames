using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30.Follow;
using TractorGame.Core.AI.V30.Lead;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V30
{
    public sealed class LeadOverlayBundleV30
    {
        public string Phase { get; init; } = "Lead";
        public string Mode { get; init; } = "v30_overlay_policy";
        public string PrimaryIntent { get; init; } = LeadDecisionIntentV30.ProbeWeakSuit.ToString();
        public string SecondaryIntent { get; init; } = DecisionIntentKind.Unknown.ToString();
        public string SelectedCandidateId { get; init; } = "lead004.low_value_probe";
        public string SelectedReason { get; init; } = "lead004.low_value_probe";
        public List<string> TriggeredRules { get; init; } = new();
        public List<string> Selected { get; init; } = new();
        public int CandidateCount { get; init; }
        public List<LeadOverlaySemanticCandidateV30> SemanticCandidates { get; init; } = new();
    }

    public sealed class LeadOverlaySemanticCandidateV30
    {
        public string CandidateId { get; init; } = string.Empty;
        public string Intent { get; init; } = LeadDecisionIntentV30.ProbeWeakSuit.ToString();
        public int PriorityTier { get; init; }
        public int ExpectedScore { get; init; }
        public int FutureValue { get; init; }
        public bool IsSelected { get; init; }
    }

    public sealed class LeadOverlayDecisionV30
    {
        public List<Card> SelectedCards { get; init; } = new();
        public List<List<Card>> OrderedCandidates { get; init; } = new();
        public string PrimaryIntent { get; init; } = DecisionIntentKind.Unknown.ToString();
        public string SecondaryIntent { get; init; } = DecisionIntentKind.Unknown.ToString();
        public string SelectedCandidateId { get; init; } = "lead004.low_value_probe";
        public string SelectedReason { get; init; } = string.Empty;
        public List<string> TriggeredRules { get; init; } = new();
        public List<double> CandidateScores { get; init; } = new();
        public List<string?> CandidateReasonCodes { get; init; } = new();
        public List<Dictionary<string, double>> CandidateFeatures { get; init; } = new();
        public LeadOverlayBundleV30? Bundle { get; init; }
    }

    /// <summary>
    /// V30 规则 AI 最小引擎封装。
    /// Lead 保持 overlay 透传；Follow 提供 V30 重排/筛选入口。
    /// </summary>
    public sealed class RuleAIEngineV30
    {
        private readonly LeadPolicyV30 _leadPolicy;
        private readonly FollowPolicyV30 _followPolicy;
        private readonly GameConfig _config;

        public RuleAIEngineV30(
            GameConfig config,
            AIDifficulty difficulty,
            CardMemory? memory,
            LeadPolicyV30? leadPolicy = null,
            FollowPolicyV30? followPolicy = null)
        {
            _config = config;
            _leadPolicy = leadPolicy ?? new LeadPolicyV30();
            _followPolicy = followPolicy ?? new FollowPolicyV30();
        }

        public LeadOverlayDecisionV30 DecideLead(
            RuleAIContext context,
            PhaseDecision v21Decision,
            AIDecisionLogContext? logContext = null)
        {
            var scoredActions = v21Decision.ScoredActions ?? new List<ScoredAction>();
            var entries = BuildLeadEntries(scoredActions, v21Decision);
            if (entries.Count == 0 && v21Decision.SelectedCards.Count > 0)
            {
                entries.Add(new LeadActionEntry
                {
                    Cards = new List<Card>(v21Decision.SelectedCards),
                    Score = 0,
                    ReasonCode = v21Decision.SelectedReason,
                    Features = new Dictionary<string, double>()
                });
            }

            var leadContext = InferLeadContext(context, v21Decision, entries);
            var leadDecision = _leadPolicy.Decide(leadContext);
            var selection = ResolveConcreteLeadSelection(context, v21Decision, entries, leadDecision.Candidates);
            var selectedEntry = selection.Entry;
            var orderedEntries = OrderLeadEntries(entries, selectedEntry);

            var selectedCards = selectedEntry != null
                ? new List<Card>(selectedEntry.Cards)
                : new List<Card>(v21Decision.SelectedCards);
            string primaryIntent = selection.PrimaryIntent;
            string secondaryIntent = v21Decision.Intent?.SecondaryIntent.ToString() ?? DecisionIntentKind.Unknown.ToString();
            string selectedCandidateId = selection.SelectedCandidateId;
            string selectedReason = selection.SelectedReason;
            var triggeredRules = selection.TriggeredRules.ToList();

            return new LeadOverlayDecisionV30
            {
                SelectedCards = selectedCards,
                OrderedCandidates = orderedEntries.Select(entry => new List<Card>(entry.Cards)).ToList(),
                PrimaryIntent = primaryIntent,
                SecondaryIntent = secondaryIntent,
                SelectedCandidateId = selectedCandidateId,
                SelectedReason = selectedReason,
                TriggeredRules = triggeredRules,
                CandidateScores = orderedEntries.Select(entry => entry.Score).ToList(),
                CandidateReasonCodes = orderedEntries
                    .Select(entry => string.Equals(entry.Key, selectedEntry?.Key, StringComparison.Ordinal)
                        ? selectedCandidateId
                        : entry.ReasonCode)
                    .ToList(),
                CandidateFeatures = orderedEntries
                    .Select(entry => new Dictionary<string, double>(entry.Features))
                    .ToList(),
                Bundle = new LeadOverlayBundleV30
                {
                    Phase = "Lead",
                    Mode = "v30_overlay_policy",
                    PrimaryIntent = primaryIntent,
                    SecondaryIntent = secondaryIntent,
                    SelectedCandidateId = selectedCandidateId,
                    SelectedReason = selectedReason,
                    TriggeredRules = triggeredRules,
                    Selected = selectedCards.Select(card => card.ToString()).ToList(),
                    CandidateCount = orderedEntries.Count,
                    SemanticCandidates = leadDecision.Candidates
                        .Select(candidate => new LeadOverlaySemanticCandidateV30
                        {
                            CandidateId = candidate.CandidateId,
                            Intent = candidate.Intent.ToString(),
                            PriorityTier = candidate.PriorityTier,
                            ExpectedScore = candidate.ExpectedScore,
                            FutureValue = candidate.FutureValue,
                            IsSelected = selection.SemanticCandidate != null &&
                                string.Equals(candidate.CandidateId, selection.SemanticCandidate.CandidateId, StringComparison.Ordinal)
                        })
                        .ToList()
                }
            };
        }

        public FollowDecisionV30 DecideFollow(
            RuleAIContext context,
            IReadOnlyList<List<Card>>? legalCandidates = null)
        {
            return _followPolicy.Decide(context, legalCandidates);
        }

        // Optional helper for standalone Lead policy tests without AIPlayer wiring.
        public LeadDecisionV30 DecideLeadStandalone(LeadContextV30 context)
        {
            return _leadPolicy.Decide(context);
        }

        private LeadContextV30 InferLeadContext(
            RuleAIContext context,
            PhaseDecision v21Decision,
            IReadOnlyList<LeadActionEntry> entries)
        {
            var safeThrow = SelectBestSafeThrowEntry(entries);
            var stableSideSuit = SelectBestStableSideSuitEntry(context, entries);
            var scoreSideLead = SelectBestScoreSideLeadEntry(context, entries);
            var forceTrump = SelectBestForceTrumpEntry(context, v21Decision, entries);
            var handoff = SelectHandoffEntry(context, entries);
            var threePairPlan = SelectThreePairPlanEntry(context, entries);
            var voidBuild = SelectVoidBuildEntry(context, entries);

            bool mateEvidence = HasMateTakeoverEvidence(context);
            var teamSideSuit = mateEvidence
                ? SelectBestTeamSideSuitEntry(context, entries)
                : null;
            bool protectBottomMode =
                context.DecisionFrame.BottomRiskPressure >= RiskLevel.High ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.High;

            int trumpCountAfterForceTrump = forceTrump == null
                ? context.HandProfile.TrumpCount
                : Math.Max(0, context.HandProfile.TrumpCount - forceTrump.Cards.Count(context.GameConfig.IsTrump));

            return new LeadContextV30
            {
                Role = ConvertRole(context.Role),
                TrickIndex = context.DecisionFrame.TrickIndex,

                HasSafeThrowPlan = safeThrow != null,
                SafeThrowExpectedScore = safeThrow?.Cards.Sum(card => card.Score) ?? 0,
                SafeThrowFutureValue = ScoreToFutureValue(safeThrow?.Score ?? 0),

                HasStableSideSuitRun = stableSideSuit != null,
                StableSideSuitFutureValue = ScoreToFutureValue(stableSideSuit?.Score ?? 0),
                HasLostSuitControl = stableSideSuit != null && IsImmediateCutRiskHigh(context, stableSideSuit.Cards),

                HasStrongScoreSideLead = scoreSideLead != null,
                StrongScoreSideLeadFutureValue = ScoreToFutureValue(scoreSideLead?.Score ?? 0),

                HasTeamSideSuitRun = teamSideSuit != null,
                TeamSideSuitFutureValue = ScoreToFutureValue(teamSideSuit?.Score ?? 0),
                KeyOpponentLikelyNotVoid = teamSideSuit != null && !IsImmediateCutRiskHigh(context, teamSideSuit.Cards),

                HasProfitableForceTrump = forceTrump != null,
                ForceTrumpFutureValue = ScoreToFutureValue(forceTrump?.Score ?? 0),

                HasClearOwnFollowUpLine = safeThrow != null || stableSideSuit != null || scoreSideLead != null || forceTrump != null || threePairPlan != null || voidBuild != null,
                MateHasPositiveTakeoverEvidence = handoff != null,

                HasFutureThrowPlan = threePairPlan != null,
                ThreePairControlLevel = ResolveThreePairControlLevel(context, threePairPlan),

                HasForceTrumpForThrowPlan = threePairPlan != null && forceTrump != null,
                FutureThrowExpectedScore = threePairPlan?.Cards.Sum(card => card.Score) ?? 0,
                TrumpCountAfterForceTrump = trumpCountAfterForceTrump,
                KeepsControlTrumpAfterForceTrump = forceTrump != null && KeepsControlTrumpAfterForceTrump(context, forceTrump.Cards),
                KeepsTrumpPairAfterForceTrump = forceTrump != null && KeepsTrumpPairAfterForceTrump(context, forceTrump.Cards),
                IsProtectBottomMode = protectBottomMode,

                HasVoidBuildPlan = voidBuild != null,
                VoidBreaksOnlyWeakNonScorePairs = voidBuild != null && IsWeakNonScoreVoidBreak(context, voidBuild.Cards),
                HasExplicitVoidFollowUpBenefit = voidBuild != null && HasExplicitVoidFollowUpBenefit(context),
                VoidPlanFutureValue = ScoreToFutureValue(voidBuild?.Score ?? 0),

                HasProbePlan = true,
                ProbeFutureValue = ScoreToFutureValue(entries.FirstOrDefault()?.Score ?? 0)
            };
        }

        private LeadSelectionResult ResolveConcreteLeadSelection(
            RuleAIContext context,
            PhaseDecision v21Decision,
            IReadOnlyList<LeadActionEntry> entries,
            IReadOnlyList<LeadCandidateV30> semanticCandidates)
        {
            var orderedCandidates = OrderSemanticCandidates(semanticCandidates);
            var bestOverallEntry = SelectBestOverallConcreteEntry(entries);
            LeadCandidateV30? firstRejectedCandidate = null;

            foreach (var candidate in orderedCandidates)
            {
                var candidateEntry = SelectConcreteLeadEntry(context, v21Decision, entries, candidate);
                if (candidateEntry == null)
                    continue;

                if (PassesConcreteLeadGuard(candidate, candidateEntry, bestOverallEntry))
                {
                    return LeadSelectionResult.FromSemantic(candidate, candidateEntry);
                }

                firstRejectedCandidate ??= candidate;
            }

            if (bestOverallEntry != null)
            {
                return LeadSelectionResult.CreateGuardFallback(bestOverallEntry, firstRejectedCandidate);
            }

            var fallbackCandidate = orderedCandidates.FirstOrDefault();
            var fallbackEntry = fallbackCandidate != null
                ? SelectConcreteLeadEntry(context, v21Decision, entries, fallbackCandidate)
                : null;
            if (fallbackCandidate != null && fallbackEntry != null)
            {
                return LeadSelectionResult.FromSemantic(fallbackCandidate, fallbackEntry);
            }

            if (v21Decision.SelectedCards.Count > 0)
            {
                var v21Entry = entries.FirstOrDefault(entry => string.Equals(
                    entry.Key,
                    BuildCandidateKey(v21Decision.SelectedCards),
                    StringComparison.Ordinal));
                if (v21Entry != null)
                {
                    return LeadSelectionResult.CreateGuardFallback(v21Entry, null);
                }
            }

            return LeadSelectionResult.CreateGuardFallback(entries.FirstOrDefault(), null);
        }

        private LeadActionEntry? SelectConcreteLeadEntry(
            RuleAIContext context,
            PhaseDecision v21Decision,
            IReadOnlyList<LeadActionEntry> entries,
            LeadCandidateV30 selectedCandidate)
        {
            LeadActionEntry? selected = selectedCandidate.CandidateId switch
            {
                "lead005.safe_throw.high" => SelectBestSafeThrowEntry(entries, minimumScorePoints: 10),
                "lead005.safe_throw.low" => SelectBestSafeThrowEntry(entries, maximumScorePoints: 9),
                "lead001.dealer_stable_side" => SelectBestStableSideSuitEntry(context, entries),
                "lead002.score_side_cash" => SelectBestScoreSideLeadEntry(context, entries),
                "lead006.team_side_suit" => SelectBestTeamSideSuitEntry(context, entries),
                "lead003.force_trump" => SelectBestForceTrumpEntry(context, v21Decision, entries),
                "lead007.handoff_to_mate" => SelectHandoffEntry(context, entries),
                "lead008.three_pair.high_control" => SelectThreePairPlanEntry(context, entries, requireHighControl: true),
                "lead008.three_pair.low_pair_consume" => SelectThreePairPlanEntry(context, entries, requireHighControl: false),
                "lead008.force_trump_for_throw" => SelectCheapestTrumpEntry(context, entries),
                "lead009.build_void" => SelectVoidBuildEntry(context, entries),
                _ => SelectProbeEntry(context, entries)
            };

            if (selected != null)
                return selected;

            if (v21Decision.SelectedCards.Count > 0)
            {
                string selectedKey = BuildCandidateKey(v21Decision.SelectedCards);
                selected = entries.FirstOrDefault(entry => string.Equals(entry.Key, selectedKey, StringComparison.Ordinal));
            }

            return selected ?? entries.FirstOrDefault();
        }

        private static IReadOnlyList<LeadCandidateV30> OrderSemanticCandidates(IReadOnlyList<LeadCandidateV30> candidates)
        {
            return candidates
                .OrderBy(candidate => candidate.PriorityTier)
                .ThenByDescending(candidate => candidate.FutureValue)
                .ThenByDescending(candidate => candidate.ExpectedScore)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToList();
        }

        private static LeadActionEntry? SelectBestOverallConcreteEntry(IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .OrderByDescending(entry => entry.Score)
                .ThenBy(entry => entry.Features.GetValueOrDefault("StructureBreakCost"))
                .ThenBy(entry => entry.Features.GetValueOrDefault("HighControlLossCost"))
                .FirstOrDefault();
        }

        private static bool PassesConcreteLeadGuard(
            LeadCandidateV30 candidate,
            LeadActionEntry candidateEntry,
            LeadActionEntry? bestOverallEntry)
        {
            if (bestOverallEntry == null)
                return true;

            if (string.Equals(candidateEntry.Key, bestOverallEntry.Key, StringComparison.Ordinal))
                return true;

            if (bestOverallEntry.Score >= 0 && candidateEntry.Score < 0)
                return false;

            double scoreGap = bestOverallEntry.Score - candidateEntry.Score;
            if (scoreGap <= 0)
                return true;

            return scoreGap <= GetConcreteScoreGapAllowance(candidate.CandidateId);
        }

        private static double GetConcreteScoreGapAllowance(string candidateId)
        {
            return candidateId switch
            {
                "lead005.safe_throw.high" => 6.0,
                "lead005.safe_throw.low" => 1.5,
                "lead001.dealer_stable_side" => 2.0,
                "lead002.score_side_cash" => 1.0,
                "lead006.team_side_suit" => 2.0,
                "lead003.force_trump" => 1.5,
                "lead007.handoff_to_mate" => 2.0,
                "lead008.three_pair.high_control" => 2.0,
                "lead008.three_pair.low_pair_consume" => 2.0,
                "lead008.force_trump_for_throw" => 2.0,
                "lead009.build_void" => 1.0,
                _ => 2.0
            };
        }

        private static List<LeadActionEntry> BuildLeadEntries(
            IReadOnlyList<ScoredAction> scoredActions,
            PhaseDecision v21Decision)
        {
            var entries = scoredActions
                .Select(action => new LeadActionEntry
                {
                    Cards = new List<Card>(action.Cards),
                    Score = action.Score,
                    ReasonCode = string.IsNullOrWhiteSpace(action.ReasonCode) ? null : action.ReasonCode,
                    Features = new Dictionary<string, double>(action.Features)
                })
                .ToList();

            if (entries.Count == 0 && v21Decision.SelectedCards.Count > 0)
            {
                entries.Add(new LeadActionEntry
                {
                    Cards = new List<Card>(v21Decision.SelectedCards),
                    Score = 0,
                    ReasonCode = v21Decision.SelectedReason,
                    Features = new Dictionary<string, double>()
                });
            }

            return entries;
        }

        private static List<LeadActionEntry> OrderLeadEntries(
            IReadOnlyList<LeadActionEntry> entries,
            LeadActionEntry? selectedEntry)
        {
            var ordered = entries
                .Select(entry => entry.Clone())
                .ToList();

            if (selectedEntry == null)
                return ordered;

            var existing = ordered.FirstOrDefault(entry => string.Equals(entry.Key, selectedEntry.Key, StringComparison.Ordinal));
            if (existing == null)
            {
                ordered.Insert(0, selectedEntry.Clone());
                return ordered;
            }

            ordered.Remove(existing);
            ordered.Insert(0, existing);
            return ordered;
        }

        private static LeadRoleV30 ConvertRole(AIRole role)
        {
            return role switch
            {
                AIRole.Dealer => LeadRoleV30.Dealer,
                AIRole.DealerPartner => LeadRoleV30.DealerPartner,
                _ => LeadRoleV30.Opponent
            };
        }

        private LeadActionEntry? SelectBestSafeThrowEntry(
            IReadOnlyList<LeadActionEntry> entries,
            int? minimumScorePoints = null,
            int? maximumScorePoints = null)
        {
            return entries
                .Where(entry => IsSafeThrowEntry(entry.Cards))
                .Where(entry => !minimumScorePoints.HasValue || entry.Cards.Sum(card => card.Score) >= minimumScorePoints.Value)
                .Where(entry => !maximumScorePoints.HasValue || entry.Cards.Sum(card => card.Score) <= maximumScorePoints.Value)
                .OrderByDescending(entry => entry.Cards.Sum(card => card.Score))
                .ThenByDescending(entry => entry.Score)
                .FirstOrDefault();
        }

        private bool IsSafeThrowEntry(IReadOnlyList<Card> cards)
        {
            if (cards.Count < 3)
                return false;

            var pattern = new CardPattern(cards.ToList(), _config);
            return pattern.Type == PatternType.Mixed;
        }

        private LeadActionEntry? SelectBestStableSideSuitEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .Where(entry => IsStableSideSuitEntry(context, entry.Cards))
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => GetStableSideSuitStrength(context, entry.Cards))
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectBestScoreSideLeadEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .Where(entry => IsScoreSideLeadEntry(context, entry.Cards))
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => GetScoreSideLeadStrength(context, entry.Cards))
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectBestTeamSideSuitEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .Where(entry => IsTeamSideSuitEntry(context, entry.Cards))
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => GetStableSideSuitStrength(context, entry.Cards))
                .FirstOrDefault();
        }

        private bool IsStableSideSuitEntry(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (cards.Count == 0 || cards.Any(_config.IsTrump))
                return false;

            if (cards.Select(card => card.Suit).Distinct().Count() != 1)
                return false;

            int strength = GetStableSideSuitStrength(context, cards);
            return strength > 0;
        }

        private bool IsScoreSideLeadEntry(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (!IsStableSideSuitEntry(context, cards))
                return false;

            int totalPoints = cards.Sum(card => card.Score);
            if (totalPoints < 10)
                return false;

            if (IsImmediateCutRiskHigh(context, cards))
                return false;

            int topValue = cards.Max(card => RuleAIUtility.GetCardValue(card, _config));
            var pattern = new CardPattern(cards.ToList(), _config);

            if (pattern.IsTractor(cards.ToList()))
                return true;

            if (IsExactPair(cards))
                return topValue >= 112;

            if (cards.Count != 1 || topValue < 114)
                return false;

            var remainingSameSuit = RuleAIUtility.RemoveCards(context.MyHand, cards.ToList())
                .Where(card => !_config.IsTrump(card) && card.Suit == cards[0].Suit)
                .ToList();

            return remainingSameSuit.Any(card => RuleAIUtility.GetCardValue(card, _config) >= 113) ||
                remainingSameSuit.Count >= 2;
        }

        private int GetScoreSideLeadStrength(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (!IsScoreSideLeadEntry(context, cards))
                return 0;

            int suitLength = context.MyHand.Count(card => !_config.IsTrump(card) && card.Suit == cards[0].Suit);
            int totalPoints = cards.Sum(card => card.Score);
            return 4000 + totalPoints * 120 + suitLength * 40 + cards.Count * 10;
        }

        private bool IsTeamSideSuitEntry(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (!IsStableSideSuitEntry(context, cards))
                return false;

            int topValue = cards.Max(card => RuleAIUtility.GetCardValue(card, _config));
            var pattern = new CardPattern(cards.ToList(), _config);

            if (pattern.IsTractor(cards.ToList()))
                return topValue >= 112;

            if (IsExactPair(cards))
                return topValue >= 113;

            return cards.Count == 1 && topValue >= 114;
        }

        private int GetStableSideSuitStrength(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (cards.Count == 0 || cards.Any(_config.IsTrump))
                return 0;

            if (cards.Select(card => card.Suit).Distinct().Count() != 1)
                return 0;

            var suit = cards[0].Suit;
            int suitLength = context.MyHand.Count(card => !_config.IsTrump(card) && card.Suit == suit);
            int topValue = cards.Max(card => RuleAIUtility.GetCardValue(card, _config));
            int totalPoints = cards.Sum(card => card.Score);
            var pattern = new CardPattern(cards.ToList(), _config);

            if (pattern.IsTractor(cards.ToList()))
                return 9000 + cards.Count * 120 + topValue * 3 + suitLength * 50 + totalPoints * 20;

            if (IsExactPair(cards))
            {
                if (topValue < 112 && totalPoints < 10 && suitLength < 4)
                    return 0;

                return 7000 + topValue * 6 + suitLength * 50 + totalPoints * 10;
            }

            if (cards.Count == 1)
            {
                bool strongSingle =
                    topValue >= 114 ||
                    (topValue >= 113 && suitLength >= 2) ||
                    (topValue >= 112 && suitLength >= 3);
                if (!strongSingle)
                    return 0;

                return 5000 + topValue * 5 + suitLength * 60 + totalPoints * 15;
            }

            return 0;
        }

        private LeadActionEntry? SelectBestForceTrumpEntry(
            RuleAIContext context,
            PhaseDecision v21Decision,
            IReadOnlyList<LeadActionEntry> entries)
        {
            var trumpEntries = entries
                .Where(entry => entry.Cards.Count > 0 && entry.Cards.All(_config.IsTrump))
                .ToList();
            if (trumpEntries.Count == 0)
                return null;

            bool v21ExplicitForceTrump = v21Decision.Intent?.PrimaryIntent == DecisionIntentKind.ForceTrump;
            bool trumpHeavy = context.HandProfile.TrumpCount >= Math.Max(5, Math.Max(1, context.HandCount / 2));
            if (!v21ExplicitForceTrump && !trumpHeavy)
                return null;

            if (SelectBestScoreSideLeadEntry(context, entries) != null)
                return null;

            bool protectBottomOrEndgame =
                context.DecisionFrame.BottomRiskPressure >= RiskLevel.Medium ||
                context.DecisionFrame.DealerRetentionRisk >= RiskLevel.Medium ||
                context.CardsLeftMin <= 6;

            trumpEntries = trumpEntries
                .Where(entry => protectBottomOrEndgame || entry.Cards.Sum(card => card.Score) == 0)
                .ToList();
            if (trumpEntries.Count == 0)
                return null;

            return trumpEntries
                .OrderBy(entry => entry.Features.GetValueOrDefault("HighControlLossCost") + entry.Features.GetValueOrDefault("TrumpConsumptionCost"))
                .ThenBy(entry => entry.Cards.Sum(card => card.Score))
                .ThenBy(entry => entry.Cards.Max(card => RuleAIUtility.GetCardValue(card, _config)))
                .ThenByDescending(entry => entry.Score)
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectHandoffEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            int mateIndex = GetMateIndex(context.PlayerIndex);
            if (mateIndex >= 0 &&
                context.MemorySnapshot.VoidSuitsByPlayer.TryGetValue(mateIndex, out var mateVoids))
            {
                var suitVoidCandidate = entries
                    .Where(entry => entry.Cards.Count == 1)
                    .Where(entry => !context.GameConfig.IsTrump(entry.Cards[0]))
                    .Where(entry => mateVoids.Contains(entry.Cards[0].Suit.ToString()))
                    .Where(entry => entry.Cards[0].Score == 0)
                    .OrderBy(entry => RuleAIUtility.GetCardValue(entry.Cards[0], _config))
                    .ThenBy(entry => entry.Score)
                    .FirstOrDefault();
                if (suitVoidCandidate != null)
                    return suitVoidCandidate;
            }

            if (!HasStrongMateTrumpTakeoverEvidence(context))
                return null;

            return entries
                .Where(entry => entry.Cards.Count == 1)
                .Where(entry => context.GameConfig.IsTrump(entry.Cards[0]))
                .Where(entry => entry.Cards[0].Score == 0)
                .Where(entry => RuleAIUtility.CountHighControlCards(_config, entry.Cards) == 0)
                .OrderBy(entry => RuleAIUtility.GetCardValue(entry.Cards[0], _config))
                .ThenBy(entry => entry.Score)
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectThreePairPlanEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries,
            bool? requireHighControl = null)
        {
            var pairEntries = entries
                .Where(entry => entry.Cards.Count >= 2)
                .Where(entry => !entry.Cards.Any(_config.IsTrump))
                .Where(entry => entry.Cards.Select(card => card.Suit).Distinct().Count() == 1)
                .Where(entry =>
                {
                    var suit = entry.Cards[0].Suit;
                    return CountPairUnitsInSuit(context.MyHand, suit) >= 3;
                })
                .ToList();

            if (pairEntries.Count == 0)
                return null;

            if (requireHighControl == true)
            {
                return pairEntries
                    .Where(entry => IsHighControlPairEntry(entry.Cards))
                    .OrderByDescending(entry => entry.Score)
                    .ThenByDescending(entry => entry.Cards.Max(card => RuleAIUtility.GetCardValue(card, _config)))
                    .FirstOrDefault();
            }

            if (requireHighControl == false)
            {
                return pairEntries
                    .Where(entry => !IsHighControlPairEntry(entry.Cards))
                    .OrderBy(entry => entry.Cards.Sum(card => card.Score))
                    .ThenBy(entry => entry.Cards.Max(card => RuleAIUtility.GetCardValue(card, _config)))
                    .ThenByDescending(entry => entry.Score)
                    .FirstOrDefault()
                    ?? pairEntries
                        .OrderBy(entry => entry.Cards.Sum(card => card.Score))
                        .ThenBy(entry => entry.Cards.Max(card => RuleAIUtility.GetCardValue(card, _config)))
                        .ThenByDescending(entry => entry.Score)
                        .FirstOrDefault();
            }

            return pairEntries
                .OrderByDescending(entry => entry.Score)
                .ThenByDescending(entry => entry.Cards.Count)
                .FirstOrDefault();
        }

        private PairControlLevelV30 ResolveThreePairControlLevel(
            RuleAIContext context,
            LeadActionEntry? threePairPlan)
        {
            if (threePairPlan == null)
                return PairControlLevelV30.None;

            return IsHighControlPairEntry(threePairPlan.Cards)
                ? PairControlLevelV30.HighPairControl
                : PairControlLevelV30.LowPairConsume;
        }

        private bool IsHighControlPairEntry(IReadOnlyList<Card> cards)
        {
            if (!IsExactPair(cards) && cards.Count < 4)
                return false;

            int topValue = cards.Max(card => RuleAIUtility.GetCardValue(card, _config));
            return topValue >= 113;
        }

        private LeadActionEntry? SelectCheapestTrumpEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .Where(entry => entry.Cards.Count > 0 && entry.Cards.All(_config.IsTrump))
                .OrderBy(entry => entry.Features.GetValueOrDefault("HighControlLossCost") + entry.Features.GetValueOrDefault("TrumpConsumptionCost"))
                .ThenBy(entry => entry.Cards.Sum(card => card.Score))
                .ThenBy(entry => entry.Cards.Max(card => RuleAIUtility.GetCardValue(card, _config)))
                .ThenByDescending(entry => entry.Score)
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectVoidBuildEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            if (context.HandProfile.PotentialVoidTargets.Count == 0)
                return null;

            var targetSuits = context.HandProfile.PotentialVoidTargets
                .Where(suit => context.MyHand.Count(card => !_config.IsTrump(card) && card.Suit == suit) <= 2)
                .ToHashSet();
            if (targetSuits.Count == 0)
                return null;

            return entries
                .Where(entry => entry.Cards.Count == 1)
                .Where(entry => !context.GameConfig.IsTrump(entry.Cards[0]))
                .Where(entry => targetSuits.Contains(entry.Cards[0].Suit))
                .Where(entry => IsWeakNonScoreVoidBreak(context, entry.Cards))
                .OrderBy(entry => entry.Cards[0].Score)
                .ThenBy(entry => RuleAIUtility.GetCardValue(entry.Cards[0], _config))
                .ThenBy(entry => entry.Features.GetValueOrDefault("StructureBreakCost"))
                .FirstOrDefault();
        }

        private LeadActionEntry? SelectProbeEntry(
            RuleAIContext context,
            IReadOnlyList<LeadActionEntry> entries)
        {
            return entries
                .OrderBy(entry => entry.Features.GetValueOrDefault("StructureBreakCost"))
                .ThenBy(entry => entry.Features.GetValueOrDefault("HighControlLossCost"))
                .ThenBy(entry => entry.Cards.Sum(card => card.Score))
                .ThenBy(entry => entry.Cards.Count == 1 ? RuleAIUtility.GetCardValue(entry.Cards[0], _config) : int.MaxValue)
                .ThenByDescending(entry => entry.Score)
                .FirstOrDefault();
        }

        private bool HasMateTakeoverEvidence(RuleAIContext context)
        {
            if (context.InferenceSnapshot.MateHoldConfidence.Probability >= 0.72)
                return true;

            int mateIndex = GetMateIndex(context.PlayerIndex);
            if (mateIndex < 0)
                return false;

            bool mateVoidEvidence =
                context.MemorySnapshot.VoidSuitsByPlayer.TryGetValue(mateIndex, out var mateVoids) &&
                mateVoids.Count > 0;
            if (mateVoidEvidence)
                return true;

            if (context.InferenceSnapshot.EstimatedTrumpCountByPlayer.TryGetValue(mateIndex, out var estimate))
                return estimate.Lower >= 2.0 || estimate.Estimate >= 3.0;

            return false;
        }

        private bool HasStrongMateTrumpTakeoverEvidence(RuleAIContext context)
        {
            if (context.InferenceSnapshot.MateHoldConfidence.Probability >= 0.72)
                return true;

            int mateIndex = GetMateIndex(context.PlayerIndex);
            if (mateIndex < 0)
                return false;

            if (!context.InferenceSnapshot.EstimatedTrumpCountByPlayer.TryGetValue(mateIndex, out var estimate))
                return false;

            return estimate.Lower >= 2.0 || estimate.Estimate >= 3.0;
        }

        private bool KeepsControlTrumpAfterForceTrump(RuleAIContext context, IReadOnlyList<Card> selectedCards)
        {
            var remainingTrump = RuleAIUtility.RemoveCards(context.MyHand, selectedCards.ToList())
                .Where(_config.IsTrump)
                .ToList();

            return remainingTrump.Any(card => RuleAIUtility.GetCardValue(card, _config) >= 700);
        }

        private bool KeepsTrumpPairAfterForceTrump(RuleAIContext context, IReadOnlyList<Card> selectedCards)
        {
            var remainingTrump = RuleAIUtility.RemoveCards(context.MyHand, selectedCards.ToList())
                .Where(_config.IsTrump)
                .GroupBy(card => (card.Suit, card.Rank))
                .Any(group => group.Count() >= 2);

            return remainingTrump;
        }

        private bool IsWeakNonScoreVoidBreak(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (cards.Count != 1)
                return false;

            var card = cards[0];
            if (_config.IsTrump(card) || card.Score > 0)
                return false;

            if (card.Rank >= Rank.Queen)
                return false;

            int sameSuitCount = context.MyHand.Count(existing => !_config.IsTrump(existing) && existing.Suit == card.Suit);
            if (sameSuitCount > 2)
                return false;

            var sameRankCount = context.MyHand.Count(existing => existing.Suit == card.Suit && existing.Rank == card.Rank);
            if (sameRankCount < 2)
                return true;

            return context.HandCount >= 10 &&
                card.Rank >= Rank.Two &&
                card.Rank <= Rank.Nine;
        }

        private bool HasExplicitVoidFollowUpBenefit(RuleAIContext context)
        {
            return context.HandProfile.TrumpCount > 0 ||
                context.DecisionFrame.BottomContestPressure >= RiskLevel.Medium ||
                HasMateTakeoverEvidence(context);
        }

        private bool IsImmediateCutRiskHigh(RuleAIContext context, IReadOnlyList<Card> cards)
        {
            if (cards.Count == 0 || cards.Any(_config.IsTrump))
                return false;

            string suitKey = $"Suit:{cards[0].Suit}";
            if (!context.InferenceSnapshot.LeadCutRiskBySystem.TryGetValue(suitKey, out var risk))
                return false;

            return risk.Level >= RiskLevel.High;
        }

        private static int CountPairUnitsInSuit(IEnumerable<Card> hand, Suit suit)
        {
            return hand
                .Where(card => card.Suit == suit)
                .GroupBy(card => (card.Suit, card.Rank))
                .Count(group => group.Count() >= 2);
        }

        private static bool IsExactPair(IReadOnlyList<Card> cards)
        {
            return cards.Count == 2 &&
                cards[0].Suit == cards[1].Suit &&
                cards[0].Rank == cards[1].Rank;
        }

        private static int GetMateIndex(int playerIndex)
        {
            return playerIndex < 0 ? -1 : (playerIndex + 2) % 4;
        }

        private static int ScoreToFutureValue(double score)
        {
            return (int)Math.Round(score, MidpointRounding.AwayFromZero);
        }

        private static string BuildCandidateKey(IReadOnlyList<Card> cards)
        {
            return string.Join("|", cards.Select(card => card.ToString()));
        }

        private sealed class LeadActionEntry
        {
            public List<Card> Cards { get; init; } = new();
            public double Score { get; init; }
            public string? ReasonCode { get; init; }
            public Dictionary<string, double> Features { get; init; } = new();
            public string Key => BuildCandidateKey(Cards);

            public LeadActionEntry Clone()
            {
                return new LeadActionEntry
                {
                    Cards = new List<Card>(Cards),
                    Score = Score,
                    ReasonCode = ReasonCode,
                    Features = new Dictionary<string, double>(Features)
                };
            }
        }

        private sealed class LeadSelectionResult
        {
            public LeadCandidateV30? SemanticCandidate { get; init; }
            public LeadActionEntry? Entry { get; init; }
            public string PrimaryIntent { get; init; } = LeadDecisionIntentV30.ProbeWeakSuit.ToString();
            public string SelectedCandidateId { get; init; } = "lead000.best_concrete_guard";
            public string SelectedReason { get; init; } = "lead000.best_concrete_guard";
            public List<string> TriggeredRules { get; init; } = new();

            public static LeadSelectionResult FromSemantic(LeadCandidateV30 candidate, LeadActionEntry entry)
            {
                return new LeadSelectionResult
                {
                    SemanticCandidate = candidate,
                    Entry = entry,
                    PrimaryIntent = candidate.Intent.ToString(),
                    SelectedCandidateId = candidate.CandidateId,
                    SelectedReason = candidate.CandidateId,
                    TriggeredRules = candidate.TriggeredRules.ToList()
                };
            }

            public static LeadSelectionResult CreateGuardFallback(LeadActionEntry? entry, LeadCandidateV30? rejectedCandidate)
            {
                string selectedReason = rejectedCandidate == null
                    ? "lead000.best_concrete_guard"
                    : $"lead000.best_concrete_guard({rejectedCandidate.CandidateId})";
                var triggeredRules = rejectedCandidate?.TriggeredRules.ToList() ?? new List<string>();
                triggeredRules.Add("Lead-ConcreteGuard");

                return new LeadSelectionResult
                {
                    SemanticCandidate = null,
                    Entry = entry,
                    PrimaryIntent = rejectedCandidate?.Intent.ToString() ?? LeadDecisionIntentV30.ProbeWeakSuit.ToString(),
                    SelectedCandidateId = "lead000.best_concrete_guard",
                    SelectedReason = selectedReason,
                    TriggeredRules = triggeredRules
                };
            }
        }
    }
}
