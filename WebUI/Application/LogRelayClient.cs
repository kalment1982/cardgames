using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using TractorGame.Core.Logging;

namespace WebUI.Application;

public sealed class LogRelayClient
{
    private readonly HttpClient _http;

    public LogRelayClient(HttpClient http)
    {
        _http = http;
    }

    public async Task TryPostAsync(LogEntry entry)
    {
        try
        {
            using var response = await _http.PostAsJsonAsync("api/log-entry", entry);
            _ = response.IsSuccessStatusCode;
        }
        catch
        {
            // Ignore relay failures in UI flow.
        }
    }
}

