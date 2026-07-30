using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// Game Bar lifecycle and UI-thread shell for the settings widget.
/// </summary>
public sealed partial class SettingsWidgetPage : Page
{
    private readonly CollectorConnection _collector = AppServices.Collector;
    private readonly SettingsAutoSaver _autoSaver =
        new(AppServices.Settings);
    private readonly CoreDispatcher _uiDispatcher;

    private XboxGameBarWidget? _widget;
    private string _deviceSignature = string.Empty;
    private volatile bool _isUnloaded;

    public SettingsWidgetPage()
    {
        ViewModel = new SettingsPageViewModel(
            SettingsDefaults.Create(),
            snapshot: null);
        InitializeComponent();
        _uiDispatcher = Dispatcher;
        SubscribeViewModel();
        _collector.SnapshotReceived += Collector_SnapshotReceived;
        _collector.StatusChanged += Collector_StatusChanged;
        Unloaded += SettingsWidgetPage_Unloaded;
    }

    public SettingsPageViewModel ViewModel { get; private set; }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _widget = e.Parameter as XboxGameBarWidget;
        if (_widget is not null)
        {
            // Keep synchronized with the settings extension values in
            // Package.appxmanifest and FrontendConstants.
            _widget.MinWindowSize = FrontendConstants.SettingsMinimumSize;
            RequestedTheme = _widget.RequestedTheme;
            _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
        }

        WidgetSettings settings = await AppServices.Settings.LoadAsync();
        await DispatchAsync(() =>
        {
            ReplaceViewModel(settings, _collector.LatestSnapshot);
            RefreshStatus();
        });
    }

    private async void Collector_SnapshotReceived(TelemetrySnapshot snapshot)
    {
        await DispatchAsync(() =>
        {
            string signature = CreateDeviceSignature(snapshot);
            if (!string.Equals(
                    signature,
                    _deviceSignature,
                    StringComparison.Ordinal))
            {
                WidgetSettings current = ViewModel.ToSettings();
                ReplaceViewModel(current, snapshot);
            }

            RefreshStatus();
        });
    }

    private async void Collector_StatusChanged(CollectorConnectionStatus status)
    {
        await DispatchAsync(RefreshStatus);
    }

    private void ViewModel_Changed(object? sender, EventArgs e) =>
        _autoSaver.Schedule(ViewModel.ToSettings());

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        ReplaceViewModel(
            SettingsDefaults.Create(),
            _collector.LatestSnapshot);
        _autoSaver.Schedule(ViewModel.ToSettings());
        RefreshStatus();
    }

    private void MetricCategoryButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (sender is not Control target ||
            target.DataContext is not MetricCategoryViewModel category)
        {
            return;
        }

        MetricEditor.Open(category, target);
    }

    private void MetricEditor_Opened(object sender, EventArgs e)
    {
        SettingsScrollViewer.IsEnabled = false;
        SettingsScrollViewer.VerticalScrollBarVisibility =
            ScrollBarVisibility.Hidden;
    }

    private void MetricEditor_Closed(object sender, EventArgs e)
    {
        SettingsScrollViewer.IsEnabled = true;
        SettingsScrollViewer.VerticalScrollBarVisibility =
            ScrollBarVisibility.Auto;
    }

    private void ReplaceViewModel(
        WidgetSettings settings,
        TelemetrySnapshot? snapshot)
    {
        MetricEditor.CloseImmediately();
        ViewModel.Changed -= ViewModel_Changed;
        ViewModel = new SettingsPageViewModel(settings, snapshot);
        _deviceSignature = CreateDeviceSignature(snapshot);
        SubscribeViewModel();
        Bindings.Update();
    }

    private void SubscribeViewModel() =>
        ViewModel.Changed += ViewModel_Changed;

    private void RefreshStatus() =>
        ViewModel.Status.Update(
            _collector.Status,
            _collector.LatestSnapshot);

    private async void Widget_RequestedThemeChanged(
        XboxGameBarWidget sender,
        object args)
    {
        await DispatchAsync(() => RequestedTheme = sender.RequestedTheme);
    }

    private async void SettingsWidgetPage_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _isUnloaded = true;
        MetricEditor.CloseImmediately();
        _collector.SnapshotReceived -= Collector_SnapshotReceived;
        _collector.StatusChanged -= Collector_StatusChanged;
        ViewModel.Changed -= ViewModel_Changed;
        _widget?.RequestedThemeChanged -= Widget_RequestedThemeChanged;

        await _autoSaver.FlushAsync();
        _autoSaver.Dispose();
    }

    private async Task DispatchAsync(Action action)
    {
        if (_isUnloaded)
        {
            return;
        }

        if (_uiDispatcher.HasThreadAccess)
        {
            action();
        }
        else
        {
            try
            {
                await _uiDispatcher.RunAsync(
                    CoreDispatcherPriority.Normal,
                    () =>
                    {
                        if (!_isUnloaded)
                        {
                            action();
                        }
                    });
            }
            catch (COMException) when (_isUnloaded)
            {
                // A queued collector callback can race with widget teardown.
            }
            catch (ObjectDisposedException) when (_isUnloaded)
            {
                // The dispatcher is no longer usable after widget teardown.
            }
        }
    }

    private static string CreateDeviceSignature(
        TelemetrySnapshot? snapshot) => string.Join(
            "|",
            (snapshot?.Readings ?? [])
                .Where(reading =>
                    MetricRegistry.TryGet(
                        reading.MetricId,
                        out MetricDefinition definition) &&
                    definition.Scope == MetricScope.PerDevice &&
                    !string.IsNullOrWhiteSpace(reading.DeviceId))
                .Select(reading =>
                    $"{reading.MetricId}\u001F{reading.DeviceId}\u001F" +
                    reading.DeviceName)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
}
