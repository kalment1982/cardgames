using System;
using System.Threading;
using System.Threading.Tasks;

namespace WebUI.Application;

public sealed class UiMessageService
{
    private CancellationTokenSource? _clearCts;

    public string CurrentMessage { get; private set; } = string.Empty;
    public event Action? Changed;

    public void Show(string message, int autoClearMs = 2000)
    {
        CurrentMessage = message ?? string.Empty;
        Changed?.Invoke();

        _clearCts?.Cancel();
        _clearCts?.Dispose();
        _clearCts = new CancellationTokenSource();
        _ = ClearLaterAsync(CurrentMessage, autoClearMs, _clearCts.Token);
    }

    public void Clear()
    {
        _clearCts?.Cancel();
        _clearCts?.Dispose();
        _clearCts = null;
        CurrentMessage = string.Empty;
        Changed?.Invoke();
    }

    private async Task ClearLaterAsync(string expectedMessage, int delayMs, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delayMs, ct);
            if (!ct.IsCancellationRequested && CurrentMessage == expectedMessage)
            {
                CurrentMessage = string.Empty;
                Changed?.Invoke();
            }
        }
        catch (TaskCanceledException)
        {
            // Ignore cancellation.
        }
    }
}

