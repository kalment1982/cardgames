using System.Collections.Generic;
using System.Linq;

namespace TractorGame.Core.AI.Evolution.LeagueArena
{
    public sealed class SingleGameOutcome
    {
        public bool CandidateWon { get; set; }
        public bool CandidateIsDealerSide { get; set; }
        public int CandidateDecisions { get; set; }
        public int CandidateIllegalDecisions { get; set; }
        public int OpponentDecisions { get; set; }
        public int OpponentIllegalDecisions { get; set; }
        public List<double> CandidateLatenciesMs { get; set; } = new();
        public List<double> OpponentLatenciesMs { get; set; } = new();
        public int CandidateDistinctActions { get; set; }
        public int OpponentDistinctActions { get; set; }
    }

    public sealed class SelfPlayAggregate
    {
        public List<SingleGameOutcome> Outcomes { get; } = new();

        public int Games => Outcomes.Count;
        public int CandidateWins => Outcomes.Count(x => x.CandidateWon);
        public int CandidateDecisions => Outcomes.Sum(x => x.CandidateDecisions);
        public int CandidateIllegalDecisions => Outcomes.Sum(x => x.CandidateIllegalDecisions);
        public int OpponentDecisions => Outcomes.Sum(x => x.OpponentDecisions);
        public int OpponentIllegalDecisions => Outcomes.Sum(x => x.OpponentIllegalDecisions);
        public int CandidateDealerSideGames => Outcomes.Count(x => x.CandidateIsDealerSide);
        public int CandidateDealerSideWins => Outcomes.Count(x => x.CandidateIsDealerSide && x.CandidateWon);
        public int CandidateDefenderSideGames => Outcomes.Count(x => !x.CandidateIsDealerSide);
        public int CandidateDefenderSideWins => Outcomes.Count(x => !x.CandidateIsDealerSide && x.CandidateWon);

        public double CandidateAvgLatencyMs => Average(Outcomes.SelectMany(x => x.CandidateLatenciesMs));
        public double OpponentAvgLatencyMs => Average(Outcomes.SelectMany(x => x.OpponentLatenciesMs));
        public double CandidateP99LatencyMs => Percentile(Outcomes.SelectMany(x => x.CandidateLatenciesMs).ToList(), 0.99);
        public double OpponentP99LatencyMs => Percentile(Outcomes.SelectMany(x => x.OpponentLatenciesMs).ToList(), 0.99);

        public double CandidateDiversity => CandidateDecisions == 0 ? 0 : (double)Outcomes.Sum(x => x.CandidateDistinctActions) / CandidateDecisions;
        public double OpponentDiversity => OpponentDecisions == 0 ? 0 : (double)Outcomes.Sum(x => x.OpponentDistinctActions) / OpponentDecisions;

        private static double Average(IEnumerable<double> values)
        {
            var data = values.ToList();
            if (data.Count == 0)
                return 0;

            return data.Average();
        }

        private static double Percentile(List<double> values, double p)
        {
            if (values.Count == 0)
                return 0;

            values.Sort();
            var index = (int)System.Math.Ceiling(values.Count * p) - 1;
            index = System.Math.Clamp(index, 0, values.Count - 1);
            return values[index];
        }
    }
}
