using System;
using Microsoft.Gaming.XboxGameBar;
using SensorHUD.Infrastructure;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SensorHUD.Widgets.Settings;
using SensorHUD.Widgets.Telemetry;

namespace SensorHUD;

/// <summary>
/// Routes Xbox Game Bar protocol activation to the display or settings widget.
/// There is intentionally no normal desktop application window.
/// </summary>
public sealed partial class App : Application
{
    // The SDK requires the app to retain this object for the entire CoreWindow
    // lifetime. Releasing it early disconnects the widget from Game Bar.
    private XboxGameBarWidget? _activeWidget;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnActivated(IActivatedEventArgs args)
    {
        XboxGameBarWidgetActivatedEventArgs? widgetArgs = GetWidgetArguments(args);
        if (widgetArgs is null || !widgetArgs.IsLaunchActivation)
        {
            return;
        }

        Frame frame = new();
        frame.NavigationFailed += OnNavigationFailed;
        Window.Current.Content = frame;

        _activeWidget = new XboxGameBarWidget(
            widgetArgs,
            Window.Current.CoreWindow,
            frame);
        frame.RequestedTheme = _activeWidget.RequestedTheme;

        switch (widgetArgs.AppExtensionId)
        {
            case FrontendConstants.TelemetryWidgetId:
                frame.Navigate(typeof(TelemetryWidgetPage), _activeWidget);
                break;

            case FrontendConstants.SettingsWidgetId:
                frame.Navigate(typeof(SettingsWidgetPage), _activeWidget);
                break;

            default:
                // An unknown ID indicates a manifest/code mismatch. Do not
                // create an unrelated external window.
                _activeWidget = null;
                return;
        }

        Window.Current.Closed += CurrentWindow_Closed;
        Window.Current.Activate();
    }

    /// <summary>
    /// Normal launch is deliberately ignored. Game Bar is SensorHUD's only
    /// user-facing entry point and the manifest removes the Start menu entry.
    /// </summary>
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
    }

    private static XboxGameBarWidgetActivatedEventArgs? GetWidgetArguments(IActivatedEventArgs args)
    {
        if (args.Kind != ActivationKind.Protocol ||
            args is not IProtocolActivatedEventArgs protocolArgs ||
            !protocolArgs.Uri.Scheme.Equals("ms-gamebarwidget", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return args as XboxGameBarWidgetActivatedEventArgs;
    }

    private void CurrentWindow_Closed(object sender, Windows.UI.Core.CoreWindowEventArgs e)
    {
        Window.Current.Closed -= CurrentWindow_Closed;
        _activeWidget = null;
    }

    private static void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
    {
        throw new InvalidOperationException($"Failed to load '{e.SourcePageType.FullName}'.");
    }
}
