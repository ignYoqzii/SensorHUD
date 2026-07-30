using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Gaming.XboxGameBar;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;
using SensorHUD.Infrastructure;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SensorHUD.Widgets.Telemetry;

/// <summary>
/// Game Bar lifecycle shell for the pin-friendly telemetry widget.
/// Presentation and XAML construction are delegated to focused collaborators.
/// </summary>
public sealed partial class TelemetryWidgetPage : Page
{
    private static readonly Color LightBackground =
        Color.FromArgb(255, 245, 245, 245);
    private static readonly Color DarkBackground =
        Color.FromArgb(255, 18, 18, 18);

    private readonly CollectorConnection _collector = AppServices.Collector;
    private readonly TelemetryRenderer _renderer;
    private readonly CoreDispatcher _uiDispatcher;

    private XboxGameBarWidget? _widget;
    private CoreCursor? _foregroundCursor;
    private WidgetSettings _settings = SettingsDefaults.Create();
    private TelemetrySnapshot? _snapshot;
    private volatile bool _isUnloaded;

    public TelemetryWidgetPage()
    {
        InitializeComponent();
        _uiDispatcher = Dispatcher;
        _renderer = new TelemetryRenderer(VerticalItems, HorizontalText);
        _collector.SnapshotReceived += Collector_SnapshotReceived;
        _collector.StatusChanged += Collector_StatusChanged;
        AppServices.SettingsPreviewed += AppServices_SettingsPreviewed;
        Unloaded += TelemetryWidgetPage_Unloaded;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _widget = e.Parameter as XboxGameBarWidget;
        if (_widget is not null)
        {
            _foregroundCursor = Window.Current.CoreWindow.PointerCursor ??
                new CoreCursor(CoreCursorType.Arrow, 0);
            _widget.SettingsClicked += Widget_SettingsClicked;
            _widget.RequestedOpacityChanged += Widget_RequestedOpacityChanged;
            _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
            _widget.GameBarDisplayModeChanged +=
                Widget_GameBarDisplayModeChanged;
            RequestedTheme = _widget.RequestedTheme;
            UpdatePinnedInputBehavior();
        }

        WidgetSettings settings = await AppServices.Settings.LoadAsync();
        await DispatchAsync(() =>
        {
            _settings = settings;
            _snapshot = _collector.LatestSnapshot;
            ApplyAppearance();
            Render();
        });

        if (!_isUnloaded)
        {
            await _collector.StartAsync();
        }
    }

    private async void Collector_SnapshotReceived(TelemetrySnapshot snapshot)
    {
        await DispatchAsync(() =>
        {
            _snapshot = snapshot;
            Render();
        });
    }

    private async void Collector_StatusChanged(CollectorConnectionStatus status)
    {
        await DispatchAsync(Render);
    }

    private async void AppServices_SettingsPreviewed(
        object? sender,
        WidgetSettings settings)
    {
        await DispatchAsync(() =>
        {
            _settings = settings;
            ApplyAppearance();
            Render();
        });
    }

    private void Render()
    {
        TelemetryDisplayModel model = TelemetryPresenter.Create(
            _settings,
            _snapshot,
            _collector.Status);
        _renderer.Render(model, _settings);
        StatusText.Text = model.StatusText;
        StatusText.Visibility = model.StatusText is null
            ? Visibility.Collapsed
            : Visibility.Visible;

        bool hasData = model.StatusText is null;
        if (!hasData)
        {
            HorizontalText.Visibility = Visibility.Collapsed;
            VerticalItems.Visibility = Visibility.Collapsed;
        }
    }

    private void ApplyAppearance()
    {
        double gameBarOpacity = _widget?.RequestedOpacity ?? 1;
        if (gameBarOpacity > 1)
        {
            gameBarOpacity /= FrontendConstants.PercentageScale;
        }

        Color backgroundColor = RequestedTheme == ElementTheme.Light
            ? LightBackground
            : DarkBackground;
        double opacity = Math.Clamp(
            _settings.Appearance.BackgroundOpacity * gameBarOpacity,
            SettingsDefaults.MinimumBackgroundOpacity,
            SettingsDefaults.MaximumBackgroundOpacity);
        if (BackgroundPanel.Background is SolidColorBrush brush)
        {
            brush.Color = backgroundColor;
            brush.Opacity = opacity;
            return;
        }

        BackgroundPanel.Background = new SolidColorBrush(backgroundColor)
        {
            Opacity = opacity,
        };
    }

    private async void Widget_SettingsClicked(
        XboxGameBarWidget sender,
        object args) => await sender.ActivateSettingsAsync();

    private async void Widget_RequestedOpacityChanged(
        XboxGameBarWidget sender,
        object args) => await DispatchAsync(ApplyAppearance);

    private async void Widget_RequestedThemeChanged(
        XboxGameBarWidget sender,
        object args)
    {
        await DispatchAsync(() =>
        {
            RequestedTheme = sender.RequestedTheme;
            ApplyAppearance();
        });
    }

    private async void Widget_GameBarDisplayModeChanged(
        XboxGameBarWidget sender,
        object args) => await DispatchAsync(UpdatePinnedInputBehavior);

    private void UpdatePinnedInputBehavior()
    {
        // Pinned-only overlays must not capture the game's pointer or make its
        // hidden cursor reappear when it crosses the widget.
        bool pinnedOnly = _widget?.GameBarDisplayMode ==
            XboxGameBarDisplayMode.PinnedOnly;
        IsHitTestVisible = !pinnedOnly;
        TrySetPointerCursor(pinnedOnly ? null : _foregroundCursor);
    }

    private async void TelemetryWidgetPage_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        _isUnloaded = true;
        _collector.SnapshotReceived -= Collector_SnapshotReceived;
        _collector.StatusChanged -= Collector_StatusChanged;
        AppServices.SettingsPreviewed -= AppServices_SettingsPreviewed;

        if (_widget is not null)
        {
            _widget.SettingsClicked -= Widget_SettingsClicked;
            _widget.RequestedOpacityChanged -= Widget_RequestedOpacityChanged;
            _widget.RequestedThemeChanged -= Widget_RequestedThemeChanged;
            _widget.GameBarDisplayModeChanged -=
                Widget_GameBarDisplayModeChanged;
        }

        TrySetPointerCursor(_foregroundCursor);
        await _collector.StopAsync();
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
                // Game Bar can destroy the page while a callback is queued.
            }
            catch (ObjectDisposedException) when (_isUnloaded)
            {
                // The dispatcher is no longer usable after widget teardown.
            }
        }
    }

    private static void TrySetPointerCursor(CoreCursor? cursor)
    {
        try
        {
            Window.Current.CoreWindow.PointerCursor = cursor;
        }
        catch (COMException)
        {
            // The CoreWindow may disappear while Game Bar changes widget mode.
        }
        catch (ObjectDisposedException)
        {
            // No cursor restoration is needed after the window is destroyed.
        }
    }
}
