using System;
using System.Collections.Generic;
using System.Text.Json;

namespace TractorGame.Core.Review
{
    public sealed class ReviewCard
    {
        public string Suit { get; init; } = string.Empty;
        public string Rank { get; init; } = string.Empty;
        public int Score { get; init; }
        public string Text { get; init; } = string.Empty;
    }

    public sealed class ReviewPlayerHand
    {
        public int PlayerIndex { get; init; }
        public int HandCount { get; init; }
        public List<ReviewCard> Cards { get; init; } = new();
    }

    public sealed class ReviewPlay
    {
        public int PlayerIndex { get; init; }
        public int Order { get; init; }
        public List<ReviewCard> Cards { get; init; } = new();
    }

    public sealed class ReviewDecision
    {
        public string DecisionTraceId { get; set; } = string.Empty;
        public string TurnId { get; set; } = string.Empty;
        public int PlayerIndex { get; set; } = -1;
        public string Actor { get; set; } = string.Empty;
        public string Phase { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string PhasePolicy { get; set; } = string.Empty;
        public string PrimaryIntent { get; set; } = string.Empty;
        public string SecondaryIntent { get; set; } = string.Empty;
        public string SelectedReason { get; set; } = string.Empty;
        public string SelectedCandidateId { get; set; } = string.Empty;
        public int PlayPosition { get; set; }
        public List<string> TriggeredRules { get; set; } = new();
        public List<ReviewCard> SelectedCards { get; set; } = new();
        public JsonElement? Bundle { get; set; }
        public JsonElement? BundleV30 { get; set; }
    }

    public sealed class ReviewTrick
    {
        public int TrickNo { get; init; }
        public string TrickId { get; init; } = string.Empty;
        public int LeadPlayer { get; set; } = -1;
        public int WinnerIndex { get; set; } = -1;
        public string WinnerReason { get; set; } = string.Empty;
        public int TrickScore { get; set; }
        public int DefenderScoreBefore { get; set; }
        public int DefenderScoreAfter { get; set; }
        public List<ReviewPlayerHand> HandsBefore { get; set; } = new();
        public List<ReviewPlay> Plays { get; set; } = new();
        public List<ReviewDecision> Decisions { get; set; } = new();
    }

    public sealed class ReviewSessionSummary
    {
        public string SessionId { get; set; } = string.Empty;
        public string RoundId { get; set; } = string.Empty;
        public string GameId { get; set; } = string.Empty;
        public string SourceTag { get; set; } = string.Empty;
        public string SourceLabel { get; set; } = string.Empty;
        public DateTime StartedAtUtc { get; set; }
        public int DealerIndex { get; set; } = -1;
        public string LevelRank { get; set; } = string.Empty;
        public string TrumpSuit { get; set; } = string.Empty;
        public int DefenderScore { get; set; }
        public int TrickCount { get; set; }
        public string AiLineSummary { get; set; } = string.Empty;
        public List<ReviewPlayerAiLine> PlayerAiLines { get; set; } = new();
    }

    public sealed class ReviewPlayerAiLine
    {
        public int PlayerIndex { get; set; } = -1;
        public string AiLine { get; set; } = string.Empty;
    }

    public sealed class ReviewSessionDetail
    {
        public ReviewSessionSummary Summary { get; init; } = new();
        public List<ReviewCard> BottomCards { get; init; } = new();
        public List<ReviewTrick> Tricks { get; init; } = new();
    }
}
