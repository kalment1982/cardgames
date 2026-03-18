using System.Text.Json.Serialization;

namespace PpoEngineHost;

// ─── Base Request ───

public class BaseRequest
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }
}

public class ResetRequest : BaseRequest
{
    [JsonPropertyName("seed")]
    public int Seed { get; set; }

    [JsonPropertyName("ppo_seats")]
    public int[] PpoSeats { get; set; } = Array.Empty<int>();

    [JsonPropertyName("rule_ai_seats")]
    public int[] RuleAiSeats { get; set; } = Array.Empty<int>();
}

public class StepRequest : BaseRequest
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;

    [JsonPropertyName("action_slot")]
    public int ActionSlot { get; set; }
}

public class GetStateSnapshotRequest : BaseRequest
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;
}

public class GetLegalActionsRequest : BaseRequest
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;
}

public class CloseRequest : BaseRequest
{
    [JsonPropertyName("env_id")]
    public string? EnvId { get; set; }

    [JsonPropertyName("scope")]
    public string? Scope { get; set; }
}

// ─── Responses ───

public class BaseResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("request_id")]
    public string? RequestId { get; set; }

    [JsonPropertyName("error_code")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorCode { get; set; }

    [JsonPropertyName("error_message")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ErrorMessage { get; set; }
}

public class ResetResponse : BaseResponse
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("current_player")]
    public int CurrentPlayer { get; set; }

    [JsonPropertyName("legal_actions")]
    public object[] LegalActions { get; set; } = Array.Empty<object>();

    [JsonPropertyName("state_snapshot")]
    public object StateSnapshot { get; set; } = new { };
}

public class StepResponse : BaseResponse
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;

    [JsonPropertyName("done")]
    public bool Done { get; set; }

    [JsonPropertyName("current_player")]
    public int CurrentPlayer { get; set; }

    [JsonPropertyName("reward")]
    public double Reward { get; set; }

    [JsonPropertyName("legal_actions")]
    public object[] LegalActions { get; set; } = Array.Empty<object>();

    [JsonPropertyName("state_snapshot")]
    public object StateSnapshot { get; set; } = new { };

    [JsonPropertyName("terminal_result")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TerminalResult? TerminalResult { get; set; }
}

public class TerminalResult
{
    [JsonPropertyName("winner_team")]
    public string WinnerTeam { get; set; } = string.Empty;

    [JsonPropertyName("my_team_won")]
    public bool MyTeamWon { get; set; }

    [JsonPropertyName("my_team_final_score")]
    public int MyTeamFinalScore { get; set; }

    [JsonPropertyName("my_team_level_gain")]
    public int MyTeamLevelGain { get; set; }

    [JsonPropertyName("defender_score")]
    public int DefenderScore { get; set; }

    [JsonPropertyName("next_dealer")]
    public int NextDealer { get; set; }
}

public class GetStateSnapshotResponse : BaseResponse
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;

    [JsonPropertyName("state_snapshot")]
    public object StateSnapshot { get; set; } = new { };
}

public class GetLegalActionsResponse : BaseResponse
{
    [JsonPropertyName("env_id")]
    public string EnvId { get; set; } = string.Empty;

    [JsonPropertyName("legal_actions")]
    public object[] LegalActions { get; set; } = Array.Empty<object>();
}

public class CloseResponse : BaseResponse
{
}

// ─── Error codes ───

public static class ErrorCodes
{
    public const string EnvNotFound = "ENV_NOT_FOUND";
    public const string InvalidRequest = "INVALID_REQUEST";
    public const string InvalidActionSlot = "INVALID_ACTION_SLOT";
    public const string ActionSlotNotLegal = "ACTION_SLOT_NOT_LEGAL";
    public const string PhaseNotPlayTricks = "PHASE_NOT_PLAY_TRICKS";
    public const string EngineInternalError = "ENGINE_INTERNAL_ERROR";
    public const string ActionSpaceOverflow = "ACTION_SPACE_OVERFLOW";
}
