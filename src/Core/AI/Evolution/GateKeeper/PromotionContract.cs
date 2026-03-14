using System;
using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.Evolution.GateKeeper
{
    public sealed class PromotionDecision
    {
        public bool Promote { get; set; }
        public string Reason { get; set; } = string.Empty;
        public CandidateEvaluation? Winner { get; set; }
    }

    public sealed class PromotionContract
    {
        private readonly HardConstraintValidator _hardValidator = new();
        private readonly StatisticalTester _stats = new(seed: 20260314);

        public PromotionDecision Decide(
            IReadOnlyList<CandidateEvaluation> finalCandidates,
            int generation,
            bool inCooldown)
        {
            var decision = new PromotionDecision();
            if (inCooldown)
            {
                decision.Promote = false;
                decision.Reason = "Cooldown active after rollback.";
                return decision;
            }

            var best = finalCandidates
                .OrderByDescending(c => c.WinRate)
                .ThenBy(c => c.CandidateP99LatencyMs)
                .FirstOrDefault();

            if (best == null)
            {
                decision.Reason = "No candidate available.";
                return decision;
            }

            decision.Winner = best;
            if (!_hardValidator.Validate(best))
            {
                decision.Reason = "Hard constraints failed.";
                decision.Promote = false;
                return decision;
            }

            var candidateOutcomes = _stats.ToWinOutcomes(best.Wins, best.Games).ToList();
            var championOutcomes = candidateOutcomes
                .Select(x => 1 - x)
                .ToList();
            var (diffMean, diffLow, diffHigh) = _stats.BootstrapDifferenceCI(
                candidateOutcomes,
                championOutcomes,
                iterations: 10000,
                confidenceLevel: 0.95);

            if (diffLow > 0)
            {
                decision.Promote = true;
                decision.Reason = $"Bootstrap diff CI lower bound ({diffLow:P2}) > 0 (mean {diffMean:P2}, high {diffHigh:P2}).";
                return decision;
            }

            // 平局晋升：平均提升明显，且下界只允许轻微为负。
            if (diffMean > 0.015 && diffLow > -0.005)
            {
                decision.Promote = true;
                decision.Reason = $"Quality promotion by bootstrap diff (mean {diffMean:P2}, low {diffLow:P2}).";
                return decision;
            }

            // Additional tie-zone quality promotion based on behavior metrics.
            var delta = best.WinRate - 0.5;
            if (delta >= -0.005 && delta <= 0.005)
            {
                var lowerIllegal = best.OpponentIllegalRate > 0
                    ? best.CandidateIllegalRate <= best.OpponentIllegalRate * 0.9
                    : best.CandidateIllegalRate <= 0.001;
                var lowerP99 = best.OpponentP99LatencyMs > 0
                    ? best.CandidateP99LatencyMs <= best.OpponentP99LatencyMs * 0.9
                    : true;
                var higherDiversity = best.OpponentDiversity > 0
                    ? best.CandidateDiversity >= best.OpponentDiversity * 1.08
                    : best.CandidateDiversity > 0;

                if (lowerIllegal || lowerP99 || higherDiversity)
                {
                    decision.Promote = true;
                    decision.Reason = "Quality promotion in tie zone.";
                    return decision;
                }
            }

            decision.Promote = false;
            decision.Reason = "Promotion threshold not met.";
            return decision;
        }
    }
}
