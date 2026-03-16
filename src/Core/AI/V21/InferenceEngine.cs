using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.Models;

namespace TractorGame.Core.AI.V21
{
    /// <summary>
    /// 基于记牌快照输出轻量级结构和风险推断。
    /// </summary>
    public sealed class InferenceEngine
    {
        private readonly GameConfig _config;

        public InferenceEngine(GameConfig config)
        {
            _config = config;
        }

        public InferenceSnapshot Build(
            CardMemory? memory,
            List<Card> myHand,
            int myPosition = -1,
            IEnumerable<int>? playerIndexes = null,
            int cardsLeftMin = -1,
            List<Card>? visibleBottomCards = null)
        {
            var positions = (playerIndexes ?? new[] { 0, 1, 2, 3 }).Distinct().ToList();
            if (memory == null)
                return new InferenceSnapshot();

            int myTrumpCount = myHand.Count(_config.IsTrump);
            int totalTrump = _config.GetTotalTrumpCount();
            int estimatedUnknownTrump = System.Math.Max(0, totalTrump - myTrumpCount - memory.GetPlayedTrumpCount());
            int playersToEstimate = System.Math.Max(1, positions.Count - (myPosition >= 0 ? 1 : 0));

            var trumpEstimate = new Dictionary<int, EstimateRange>();
            var highTrumpRisk = new Dictionary<int, RiskEstimate>();
            foreach (var player in positions)
            {
                if (player == myPosition)
                    continue;

                bool voidTrump = memory.IsPlayerVoidTrump(player);
                double estimate = voidTrump ? 0 : estimatedUnknownTrump / (double)playersToEstimate;
                double spread = voidTrump ? 0 : System.Math.Max(1, estimatedUnknownTrump / 2.0);
                trumpEstimate[player] = new EstimateRange
                {
                    Estimate = estimate,
                    Lower = System.Math.Max(0, estimate - spread),
                    Upper = estimate + spread,
                    Confidence = voidTrump ? 0.85 : 0.45
                };
                highTrumpRisk[player] = new RiskEstimate
                {
                    Level = estimate >= 4 ? RiskLevel.High : estimate >= 2 ? RiskLevel.Medium : RiskLevel.Low,
                    Confidence = voidTrump ? 0.80 : 0.50
                };
            }

            var pairPotential = new Dictionary<string, ProbabilityEstimate>();
            var tractorPotential = new Dictionary<string, ProbabilityEstimate>();
            var leadCutRisk = new Dictionary<string, RiskEstimate>();
            foreach (var suit in new[] { Suit.Spade, Suit.Heart, Suit.Club, Suit.Diamond })
            {
                var systemKey = $"Suit:{suit}";
                int voidCount = positions.Count(pos => pos != myPosition && memory.IsPlayerVoid(pos, suit));
                int noPairCount = CountEvidence(memory.GetNoPairEvidenceSnapshot(), systemKey, myPosition);
                int noTractorCount = CountEvidence(memory.GetNoTractorEvidenceSnapshot(), systemKey, myPosition);

                pairPotential[systemKey] = new ProbabilityEstimate
                {
                    Probability = System.Math.Max(0.10, 0.70 - voidCount * 0.20 - noPairCount * 0.25),
                    Confidence = 0.55
                };
                tractorPotential[systemKey] = new ProbabilityEstimate
                {
                    Probability = System.Math.Max(0.05, 0.55 - voidCount * 0.20 - noTractorCount * 0.25),
                    Confidence = 0.45
                };
                leadCutRisk[systemKey] = new RiskEstimate
                {
                    Level = voidCount >= 2 ? RiskLevel.High : voidCount == 1 ? RiskLevel.Medium : RiskLevel.Low,
                    Confidence = 0.65
                };
            }

            pairPotential["Trump"] = new ProbabilityEstimate
            {
                Probability = 0.65,
                Confidence = 0.55
            };
            tractorPotential["Trump"] = new ProbabilityEstimate
            {
                Probability = 0.45,
                Confidence = 0.45
            };
            leadCutRisk["Trump"] = new RiskEstimate
            {
                Level = RiskLevel.Medium,
                Confidence = 0.55
            };

            var visibleBottom = visibleBottomCards ?? new List<Card>();
            var visibleBottomScore = visibleBottom.Sum(card => card.Score);
            return new InferenceSnapshot
            {
                EstimatedTrumpCountByPlayer = trumpEstimate,
                HighTrumpRiskByPlayer = highTrumpRisk,
                PairPotentialBySystem = pairPotential,
                TractorPotentialBySystem = tractorPotential,
                LeadCutRiskBySystem = leadCutRisk,
                ThrowSafetyEstimate = new ThrowSafetyEstimate
                {
                    Level = "Unknown",
                    SuccessProbability = 0.50,
                    IsDeterministicallySafe = false
                },
                MateHoldConfidence = new ProbabilityEstimate
                {
                    Probability = cardsLeftMin <= 5 && cardsLeftMin >= 0 ? 0.70 : 0.52,
                    Confidence = cardsLeftMin > 0 ? 0.65 : 0.45
                },
                EndgameBottomThreat = new RiskEstimate
                {
                    Level = visibleBottomScore >= 20 ? RiskLevel.High : visibleBottomScore >= 10 ? RiskLevel.Medium : RiskLevel.Low,
                    Confidence = visibleBottomCards == null ? 0.35 : 0.85
                }
            };
        }

        private static int CountEvidence(Dictionary<int, List<string>> snapshot, string systemKey, int myPosition)
        {
            return snapshot.Count(entry => entry.Key != myPosition && entry.Value.Contains(systemKey));
        }
    }
}
