using System;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Core.Settings;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Publishes edits immediately, debounces atomic persistence, and flushes the
/// most recent model when the settings widget closes.
/// </summary>
internal sealed class SettingsAutoSaver : IDisposable
{
    private readonly WidgetSettingsStore _store;
    private readonly object _sync = new();

    private CancellationTokenSource? _delayCancellation;
    private WidgetSettings? _pendingSettings;
    private Task _pendingSave = Task.CompletedTask;

    public SettingsAutoSaver(WidgetSettingsStore store)
    {
        _store = store;
    }

    public void Schedule(WidgetSettings settings)
    {
        WidgetSettings normalized = SettingsValidator.Normalize(settings);
        AppServices.PreviewSettings(normalized);

        lock (_sync)
        {
            _pendingSettings = normalized;
            _delayCancellation?.Cancel();
            _delayCancellation?.Dispose();
            _delayCancellation = new CancellationTokenSource();
            _pendingSave = SaveAfterDelayAsync(
                normalized,
                _delayCancellation.Token);
        }
    }

    public async Task FlushAsync()
    {
        WidgetSettings? pending;
        Task pendingSave;
        lock (_sync)
        {
            _delayCancellation?.Cancel();
            pending = _pendingSettings;
            _pendingSettings = null;
            pendingSave = _pendingSave;
        }

        try
        {
            await pendingSave;
        }
        catch
        {
            // A final explicit save below remains the authoritative attempt.
        }

        if (pending is not null)
        {
            await _store.SaveAsync(pending);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            _delayCancellation?.Cancel();
            _delayCancellation?.Dispose();
            _delayCancellation = null;
        }
    }

    private async Task SaveAfterDelayAsync(
        WidgetSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(
                SettingsDefaults.SaveDebounce,
                cancellationToken);
            await _store.SaveAsync(settings);
            lock (_sync)
            {
                if (ReferenceEquals(_pendingSettings, settings))
                {
                    _pendingSettings = null;
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
            when (exception is System.IO.IOException or
                UnauthorizedAccessException)
        {
            // A transient persistence failure must not crash Game Bar. The
            // unload flush gets one final opportunity to save the latest edit.
        }
    }
}
