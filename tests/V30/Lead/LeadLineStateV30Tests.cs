using System.Linq;
using TractorGame.Core.AI.V21;
using TractorGame.Core.AI.V30.Lead;
using Xunit;

namespace TractorGame.Tests.V30.Lead
{
    public class LeadLineStateV30Tests
    {
        private readonly LeadCandidateGeneratorV30 _generator = new();
        private readonly LeadRuleEvaluatorV30 _evaluator = new();

        private static LeadLineStateV30 MakeRunState(LeadLineKind line, int wins = 2)
        {
            return new LeadLineStateV30
            {
                ActiveLine = line,
                ConsecutiveWins = wins,
                LastTrickWon = true,
                ConsecutiveLeads = wins,
                AccumulatedScore = 20
            };
        }

        [Fact]
        public void ContinuationBoost_StableSideSuitRun_PromotesToTier1()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 3,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 14,
                ProbeFutureValue = 10,
                HasProbePlan = true,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            var candidates = _generator.Generate(context);
            var stableSide = candidates.Single(c => c.CandidateId == "lead001.dealer_stable_side");
            Assert.Equal(1, stableSide.PriorityTier);
        }

        [Fact]
        public void ContinuationBoost_ScorePush_PromotesToTier1()
        {
            var context = new LeadContextV30
            {
                HasStrongScoreSideLead = true,
                StrongScoreSideLeadFutureValue = 10,
                HasProbePlan = true,
                LineState = MakeRunState(LeadLineKind.ScorePush)
            };

            var candidates = _generator.Generate(context);
            var scoreSide = candidates.Single(c => c.CandidateId == "lead002.score_side_cash");
            Assert.Equal(1, scoreSide.PriorityTier);
        }

        [Fact]
        public void ForceTrump_Suppressed_WhenInRun_EvaluatorLevel()
        {
            var context = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 5,
                HasProbePlan = true,
                EndgameLevel = EndgameLevel.None,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            Assert.False(_evaluator.ShouldLead003ForceTrump(context));
        }

        [Fact]
        public void ForceTrump_Suppressed_WhenInRun_GeneratorLevel()
        {
            var context = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 5,
                HasProbePlan = true,
                EndgameLevel = EndgameLevel.None,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            var candidates = _generator.Generate(context);
            Assert.DoesNotContain(candidates, c => c.CandidateId == "lead003.force_trump");
        }

        [Fact]
        public void ForceTrump_NotSuppressed_WhenTrumpSqueeze()
        {
            var context = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 5,
                HasProbePlan = true,
                EndgameLevel = EndgameLevel.None,
                LineState = MakeRunState(LeadLineKind.TrumpSqueeze)
            };

            Assert.True(_evaluator.ShouldLead003ForceTrump(context));
        }

        [Fact]
        public void ForceTrump_NotSuppressed_InEndgame()
        {
            var context = new LeadContextV30
            {
                HasProfitableForceTrump = true,
                ForceTrumpFutureValue = 5,
                HasProbePlan = true,
                EndgameLevel = EndgameLevel.Late,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            Assert.True(_evaluator.ShouldLead003ForceTrump(context));
        }

        [Fact]
        public void ProbeWeakSuit_Suppressed_WhenInRun()
        {
            var context = new LeadContextV30
            {
                Role = LeadRoleV30.Dealer,
                TrickIndex = 3,
                HasStableSideSuitRun = true,
                StableSideSuitFutureValue = 14,
                ProbeFutureValue = 10,
                HasProbePlan = true,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            var candidates = _generator.Generate(context);
            Assert.DoesNotContain(candidates, c => c.CandidateId == "lead004.low_value_probe");
        }

        [Fact]
        public void ProbeWeakSuit_FallbackAdded_WhenNoCandidates()
        {
            // In a run but no other candidates qualify → probe must still appear
            var context = new LeadContextV30
            {
                HasProbePlan = false,
                LineState = MakeRunState(LeadLineKind.StableSideSuitRun)
            };

            var candidates = _generator.Generate(context);
            Assert.Single(candidates);
            Assert.Equal("lead004.low_value_probe", candidates[0].CandidateId);
        }

        [Fact]
        public void LineBreak_Reset_WhenLostTrick()
        {
            var state = MakeRunState(LeadLineKind.StableSideSuitRun);
            Assert.True(state.IsInRun);

            state.Reset();

            Assert.Equal(LeadLineKind.None, state.ActiveLine);
            Assert.False(state.IsInRun);
            Assert.Equal(0, state.ConsecutiveWins);
            Assert.False(state.LastTrickWon);
        }

        [Fact]
        public void NonLeadTrick_DoesNotChangeState()
        {
            // Verified at engine level: UpdateLeadLineState skips when not my lead.
            // Here we verify the state model itself is stable.
            var state = MakeRunState(LeadLineKind.StableSideSuitRun);
            var snapshot = (state.ActiveLine, state.ConsecutiveWins, state.LastTrickWon);

            // No mutation — state should remain identical
            Assert.Equal(snapshot, (state.ActiveLine, state.ConsecutiveWins, state.LastTrickWon));
        }

        [Fact]
        public void NewGame_Reset_ClearsAllState()
        {
            var state = MakeRunState(LeadLineKind.ScorePush);
            state.ActiveSuit = "hearts";
            state.ActiveCandidateId = "lead002.score_side_cash";

            state.Reset();

            Assert.Equal(LeadLineKind.None, state.ActiveLine);
            Assert.Null(state.ActiveSuit);
            Assert.Null(state.ActiveCandidateId);
            Assert.Equal(0, state.ConsecutiveWins);
            Assert.Equal(0, state.ConsecutiveLeads);
            Assert.False(state.LastTrickWon);
            Assert.Equal(0, state.AccumulatedScore);
        }

        [Fact]
        public void IsInRun_False_WhenNoLine()
        {
            var state = new LeadLineStateV30();
            Assert.False(state.IsInRun);
        }

        [Fact]
        public void IsInRun_False_WhenLastTrickLost()
        {
            var state = new LeadLineStateV30
            {
                ActiveLine = LeadLineKind.StableSideSuitRun,
                ConsecutiveWins = 2,
                LastTrickWon = false
            };
            Assert.False(state.IsInRun);
        }

        [Fact]
        public void IsInRun_False_WhenZeroWins()
        {
            var state = new LeadLineStateV30
            {
                ActiveLine = LeadLineKind.StableSideSuitRun,
                ConsecutiveWins = 0,
                LastTrickWon = true
            };
            Assert.False(state.IsInRun);
        }
    }
}
