using System.Collections.Generic;

namespace TractorGame.Core.AI.V30.Lead
{
    public sealed class LeadCandidateGeneratorV30
    {
        private readonly LeadRuleEvaluatorV30 _ruleEvaluator;

        public LeadCandidateGeneratorV30(LeadRuleEvaluatorV30? ruleEvaluator = null)
        {
            _ruleEvaluator = ruleEvaluator ?? new LeadRuleEvaluatorV30();
        }

        public IReadOnlyList<LeadCandidateV30> Generate(LeadContextV30 context)
        {
            var candidates = new List<LeadCandidateV30>();

            if (_ruleEvaluator.IsHighValueSafeThrow(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead005.safe_throw.high",
                    Intent = LeadDecisionIntentV30.SafeThrow,
                    PriorityTier = 1,
                    ExpectedScore = context.SafeThrowExpectedScore,
                    FutureValue = context.SafeThrowFutureValue,
                    TriggeredRules = new[] { "Lead-005" }
                });
            }

            if (_ruleEvaluator.ShouldLead001DealerStableSideSuit(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead001.dealer_stable_side",
                    Intent = LeadDecisionIntentV30.StableSideSuitRun,
                    PriorityTier = 2,
                    FutureValue = context.StableSideSuitFutureValue,
                    TriggeredRules = new[] { "Lead-001" }
                });
            }

            if (_ruleEvaluator.ShouldLead002StrongScoreSideLead(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead002.score_side_cash",
                    Intent = LeadDecisionIntentV30.StableSideSuitRun,
                    PriorityTier = 3,
                    FutureValue = context.StrongScoreSideLeadFutureValue,
                    TriggeredRules = new[] { "Lead-002" }
                });
            }

            if (_ruleEvaluator.ShouldLead006TeamSideSuit(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead006.team_side_suit",
                    Intent = LeadDecisionIntentV30.StableSideSuitRun,
                    PriorityTier = 2,
                    FutureValue = context.TeamSideSuitFutureValue,
                    TriggeredRules = new[] { "Lead-006" }
                });
            }

            if (_ruleEvaluator.IsLowValueSafeThrow(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead005.safe_throw.low",
                    Intent = LeadDecisionIntentV30.SafeThrow,
                    PriorityTier = 2,
                    ExpectedScore = context.SafeThrowExpectedScore,
                    FutureValue = context.SafeThrowFutureValue,
                    TriggeredRules = new[] { "Lead-005" }
                });
            }

            if (_ruleEvaluator.ShouldLead003ForceTrump(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead003.force_trump",
                    Intent = LeadDecisionIntentV30.ForceTrump,
                    PriorityTier = 4,
                    FutureValue = context.ForceTrumpFutureValue,
                    TriggeredRules = new[] { "Lead-003" }
                });
            }

            if (_ruleEvaluator.ShouldLead007HandOff(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead007.handoff_to_mate",
                    Intent = LeadDecisionIntentV30.HandOffToMate,
                    PriorityTier = 5,
                    TriggeredRules = new[] { "Lead-007" }
                });
            }

            if (_ruleEvaluator.ShouldLead008ThreePairPlan(context))
            {
                string candidateId = context.ThreePairControlLevel == PairControlLevelV30.HighPairControl
                    ? "lead008.three_pair.high_control"
                    : "lead008.three_pair.low_pair_consume";

                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = candidateId,
                    Intent = LeadDecisionIntentV30.PrepareFutureThrow,
                    PriorityTier = 6,
                    FutureValue = context.FutureThrowExpectedScore,
                    TriggeredRules = new[] { "Lead-008" }
                });
            }

            if (_ruleEvaluator.ShouldLead008ForceTrumpForThrow(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead008.force_trump_for_throw",
                    Intent = LeadDecisionIntentV30.PrepareFutureThrow,
                    PriorityTier = 6,
                    ExpectedScore = context.FutureThrowExpectedScore,
                    FutureValue = context.FutureThrowExpectedScore,
                    TriggeredRules = new[] { "Lead-008" }
                });
            }

            if (_ruleEvaluator.ShouldLead009BuildVoid(context))
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead009.build_void",
                    Intent = LeadDecisionIntentV30.BuildVoid,
                    PriorityTier = 7,
                    FutureValue = context.VoidPlanFutureValue,
                    TriggeredRules = new[] { "Lead-009" }
                });
            }

            if (context.HasProbePlan || candidates.Count == 0)
            {
                candidates.Add(new LeadCandidateV30
                {
                    CandidateId = "lead004.low_value_probe",
                    Intent = LeadDecisionIntentV30.ProbeWeakSuit,
                    PriorityTier = 8,
                    FutureValue = context.ProbeFutureValue,
                    TriggeredRules = new[] { "Lead-004" }
                });
            }

            return candidates;
        }
    }
}
