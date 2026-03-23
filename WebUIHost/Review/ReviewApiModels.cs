using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace WebUIHost.Review;

public sealed class ReviewSessionsResponse
{
    public DateTime GeneratedAtUtc { get; init; }
    public int Total { get; init; }
    public List<ReviewSessionSummary> Sessions { get; init; } = new();
}

public sealed class ReviewSessionSummary
{
    public string SessionId { get; init; } = string.Empty;
    public string RoundId { get; init; } = string.Empty;
    public string? GameId { get; init; }
    public string SourceTag { get; init; } = string.Empty;
    public string SourceLabel { get; init; } = string.Empty;
    public DateTime? StartedAtUtc { get; init; }
    public int DealerIndex { get; init; } = -1;
    public string? LevelRank { get; init; }
    public string? TrumpSuit { get; init; }
    public int DefenderScore { get; init; }
    public int TrickCount { get; init; }
    public string AiLineSummary { get; init; } = string.Empty;
    public ReviewAiLineBreakdown AiLineBreakdown { get; init; } = new();
    public List<ReviewPlayerAiLine> PlayerAiLines { get; init; } = new();
}

public sealed class ReviewAiLineBreakdown
{
    public int V30Decisions { get; init; }
    public int V21Decisions { get; init; }
    public int LegacyDecisions { get; init; }
    public int OtherDecisions { get; init; }
    public Dictionary<string, int> PathCounts { get; init; } = new(StringComparer.Ordinal);
}

public sealed class ReviewPlayerAiLine
{
    public int PlayerIndex { get; init; } = -1;
    public string AiLine { get; init; } = string.Empty;
}

public sealed class ReviewSessionDetailResponse
{
    public DateTime GeneratedAtUtc { get; init; }
    public ReviewSessionSummary Summary { get; init; } = new();
    public List<string> BottomCards { get; init; } = new();
    public List<ReviewTrickDetail> Tricks { get; init; } = new();
    public List<string> Warnings { get; init; } = new();
}

public sealed class ReviewTrickDetail
{
    public int TrickNo { get; init; }
    public string TrickId { get; init; } = string.Empty;
    public int LeadPlayer { get; set; } = -1;
    public int WinnerIndex { get; set; } = -1;
    public string? WinnerReason { get; set; }
    public int TrickScore { get; set; }
    public int DefenderScoreBefore { get; set; }
    public int DefenderScoreAfter { get; set; }
    public List<ReviewPlayerHand> HandsBefore { get; set; } = new();
    public List<ReviewPlay> Plays { get; set; } = new();
    public List<ReviewDecision> Decisions { get; set; } = new();
}

public sealed class ReviewPlayerHand
{
    public int PlayerIndex { get; init; } = -1;
    public int HandCount { get; init; }
    public List<string> Cards { get; init; } = new();
}

public sealed class ReviewPlay
{
    public int PlayOrder { get; init; }
    public int PlayerIndex { get; init; } = -1;
    public List<string> Cards { get; init; } = new();
}

public sealed class ReviewDecision
{
    [JsonPropertyName("decision_trace_id")]
    public string? DecisionTraceId { get; set; }

    [JsonPropertyName("turn_id")]
    public string? TurnId { get; set; }

    [JsonPropertyName("play_position")]
    public int PlayPosition { get; set; }

    [JsonPropertyName("player_index")]
    public int PlayerIndex { get; set; } = -1;

    [JsonPropertyName("ai_line")]
    public string? AiLine { get; set; }

    public string? Phase { get; set; }
    public string? Path { get; set; }

    [JsonPropertyName("phase_policy")]
    public string? PhasePolicy { get; set; }

    [JsonPropertyName("primary_intent")]
    public string? PrimaryIntent { get; set; }

    [JsonPropertyName("secondary_intent")]
    public string? SecondaryIntent { get; set; }

    [JsonPropertyName("selected_reason")]
    public string? SelectedReason { get; set; }

    [JsonPropertyName("selected_candidate_id")]
    public string? SelectedCandidateId { get; set; }

    [JsonPropertyName("triggered_rules")]
    public List<string> TriggeredRules { get; set; } = new();

    [JsonPropertyName("selected_cards")]
    public List<string> SelectedCards { get; set; } = new();

    [JsonPropertyName("bundle_v30")]
    public JsonElement? BundleV30 { get; set; }

    [JsonPropertyName("bundle")]
    public JsonElement? Bundle { get; set; }
}
