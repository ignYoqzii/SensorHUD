using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using SensorHUD.Core.Updates;
using SensorHUD.Infrastructure;
using Windows.ApplicationModel;
using Windows.Foundation;
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
    private CancellationTokenSource? _updateCheckCancellation;
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

    private async void CheckForUpdatesButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_updateCheckCancellation is not null)
        {
            return;
        }

        CancellationTokenSource cancellation = new();
        _updateCheckCancellation = cancellation;
        CheckForUpdatesButton.IsEnabled = false;
        ShowUpdateStatus("Checking for updates…");
        bool updateAvailable = false;

        try
        {
            PackageVersion packageVersion = Package.Current.Id.Version;
            Version installedVersion = new(
                packageVersion.Major,
                packageVersion.Minor,
                packageVersion.Build,
                packageVersion.Revision);
            UpdateCheckResult result =
                await AppServices.Updates.CheckAsync(
                    installedVersion,
                    cancellation.Token);
            if (_isUnloaded)
            {
                return;
            }

            if (!result.IsUpdateAvailable)
            {
                await DispatchAsync(
                    () => ShowUpdateStatus("SensorHUD is up to date."));
                return;
            }

            updateAvailable = true;
            IAsyncOperation<bool>? launchOperation = null;
            await DispatchAsync(() =>
            {
                ShowUpdateStatus(
                    $"Version {FormatReleaseVersion(
                        result.LatestVersion)} is available. " +
                    "Opening the download page…");
                launchOperation = _widget?.LaunchUriAsync(
                    GitHubUpdateChecker.DownloadPageUri);
            });
            if (launchOperation is null)
            {
                await DispatchAsync(() => ShowUpdateStatus(
                    "An update is available, but Game Bar could not open " +
                    "the download page."));
                return;
            }

            bool launched = await launchOperation;
            await DispatchAsync(() => ShowUpdateStatus(
                launched
                    ? "The download page was opened in your browser."
                    : "An update is available, but Windows could not open " +
                      "the download page."));
        }
        catch (OperationCanceledException) when (_isUnloaded)
        {
            // Widget teardown cancels the optional network request.
        }
        catch (Exception)
        {
            await DispatchAsync(() => ShowUpdateStatus(
                updateAvailable
                    ? "An update is available, but Game Bar could not open " +
                      "the download page."
                    : "Could not check for updates. Check your Internet " +
                      "connection and try again."));
        }
        finally
        {
            cancellation.Dispose();
            if (ReferenceEquals(_updateCheckCancellation, cancellation))
            {
                _updateCheckCancellation = null;
            }

            await DispatchAsync(
                () => CheckForUpdatesButton.IsEnabled = true);
        }
    }

    private void ShowUpdateStatus(string message)
    {
        if (_isUnloaded)
        {
            return;
        }

        UpdateStatusText.Text = message;
        UpdateStatusText.Visibility = Visibility.Visible;
    }

    private static string FormatReleaseVersion(Version version) =>
        version.Revision == 0
            ? version.ToString(3)
            : version.ToString(4);

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
        _updateCheckCancellation?.Cancel();
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
            (snapshot?.Instances ?? [])
                .Where(instance =>
                    MetricRegistry.TryGet(
                        instance.MetricId,
                        out MetricDefinition definition) &&
                    definition.Scope == MetricScope.PerDevice &&
                    !string.IsNullOrWhiteSpace(instance.DeviceId))
                .Select(instance =>
                    $"{instance.MetricId}\u001F{instance.DeviceId}\u001F" +
                    instance.DeviceName)
                .Distinct(StringComparer.Ordinal)
                .Order(StringComparer.Ordinal));
}
