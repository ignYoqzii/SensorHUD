using System;
using System.Threading;
using System.Threading.Tasks;
using SensorHUD.Core.Settings;
using SensorHUD.Infrastructure;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Publishes validated edits immediately, orders debounced atomic writes, and
/// flushes the most recent unsaved model when the settings widget closes.
/// </summary>
internal sealed class SettingsAutoSaver(WidgetSettingsStore store) : IDisposable
{
    private readonly Lock _sync = new();

    private CancellationTokenSource? _delayCancellation;
    private WidgetSettings? _pendingSettings;
    private Task _pendingSave = Task.CompletedTask;

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
            Task previousSave = _pendingSave;
            _pendingSave = SaveAfterDelayAsync(
                previousSave,
                normalized,
                _delayCancellation.Token);
        }
    }

    public async Task FlushAsync()
    {
        Task pendingSave;
        lock (_sync)
        {
            _delayCancellation?.Cancel();
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

        WidgetSettings? pending;
        lock (_sync)
        {
            pending = _pendingSettings;
            _pendingSettings = null;
        }

        if (pending is not null)
        {
            try
            {
                await store.SaveAsync(pending);
            }
            catch (Exception exception)
                when (exception is System.IO.IOException or
                    UnauthorizedAccessException)
            {
                // A storage failure during teardown must not crash Game Bar.
            }
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
        Task previousSave,
        WidgetSettings settings,
        CancellationToken cancellationToken)
    {
        try
        {
            try
            {
                await previousSave;
            }
            catch
            {
                // A later valid edit must not be blocked by an earlier save.
            }

            await Task.Delay(
                SettingsDefaults.SaveDebounce,
                cancellationToken);
            await store.SaveAsync(settings);
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
