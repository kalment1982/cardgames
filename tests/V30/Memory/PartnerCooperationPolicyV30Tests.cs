using System.Collections.Generic;
using TractorGame.Core.AI.V30.Memory;
using Xunit;

namespace TractorGame.Tests.V30.Memory
{
    public class PartnerCooperationPolicyV30Tests
    {
        private readonly PartnerCooperationPolicyV30 _policy = new();

        [Fact]
        public void CanPureDonatePoints_WhenLockWin_ReturnsTrue()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.LockWin,
                TeammateWinConfidence = 0.40
            };

            Assert.True(_policy.CanPureDonatePoints(context));
        }

        [Fact]
        public void CanPureDonatePoints_WhenStableWinAndConfidenceAtThreshold_ReturnsTrue()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.StableWin,
                TeammateWinConfidence = 0.70
            };

            Assert.True(_policy.CanPureDonatePoints(context));
        }

        [Fact]
        public void CanPureDonatePoints_WhenStableWinButConfidenceLow_ReturnsFalse()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.StableWin,
                TeammateWinConfidence = 0.69
            };

            Assert.False(_policy.CanPureDonatePoints(context));
        }

        [Fact]
        public void Decide_WhenNoMaterialDifference_PrefersSmallAndPreserveStructure()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.StableWin,
                TeammateWinConfidence = 0.80,
                NoMaterialDifference = true
            };

            var candidates = new List<CooperationCandidateV30>
            {
                new() { CandidateId = "high_control", ControlSpendCost = 5, StructureBreakCost = 1, PointValue = 10 },
                new() { CandidateId = "small_safe", ControlSpendCost = 1, StructureBreakCost = 0, PointValue = 5 },
                new() { CandidateId = "small_but_break", ControlSpendCost = 1, StructureBreakCost = 2, PointValue = 0 }
            };

            var decision = _policy.Decide(context, candidates);

            Assert.Equal("small_safe", decision.SelectedCandidate?.CandidateId);
            Assert.Equal("NoMaterialDifferencePreferSmallAndStructure", decision.Reason);
        }

        [Fact]
        public void Decide_WhenSafeToDonateAndMaterialDifference_SelectsHighestPointCandidate()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = true,
                TeammateWinSecurity = WinSecurityLevelV30.LockWin,
                TeammateWinConfidence = 0.95,
                NoMaterialDifference = false
            };

            var candidates = new List<CooperationCandidateV30>
            {
                new() { CandidateId = "low_point", ControlSpendCost = 0, StructureBreakCost = 0, PointValue = 0 },
                new() { CandidateId = "high_point", ControlSpendCost = 2, StructureBreakCost = 1, PointValue = 20 }
            };

            var decision = _policy.Decide(context, candidates);

            Assert.True(decision.AllowPurePointDonation);
            Assert.Equal("high_point", decision.SelectedCandidate?.CandidateId);
            Assert.Equal("SafeDonatePoints", decision.Reason);
        }

        [Fact]
        public void Decide_WhenNotSafeToDonate_PreservesControlFirst()
        {
            var context = new PartnerCooperationContextV30
            {
                IsTeammateCurrentlyWinning = false,
                TeammateWinSecurity = WinSecurityLevelV30.FragileWin,
                TeammateWinConfidence = 0.30,
                NoMaterialDifference = false
            };

            var candidates = new List<CooperationCandidateV30>
            {
                new() { CandidateId = "expensive", ControlSpendCost = 4, StructureBreakCost = 0, PointValue = 0 },
                new() { CandidateId = "cheap", ControlSpendCost = 1, StructureBreakCost = 1, PointValue = 10 }
            };

            var decision = _policy.Decide(context, candidates);

            Assert.False(decision.AllowPurePointDonation);
            Assert.Equal("cheap", decision.SelectedCandidate?.CandidateId);
            Assert.Equal("PreserveControl", decision.Reason);
        }
    }
}
