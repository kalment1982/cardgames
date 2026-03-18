namespace PpoEngineHost;

public class EnvironmentManager
{
    private readonly Dictionary<string, EnvironmentSession> _sessions = new();
    private int _counter;

    public (string envId, EnvironmentSession session) CreateEnvironment(
        int seed, int[] ppoSeats, int[] ruleAiSeats)
    {
        _counter++;
        var envId = $"env_{_counter:D4}";
        var session = new EnvironmentSession(seed, ppoSeats, ruleAiSeats);
        _sessions[envId] = session;
        return (envId, session);
    }

    public EnvironmentSession? GetSession(string envId)
    {
        return _sessions.TryGetValue(envId, out var session) ? session : null;
    }

    public bool CloseSession(string envId)
    {
        return _sessions.Remove(envId);
    }

    public void CloseAll()
    {
        _sessions.Clear();
    }
}
