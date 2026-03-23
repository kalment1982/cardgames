using System;
using System.Collections.Generic;

namespace WebUI.Application;

public sealed class ReviewSessionListItem
{
    public string SessionId { get; set; } = string.Empty;
    public string? RoundId { get; set; }
    public string? GameId { get; set; }
    public string? SourceTag { get; set; }
    public string? SourceLabel { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public int DealerIndex { get; set; } = -1;
    public string? LevelRank { get; set; }
    public string? TrumpSuit { get; set; }
    public int DefenderScore { get; set; }
    public int TrickCount { get; set; }
    public string? AiLineSummary { get; set; }
    public ReviewAiLineBreakdown AiLineBreakdown { get; set; } = new();
    public List<ReviewPlayerAiLine> PlayerAiLines { get; set; } = new();
}

public sealed class ReviewSessionDetail
{
    public ReviewSessionSummary Summary { get; set; } = new();
    public List<string> BottomCards { get; set; } = new();
    public List<ReviewTrickFrame> Tricks { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed class ReviewSessionSummary
{
    public string SessionId { get; set; } = string.Empty;
    public string? RoundId { get; set; }
    public string? GameId { get; set; }
    public string? SourceTag { get; set; }
    public string? SourceLabel { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public int DealerIndex { get; set; } = -1;
    public string? LevelRank { get; set; }
    public string? TrumpSuit { get; set; }
    public int DefenderScore { get; set; }
    public int TrickCount { get; set; }
    public string? AiLineSummary { get; set; }
    public ReviewAiLineBreakdown AiLineBreakdown { get; set; } = new();
    public List<ReviewPlayerAiLine> PlayerAiLines { get; set; } = new();
}

public sealed class ReviewAiLineBreakdown
{
    public int V30Decisions { get; set; }
    public int V21Decisions { get; set; }
    public int LegacyDecisions { get; set; }
    public int OtherDecisions { get; set; }
}

public sealed class ReviewPlayerAiLine
{
    public int PlayerIndex { get; set; } = -1;
    public string AiLine { get; set; } = string.Empty;
}

public sealed class ReviewTrickFrame
{
    public int TrickNo { get; set; }
    public string? TrickId { get; set; }
    public int LeadPlayer { get; set; } = -1;
    public int WinnerIndex { get; set; } = -1;
    public string? WinnerReason { get; set; }
    public int TrickScore { get; set; }
    public int DefenderScoreBefore { get; set; }
    public int DefenderScoreAfter { get; set; }
    public List<ReviewPlayerHandFrame> HandsBefore { get; set; } = new();
    public List<ReviewPlayFrame> Plays { get; set; } = new();
    public List<ReviewDecisionFrame> Decisions { get; set; } = new();
}

public sealed class ReviewPlayerHandFrame
{
    public int PlayerIndex { get; set; } = -1;
    public int HandCount { get; set; }
    public List<string> Cards { get; set; } = new();
}

public sealed class ReviewPlayFrame
{
    public int PlayerIndex { get; set; } = -1;
    public int PlayOrder { get; set; }
    public List<string> Cards { get; set; } = new();
}

public sealed class ReviewDecisionFrame
{
    public string? DecisionTraceId { get; set; }
    public int PlayerIndex { get; set; } = -1;
    public string? Phase { get; set; }
    public string? Path { get; set; }
    public string? PhasePolicy { get; set; }
    public string? AiLine { get; set; }
    public string? PrimaryIntent { get; set; }
    public string? SecondaryIntent { get; set; }
    public string? SelectedReason { get; set; }
    public string? SelectedCandidateId { get; set; }
    public List<string> TriggeredRules { get; set; } = new();
    public List<string> SelectedCards { get; set; } = new();
    public string? TurnId { get; set; }
    public int PlayPosition { get; set; }
    public string? V30Mode { get; set; }
    public string? V30PrimaryIntent { get; set; }
    public string? V30SelectedReason { get; set; }
    public int V30CandidateCount { get; set; }
}
