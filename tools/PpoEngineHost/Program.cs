using System.Text.Json;
using System.Text.Json.Serialization;
using PpoEngineHost;

// Capture the real stdout for JSON protocol, then redirect Console.Out to stderr
// so that any Console.WriteLine from library code (e.g. ChampionLoader) doesn't
// pollute the protocol stream.
var protocolOut = Console.Out;
Console.SetOut(Console.Error);

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    PropertyNameCaseInsensitive = true
};

var envManager = new EnvironmentManager();
var dispatcher = new RequestDispatcher(envManager, jsonOptions, protocolOut);

Log("PpoEngineHost started, waiting for requests on stdin...");

string? line;
while ((line = Console.In.ReadLine()) != null)
{
    line = line.Trim();
    if (line.Length == 0) continue;

    string responseJson;
    try
    {
        responseJson = dispatcher.Handle(line);
    }
    catch (Exception ex)
    {
        Log($"Unhandled exception: {ex}");
        var errorResp = new BaseResponse
        {
            Ok = false,
            Type = "error",
            ErrorCode = ErrorCodes.EngineInternalError,
            ErrorMessage = ex.Message
        };
        responseJson = JsonSerializer.Serialize(errorResp, jsonOptions);
    }

    protocolOut.WriteLine(responseJson);
    protocolOut.Flush();
}

Log("stdin closed, exiting.");

static void Log(string message)
{
    Console.Error.WriteLine($"[PpoEngineHost] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} {message}");
    Console.Error.Flush();
}

// ─── Dispatcher ───

class RequestDispatcher
{
    private readonly EnvironmentManager _envManager;
    private readonly JsonSerializerOptions _json;
    private readonly TextWriter _protocolOut;

    public RequestDispatcher(EnvironmentManager envManager, JsonSerializerOptions json, TextWriter protocolOut)
    {
        _envManager = envManager;
        _json = json;
        _protocolOut = protocolOut;
    }

    public string Handle(string line)
    {
        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(line);
        }
        catch (JsonException ex)
        {
            return Error("error", null, ErrorCodes.InvalidRequest, $"Invalid JSON: {ex.Message}");
        }

        var root = doc.RootElement;
        var type = root.TryGetProperty("type", out var typeProp) ? typeProp.GetString() : null;
        var requestId = root.TryGetProperty("request_id", out var ridProp) ? ridProp.GetString() : null;

        if (string.IsNullOrEmpty(type))
            return Error("error", requestId, ErrorCodes.InvalidRequest, "Missing 'type' field.");

