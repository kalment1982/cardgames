using System;
using System.Collections.Generic;
using System.Linq;
using TractorGame.Core.AI;
using TractorGame.Core.AI.Evolution;

namespace TractorGame.Core.AI.Evolution.PolicyFactory
{
    public sealed class CandidateFactory
    {
        private readonly MutationOperator _mutator;
        private readonly CrossoverOperator _crossover;
        private readonly RepairOperator _repair;

        public CandidateFactory(int seed = 0)
        {
            _mutator = new MutationOperator(seed == 0 ? 11 : seed + 11);
            _crossover = new CrossoverOperator(seed == 0 ? 17 : seed + 17);
            _repair = new RepairOperator();
        }

        public List<CandidateProfile> Generate(
            AIStrategyParameters champion,
            string championHash,
            int generation,
            int count,
            EvolutionConfig? config = null)
        {
            var cache = new DeduplicationCache();
            var result = new List<CandidateProfile>(count);

            if (ShouldInjectDefenderBoost(config, generation))
            {
                var boosted = BuildDefenderBoostCandidate(champion, championHash, generation);
                cache.TryAdd(boosted.GenomeHash);
                result.Add(boosted);
            }

            var attempt = 0;
            var maxAttempt = count * 30;
            while (result.Count < count && attempt < maxAttempt)
            {
                attempt++;
                var exploratory = result.Count >= count * 0.65;

                AIStrategyParameters raw;
                if (result.Count % 4 == 0 && result.Count > 1)
                {
                    var p1 = result[result.Count - 1].Parameters;
                    var p2 = result[result.Count - 2].Parameters;
                    raw = _crossover.Crossover(p1, p2);
                }
                else
                {
                    raw = _mutator.Mutate(champion, exploratory);
                }

                var repaired = _repair.Repair(raw);
                var genome = new ParameterGenome(repaired);
                var hash = genome.ComputeHash();
                if (!cache.TryAdd(hash))
                    continue;

                var candidateId = $"gen_{generation:D3}_candidate_{result.Count:D2}";
                result.Add(new CandidateProfile
                {
                    CandidateId = candidateId,
                    Generation = generation,
                    ParentHash = championHash,
                    GenomeHash = hash,
                    Parameters = repaired
                });
            }

            if (result.Count == 0)
            {
                var fallback = _repair.Repair(_mutator.Mutate(champion, exploratory: false));
                var fallbackHash = new ParameterGenome(fallback).ComputeHash();
                result.Add(new CandidateProfile
                {
                    CandidateId = $"gen_{generation:D3}_candidate_00",
                    Generation = generation,
                    ParentHash = championHash,
                    GenomeHash = fallbackHash,
                    Parameters = fallback
                });
            }

            return result;
        }

        private CandidateProfile BuildDefenderBoostCandidate(
            AIStrategyParameters champion,
            string championHash,
            int generation)
        {
            var boosted = champion.Clone();
            boosted.FollowBeatAttemptBias = 0.85;
            boosted.FollowTrumpCutPriority = 0.78;
            boosted.PointCardProtectionWeight = 0.80;
            boosted.OpponentDenyPointBias = 0.82;
            boosted.EndgameFinishBias = 0.75;
            boosted.EndgameStabilityBias = 0.82;
            boosted = _repair.Repair(boosted);

            var hash = new ParameterGenome(boosted).ComputeHash();
            return new CandidateProfile
            {
                CandidateId = $"gen_{generation:D3}_candidate_00",
                Generation = generation,
                ParentHash = championHash,
                GenomeHash = hash,
                Parameters = boosted
            };
        }

        private static bool ShouldInjectDefenderBoost(EvolutionConfig? config, int generation)
        {
            if (config == null || !config.ForceDefenderBoostCandidate)
                return false;

            return generation == config.ForceDefenderBoostGeneration;
        }
    }
}
