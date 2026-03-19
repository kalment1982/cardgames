using System.Collections.Generic;
using TractorGame.Core.AI.V30.Memory;
using TractorGame.Core.Models;
using Xunit;

namespace TractorGame.Tests.V30.Acceptance
{
    public class MemoryAcceptanceTests
    {
        private readonly PartnerCooperationPolicyV30 _cooperationPolicy = new PartnerCooperationPolicyV30();
        private readonly InferenceEngineV30 _inferenceEngine = new InferenceEngineV30();
        private readonly ThreatAssessmentV30 _threatAssessment = new ThreatAssessmentV30();

        [Fact]
        public void Mate001_PartnerStableWin_ShouldAllowPointFeedWithoutOvertaking()
        {
            var allow = _cooperationPolicy.CanPureDonatePoints(new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.StableWin,
                TeammateWinConfidence = 0.70
            });

            var deny = _cooperationPolicy.CanPureDonatePoints(new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.FragileWin,
                TeammateWinConfidence = 0.95
            });

            Assert.True(allow);
            Assert.False(deny);
        }

        [Fact]
        public void Mate003_NoOutcomeDelta_ShouldPreferSmallCardAndPreserveStructure()
        {
            var decision = _cooperationPolicy.Decide(
                new PartnerCooperationContextV30
                {
                    IsTeammateCurrentlyWinning = true,
                    TeammateWinSecurity = WinSecurityLevelV30.StableWin,
                    TeammateWinConfidence = 0.9,
                    NoMaterialDifference = true
                },
                new List<CooperationCandidateV30>
                {
                    new() { CandidateId = "a", ControlSpendCost = 3, StructureBreakCost = 0, PointValue = 0 },
                    new() { CandidateId = "b", ControlSpendCost = 1, StructureBreakCost = 1, PointValue = 5 },
                    new() { CandidateId = "c", ControlSpendCost = 1, StructureBreakCost = 0, PointValue = 10 }
                });

            Assert.Equal("c", decision.SelectedCandidate?.CandidateId);
            Assert.Equal("NoMaterialDifferencePreferSmallAndStructure", decision.Reason);
        }

        [Fact]
        public void Memory002_VoidInference_ShouldSeparateConfirmedAndProbabilisticFacts()
        {
            var confirmedVoid = _inferenceEngine.ObserveFollowAction(
                playerIndex: 1,
                ledSuit: Suit.Heart,
                playedCards: new List<Card> { new Card(Suit.Spade, Rank.Five) });

            var probableHas = _inferenceEngine.BuildSuitKnowledge(
                playerIndex: 2,
                suit: Suit.Heart,
                confirmedVoid: false,
                probabilityHasSuit: 0.72);

            Assert.Equal(SuitKnowledgeStateV30.ConfirmedVoid, confirmedVoid.State);
            Assert.Equal(SuitKnowledgeStateV30.ProbablyHasSuit, probableHas.State);
            Assert.False(probableHas.ConfirmedVoid);
        }

        [Fact]
        public void Memory006_RearThreatAssessment_ShouldClassifyFragileStableLockWins()
        {
            var lockWin = _threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 2, IsTeammate = true, OvertakeProbability = 0.9 }
                }
            });

            var stableWin = _threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.1 }
                }
            });

            var fragileWin = _threatAssessment.Evaluate(new ThreatAssessmentInputV30
            {
                CandidateCanBeatCurrentWinner = true,
                RemainingPlayers = new[]
                {
                    new RemainingPlayerThreatV30 { PlayerIndex = 1, IsTeammate = false, OvertakeProbability = 0.7 },
                    new RemainingPlayerThreatV30 { PlayerIndex = 3, IsTeammate = false, OvertakeProbability = 0.5 }
                }
            });

            Assert.Equal(WinSecurityLevelV30.LockWin, lockWin.WinSecurity);
            Assert.Equal(WinSecurityLevelV30.StableWin, stableWin.WinSecurity);
            Assert.Equal(WinSecurityLevelV30.FragileWin, fragileWin.WinSecurity);
        }
    }
}
