using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.V30.Memory
{
    public sealed class PartnerCooperationContextV30
    {
        public bool IsTeammateCurrentlyWinning { get; init; }
        public WinSecurityLevelV30 TeammateWinSecurity { get; init; } = WinSecurityLevelV30.FragileWin;
        public double TeammateWinConfidence { get; init; } = 0.0;
        public bool NoMaterialDifference { get; init; }
    }

    public sealed class CooperationCandidateV30
    {
        public string CandidateId { get; init; } = string.Empty;
        public int PointValue { get; init; }
        public int ControlSpendCost { get; init; }
        public int StructureBreakCost { get; init; }
        public bool MateriallyDifferentOutcome { get; init; }
    }

    public sealed class PartnerCooperationDecisionV30
    {
        public bool AllowPurePointDonation { get; init; }
        public CooperationCandidateV30? SelectedCandidate { get; init; }
        public string Reason { get; init; } = string.Empty;
    }

    public sealed class PartnerCooperationPolicyV30
    {
        public const double HighConfidenceThreshold = 0.70;

        public bool CanPureDonatePoints(PartnerCooperationContextV30 context)
        {
            if (!context.IsTeammateCurrentlyWinning)
                return false;

            if (context.TeammateWinSecurity == WinSecurityLevelV30.LockWin)
                return true;

            return context.TeammateWinSecurity == WinSecurityLevelV30.StableWin &&
                context.TeammateWinConfidence >= HighConfidenceThreshold;
        }

        public PartnerCooperationDecisionV30 Decide(
            PartnerCooperationContextV30 context,
            IReadOnlyList<CooperationCandidateV30> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new PartnerCooperationDecisionV30
                {
                    AllowPurePointDonation = false,
                    SelectedCandidate = null,
                    Reason = "NoCandidate"
                };
            }

            bool canDonatePoints = CanPureDonatePoints(context);
            if (context.NoMaterialDifference)
            {
                var selected = SelectSmallAndPreserveStructure(candidates);
                return new PartnerCooperationDecisionV30
                {
                    AllowPurePointDonation = canDonatePoints,
                    SelectedCandidate = selected,
                    Reason = "NoMaterialDifferencePreferSmallAndStructure"
                };
            }

            if (canDonatePoints)
            {
                var selected = candidates
                    .OrderByDescending(item => item.PointValue)
                    .ThenBy(item => item.ControlSpendCost)
                    .ThenBy(item => item.StructureBreakCost)
                    .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                    .First();

                return new PartnerCooperationDecisionV30
                {
                    AllowPurePointDonation = true,
                    SelectedCandidate = selected,
                    Reason = "SafeDonatePoints"
                };
            }

            var conservative = candidates
                .OrderBy(item => item.ControlSpendCost)
                .ThenBy(item => item.StructureBreakCost)
                .ThenBy(item => item.PointValue)
                .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                .First();

            return new PartnerCooperationDecisionV30
            {
                AllowPurePointDonation = false,
                SelectedCandidate = conservative,
                Reason = "PreserveControl"
            };
        }

        private static CooperationCandidateV30 SelectSmallAndPreserveStructure(IReadOnlyList<CooperationCandidateV30> candidates)
        {
            return candidates
                .OrderBy(item => item.ControlSpendCost)
                .ThenBy(item => item.StructureBreakCost)
                .ThenBy(item => item.PointValue)
                .ThenBy(item => item.CandidateId, StringComparer.Ordinal)
                .First();
        }
    }
}