        try
        {
            return type switch
            {
                "reset" => HandleReset(line, requestId),
                "step" => HandleStep(line, requestId),
                "get_state_snapshot" => HandleGetStateSnapshot(line, requestId),
                "get_legal_actions" => HandleGetLegalActions(line, requestId),
                "get_teacher_action" => HandleGetTeacherAction(line, requestId),
                "close" => HandleClose(line, requestId),
                _ => Error(type, requestId, ErrorCodes.InvalidRequest, $"Unknown request type: {type}")
            };
        }
        catch (PpoEngineProtocolException ex)
        {
            return Error(type, requestId, ex.ErrorCode, ex.Message);
        }
        catch (Exception ex)
        {
            LogStderr($"Error handling '{type}': {ex}");
            return Error(type, requestId, ErrorCodes.EngineInternalError, ex.Message);
        }
    }

    private string HandleReset(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<ResetRequest>(line, _json);
        if (req == null)
            return Error("reset", requestId, ErrorCodes.InvalidRequest, "Failed to parse reset request.");

        var rid = requestId ?? req.RequestId;

        if (req.PpoSeats.Length == 0 && req.RuleAiSeats.Length == 0)
            return Error("reset", rid, ErrorCodes.InvalidRequest, "Must specify ppo_seats and/or rule_ai_seats.");

        var (envId, session) = _envManager.CreateEnvironment(req.Seed, req.PpoSeats, req.RuleAiSeats);
        var (done, currentPlayer, _) = session.Reset();

        var resp = new ResetResponse
        {
            Ok = true,
            Type = "reset",
            RequestId = rid,
            EnvId = envId,
            Done = done,
            CurrentPlayer = currentPlayer,
            LegalActions = session.ExportLegalActions(currentPlayer),
            StateSnapshot = session.BuildStateSnapshot(currentPlayer)
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string HandleStep(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<StepRequest>(line, _json);
        if (req == null)
            return Error("step", requestId, ErrorCodes.InvalidRequest, "Failed to parse step request.");

        var rid = requestId ?? req.RequestId;

        var session = _envManager.GetSession(req.EnvId);
        if (session == null)
            return Error("step", rid, ErrorCodes.EnvNotFound, $"Environment '{req.EnvId}' not found.");

        if (session.Game.State.Phase != TractorGame.Core.GameFlow.GamePhase.Playing)
            return Error("step", rid, ErrorCodes.PhaseNotPlayTricks, "Game is not in Playing phase.");

        var (done, currentPlayer, terminalResult) = session.Step(req.ActionSlot);

        var resp = new StepResponse
        {
            Ok = true,
            Type = "step",
            RequestId = rid,
            EnvId = req.EnvId,
            Done = done,
            CurrentPlayer = currentPlayer,
            Reward = 0.0,
            LegalActions = session.ExportLegalActions(currentPlayer),
            StateSnapshot = done ? new { } : session.BuildStateSnapshot(currentPlayer),
            TerminalResult = terminalResult
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string HandleGetStateSnapshot(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<GetStateSnapshotRequest>(line, _json);
        if (req == null)
            return Error("get_state_snapshot", requestId, ErrorCodes.InvalidRequest, "Failed to parse request.");

        var rid = requestId ?? req.RequestId;

        var session = _envManager.GetSession(req.EnvId);
        if (session == null)
            return Error("get_state_snapshot", rid, ErrorCodes.EnvNotFound, $"Environment '{req.EnvId}' not found.");

        var resp = new GetStateSnapshotResponse
        {
            Ok = true,
            Type = "get_state_snapshot",
            RequestId = rid,
            EnvId = req.EnvId,
            StateSnapshot = session.BuildStateSnapshot(session.Game.State.CurrentPlayer)
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string HandleGetLegalActions(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<GetLegalActionsRequest>(line, _json);
        if (req == null)
            return Error("get_legal_actions", requestId, ErrorCodes.InvalidRequest, "Failed to parse request.");

        var rid = requestId ?? req.RequestId;

        var session = _envManager.GetSession(req.EnvId);
        if (session == null)
            return Error("get_legal_actions", rid, ErrorCodes.EnvNotFound, $"Environment '{req.EnvId}' not found.");

        var currentPlayer = session.Game.State.CurrentPlayer;
        var resp = new GetLegalActionsResponse
        {
            Ok = true,
            Type = "get_legal_actions",
            RequestId = rid,
            EnvId = req.EnvId,
            LegalActions = session.ExportLegalActions(currentPlayer)
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string HandleGetTeacherAction(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<GetTeacherActionRequest>(line, _json);
        if (req == null)
            return Error("get_teacher_action", requestId, ErrorCodes.InvalidRequest, "Failed to parse request.");

        var rid = requestId ?? req.RequestId;

        var session = _envManager.GetSession(req.EnvId);
        if (session == null)
            return Error("get_teacher_action", rid, ErrorCodes.EnvNotFound, $"Environment '{req.EnvId}' not found.");

        var currentPlayer = session.Game.State.CurrentPlayer;
        var resp = new GetTeacherActionResponse
        {
            Ok = true,
            Type = "get_teacher_action",
            RequestId = rid,
            EnvId = req.EnvId,
            CurrentPlayer = currentPlayer,
            TeacherAction = session.ExportTeacherAction(currentPlayer)
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string HandleClose(string line, string? requestId)
    {
        var req = JsonSerializer.Deserialize<CloseRequest>(line, _json);
        if (req == null)
            return Error("close", requestId, ErrorCodes.InvalidRequest, "Failed to parse close request.");

        var rid = requestId ?? req.RequestId;

        // scope=host means shut down the entire process
        if (req.Scope == "host")
        {
            _envManager.CloseAll();
            var hostResp = new CloseResponse
            {
                Ok = true,
                Type = "close",
                RequestId = rid
            };
            var hostJson = JsonSerializer.Serialize(hostResp, _json);
            _protocolOut.WriteLine(hostJson);
            _protocolOut.Flush();
            LogStderr("Received close with scope=host, exiting.");
            Environment.Exit(0);
        }

        // Close a specific environment
        if (!string.IsNullOrEmpty(req.EnvId))
        {
            if (!_envManager.CloseSession(req.EnvId))
                return Error("close", rid, ErrorCodes.EnvNotFound, $"Environment '{req.EnvId}' not found.");
        }
        else
        {
            _envManager.CloseAll();
        }

        var resp = new CloseResponse
        {
            Ok = true,
            Type = "close",
            RequestId = rid
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private string Error(string type, string? requestId, string errorCode, string errorMessage)
    {
        var resp = new BaseResponse
        {
            Ok = false,
            Type = type,
            RequestId = requestId,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
        return JsonSerializer.Serialize(resp, _json);
    }

    private static void LogStderr(string message)
    {
        Console.Error.WriteLine($"[PpoEngineHost] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff} {message}");
        Console.Error.Flush();
    }
}
