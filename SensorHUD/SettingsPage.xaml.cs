using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensorHUD.Models;
using SensorHUD.Services;
using SensorHUD.Shared;
using SensorHUD.ViewModels;
using Windows.ApplicationModel;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace SensorHUD;

/// <summary>
/// Game Bar settings widget. Editable state is data-bound to a small view
/// model, previewed immediately, and persisted after a short debounce.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private readonly CollectorClient _collectorClient =
        CollectorClient.Shared;
    private readonly DispatcherTimer _saveTimer = new()
    {
        Interval = CollectorProtocol.SettingsSaveDelay,
    };

    private IReadOnlyList<MetricDefinition> _definitions = [];
    private string _definitionSignature = string.Empty;
    private bool _saving;
    private bool _saveRequested;

    public SettingsPage()
    {
        InitializeComponent();
        _saveTimer.Tick += SaveTimer_Tick;
        _collectorClient.SnapshotReceived +=
            CollectorClient_SnapshotReceived;
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    /// <summary>
    /// Strongly typed source for the page-level compiled bindings.
    /// </summary>
    public SettingsPageViewModel? ViewModel { get; private set; }

    private async void SettingsPage_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= SettingsPage_Loaded;

        TelemetrySettings settings = await SettingsService.LoadAsync();
        TelemetrySnapshot? snapshot = _collectorClient.LatestSnapshot;
        IReadOnlyList<MetricDefinition> definitions =
            MetricCatalog.CreateForSnapshot(snapshot);

        await RunOnUiThreadAsync(() =>
        {
            SetViewModel(settings, definitions);
            UpdateDiagnostics(snapshot);
            UpdateVersion();
        });
    }

    /// <summary>
    /// Replaces the complete editable state without manually touching any
    /// controls. Initial compiled-binding reads do not trigger a save because
    /// the change event is attached only after the bindings are refreshed.
    /// </summary>
    private void SetViewModel(
        TelemetrySettings settings,
        IReadOnlyList<MetricDefinition> definitions)
    {
        if (ViewModel is not null)
        {
            ViewModel.SettingsChanged -=
                ViewModel_SettingsChanged;
        }

        _definitions = definitions;
        _definitionSignature = CreateDefinitionSignature(definitions);

        SettingsPageViewModel viewModel =
            new(settings, definitions);
        ViewModel = viewModel;
        Bindings.Update();
        viewModel.SettingsChanged += ViewModel_SettingsChanged;
    }

    private void ViewModel_SettingsChanged(
        object? sender,
        EventArgs e)
    {
        QueueAutoSave();
    }

    private void QueueAutoSave()
    {
        if (ViewModel is null)
        {
            return;
        }

        // Preview is intentionally synchronous so the pinned widget changes
        // in the same UI interaction. Disk writes remain debounced.
        SettingsService.Preview(ViewModel.ToSettings());

        _saveRequested = true;
        if (!_saving)
        {
            _saveTimer.Stop();
            _saveTimer.Start();
        }
    }

    private async void SaveTimer_Tick(object? sender, object e)
    {
        _saveTimer.Stop();
        if (_saving || !_saveRequested || ViewModel is null)
        {
            return;
        }

        _saving = true;
        _saveRequested = false;
        TelemetrySettings settings = ViewModel.ToSettings();

        try
        {
            await SettingsService.SaveAsync(settings);
        }
        catch
        {
            // The next edit retries the save. Atomic replacement leaves the
            // previous settings file intact after a transient write failure.
        }

        await RunOnUiThreadAsync(() =>
        {
            _saving = false;
            if (_saveRequested)
            {
                _saveTimer.Start();
            }
        });
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        SetViewModel(
            SettingsService.CreateDefaults(),
            _definitions);
        QueueAutoSave();
    }

    private async void SettingsPage_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTimer_Tick;
        _collectorClient.SnapshotReceived -=
            CollectorClient_SnapshotReceived;
        Unloaded -= SettingsPage_Unloaded;

        if (ViewModel is not null)
        {
            ViewModel.SettingsChanged -=
                ViewModel_SettingsChanged;
        }

        if (_saveRequested && ViewModel is not null)
        {
            try
            {
                await SettingsService.SaveAsync(
                    ViewModel.ToSettings());
            }
            catch
            {
                // The previous atomic settings file remains valid.
            }
        }
    }

    private async void CollectorClient_SnapshotReceived(
        TelemetrySnapshot snapshot)
    {
        try
        {
            await RunOnUiThreadAsync(() =>
            {
                UpdateDiagnostics(snapshot);
                RefreshMetricCatalog(snapshot);
            });
        }
        catch
        {
            // The settings widget may be closing while a final snapshot is
            // being dispatched from the shared pipe client.
        }
    }

    /// <summary>
    /// Rebinds metric cards only when the collector exposes a different set
    /// of IDs, such as when a GPU first appears. Normal one-second snapshots
    /// therefore cause no settings-tree allocation or visual disturbance.
    /// </summary>
    private void RefreshMetricCatalog(TelemetrySnapshot snapshot)
    {
        if (ViewModel is null)
        {
            return;
        }

        IReadOnlyList<MetricDefinition> definitions =
            MetricCatalog.CreateForSnapshot(snapshot);
        string signature = CreateDefinitionSignature(definitions);
        if (signature == _definitionSignature)
        {
            return;
        }

        SetViewModel(ViewModel.ToSettings(), definitions);
    }

    private static string CreateDefinitionSignature(
        IReadOnlyList<MetricDefinition> definitions)
    {
        return string.Join(
            "|",
            definitions.Select(definition => definition.Id));
    }

    private void UpdateDiagnostics(TelemetrySnapshot? snapshot)
    {
        bool running = string.Equals(
            snapshot?.CollectorStatus,
            CollectorStates.Running,
            StringComparison.Ordinal);
        CollectorDiagnostics diagnostics =
            snapshot?.Diagnostics ?? new CollectorDiagnostics();

        CollectorStatusText.Text =
            snapshot?.CollectorStatus ?? CollectorStates.NoData;
        Color indicatorColor = running
            ? Color.FromArgb(255, 57, 196, 115)
            : string.Equals(
                snapshot?.CollectorStatus,
                CollectorStates.Starting,
                StringComparison.Ordinal)
                ? Color.FromArgb(255, 245, 165, 36)
                : Color.FromArgb(255, 229, 72, 77);
        CollectorStatusIndicator.Fill = new SolidColorBrush(
            indicatorColor);
        LastSnapshotText.Text = running && snapshot is not null
            ? snapshot.CapturedAtUtc.ToLocalTime().ToString("T")
            : "Waiting";
        AdministratorStatusText.Text = running
            ? diagnostics.IsAdministrator ? "Elevated" : "Not elevated"
            : "Unknown";
        PawnIoStatusText.Text = running
            ? diagnostics.PawnIoStatus
            : "Unknown";
        FrameMetricsStatusText.Text = running
            ? diagnostics.FrameMetricsStatus
            : "Waiting";
        CpuStatusText.Text = running
            ? diagnostics.CpuName
            : "Not detected";
        GpuStatusText.Text = running && diagnostics.GpuNames.Count > 0
            ? string.Join(Environment.NewLine, diagnostics.GpuNames)
            : "Not detected";

        bool hasError =
            !string.IsNullOrWhiteSpace(diagnostics.LastError);
        LastErrorPanel.Visibility =
            hasError ? Visibility.Visible : Visibility.Collapsed;
        LastErrorText.Text = diagnostics.LastError ?? string.Empty;
    }

    private void UpdateVersion()
    {
        try
        {
            PackageVersion version = Package.Current.Id.Version;
            VersionText.Text =
                $"v{version.Major}.{version.Minor}.{version.Build}";
        }
        catch
        {
            VersionText.Text = "Development build";
        }
    }

    private async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        await Dispatcher.RunAsync(
            CoreDispatcherPriority.Normal,
            () => action());
    }
}
