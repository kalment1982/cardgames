using TractorGame.Core.AI.Evolution;

namespace TractorGame.Core.AI.Evolution.GateKeeper
{
    public sealed class HardConstraintValidator
    {
        public bool Validate(CandidateEvaluation evaluation)
        {
            return evaluation.CandidateIllegalRate <= 0
                   && evaluation.CandidateAvgLatencyMs < 100
                   && evaluation.CandidateP99LatencyMs < 150;
        }
    }
}
