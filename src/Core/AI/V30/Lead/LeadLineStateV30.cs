namespace TractorGame.Core.AI.V30.Lead
{
    public enum LeadLineKind
    {
        None,
        StableSideSuitRun,
        ScorePush,
        TrumpSqueeze,
        EndgameControl,
        HandOffToMate
    }

    public sealed class LeadLineStateV30
    {
        public LeadLineKind ActiveLine { get; set; } = LeadLineKind.None;
        public string? ActiveSuit { get; set; }
        public string? ActiveCandidateId { get; set; }
        public int ConsecutiveWins { get; set; }
        public int ConsecutiveLeads { get; set; }
        public bool LastTrickWon { get; set; }
        public int AccumulatedScore { get; set; }

        public bool IsInRun => ActiveLine != LeadLineKind.None && LastTrickWon && ConsecutiveWins >= 1;

        public void Reset()
        {
            ActiveLine = LeadLineKind.None;
            ActiveSuit = null;
            ActiveCandidateId = null;
            ConsecutiveWins = 0;
            ConsecutiveLeads = 0;
            LastTrickWon = false;
            AccumulatedScore = 0;
        }
    }
}
