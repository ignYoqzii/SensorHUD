using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using SensorHUD.Formatting;
using SensorHUD.Models;
using SensorHUD.Services;
using SensorHUD.Shared;
using Microsoft.Gaming.XboxGameBar;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace SensorHUD;

/// <summary>
/// The pin-friendly telemetry view hosted by Xbox Game Bar.
/// </summary>
public sealed partial class TelemetryPage : Page
{
    private const double PercentageScale = 100;

    private static readonly Color LightBackground =
        Color.FromArgb(255, 245, 245, 245);
    private static readonly Color DarkBackground =
        Color.FromArgb(255, 18, 18, 18);

    private readonly CollectorClient _collectorClient = CollectorClient.Shared;
    private readonly Dictionary<string, TextBlock> _textByMetricId = [];

    private XboxGameBarWidget? _widget;
    private CoreCursor? _foregroundCursor;
    private TelemetrySettings _settings = SettingsService.CreateDefaults();
    private TelemetrySnapshot _snapshot = new();
    private IReadOnlyList<MetricDefinition> _definitions = [];
    private string _layoutSignature = string.Empty;

    public TelemetryPage()
    {
        InitializeComponent();
        _collectorClient.SnapshotReceived += CollectorClient_SnapshotReceived;
        SettingsService.PreviewChanged += SettingsService_PreviewChanged;
        Unloaded += TelemetryPage_Unloaded;
    }

    protected override async void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _widget = e.Parameter as XboxGameBarWidget;

        if (_widget is not null)
        {
            _foregroundCursor = Window.Current.CoreWindow.PointerCursor
                ?? new CoreCursor(CoreCursorType.Arrow, 0);
            _widget.SettingsClicked += Widget_SettingsClicked;
            _widget.RequestedOpacityChanged += Widget_RequestedOpacityChanged;
            _widget.RequestedThemeChanged += Widget_RequestedThemeChanged;
            _widget.GameBarDisplayModeChanged += Widget_GameBarDisplayModeChanged;
            RequestedTheme = _widget.RequestedTheme;
            UpdatePinnedInputBehavior();
        }

        TelemetrySettings settings = await SettingsService.LoadAsync();
        await RunOnUiThreadAsync(() =>
        {
            _settings = settings;
            TelemetrySnapshot snapshot = _collectorClient.LatestSnapshot ??
                new TelemetrySnapshot
                {
                    CollectorStatus = CollectorStates.Starting,
                };
            IReadOnlyList<MetricDefinition> definitions =
                MetricCatalog.CreateForSnapshot(snapshot);
            ApplyTelemetryState(_settings, definitions, snapshot);
        });

        await _collectorClient.StartAsync();
    }

    private async void CollectorClient_SnapshotReceived(
        TelemetrySnapshot snapshot)
    {
        try
        {
            IReadOnlyList<MetricDefinition> definitions =
                MetricCatalog.CreateForSnapshot(snapshot);
            await RunOnUiThreadAsync(() =>
            {
                ApplyTelemetryState(_settings, definitions, snapshot);
            });
        }
        catch
        {
            // The page may be closing while an in-flight snapshot is being
            // dispatched. The shared client remains available for reconnect.
        }
    }

    private void ApplyTelemetryState(
        TelemetrySettings settings,
        IReadOnlyList<MetricDefinition> definitions,
        TelemetrySnapshot snapshot)
    {
        _settings = settings;
        _definitions = definitions;
        _snapshot = snapshot;
        ApplyAppearance();

        string signature = string.Join("|", definitions.Select(item => item.Id)) +
            "|" + CreateControlSignature();
        if (_layoutSignature != signature)
        {
            _layoutSignature = signature;
            BuildMetricControls();
        }

        UpdateMetricTexts(snapshot);
        UpdateStatus(snapshot);
    }

    private async void SettingsService_PreviewChanged(
        object? sender,
        TelemetrySettings settings)
    {
        // The settings widget publishes every edit before its debounced disk
        // save. Game Bar hosts both definitions in this single app process.
        await RunOnUiThreadAsync(() =>
        {
            ApplyTelemetryState(settings, _definitions, _snapshot);
        });
    }

    private void BuildMetricControls()
    {
        VerticalItems.Items.Clear();
        _textByMetricId.Clear();

        ApplyTextAppearance(HorizontalText);

        // Horizontal mode is rendered as one naturally wrapping line. This
        // avoids the uniform-cell clipping behavior of ItemsWrapGrid.
        if (_settings.Layout == LayoutNames.Horizontal)
        {
            return;
        }

        foreach (MetricDefinition definition in _definitions)
        {
            if (!IsMetricEnabled(definition))
            {
                continue;
            }

            TextBlock text = new();
            ApplyTextAppearance(text);

            Border item = new()
            {
                Child = text,
                Padding = new Thickness(2, 0, 2, 2),
                BorderBrush = new SolidColorBrush(Color.FromArgb(65, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };

            VerticalItems.Items.Add(item);
            _textByMetricId[definition.Id] = text;
        }
    }

    private void UpdateMetricTexts(TelemetrySnapshot snapshot)
    {
        Dictionary<string, TelemetryValue> readingById =
            new(StringComparer.Ordinal);
        foreach (TelemetryValue reading in snapshot.Values)
        {
            // The latest value wins if a faulty provider repeats an ID. This
            // keeps malformed backend data from crashing the frontend.
            readingById[reading.Id] = reading;
        }

        if (_settings.Layout == LayoutNames.Horizontal)
        {
            List<string> errors = [];
            HorizontalText.Inlines.Clear();
            bool firstMetric = true;

            foreach (MetricDefinition definition in _definitions.Where(IsMetricEnabled))
            {
                readingById.TryGetValue(definition.Id, out TelemetryValue? reading);
                MetricPreference? preference = FindPreference(definition.Id);
                if (!firstMetric)
                {
                    HorizontalText.Inlines.Add(new Run
                    {
                        Text = CreateHorizontalSeparator(),
                    });
                }

                AppendMetricParts(
                    HorizontalText,
                    MetricFormatter.FormatParts(
                        definition,
                        reading,
                        preference));
                firstMetric = false;

                if (!string.IsNullOrWhiteSpace(reading?.Error))
                {
                    errors.Add($"{definition.Name}: {reading.Error}");
                }
            }

            ToolTipService.SetToolTip(
                HorizontalText,
                errors.Count == 0 ? null : string.Join(Environment.NewLine, errors));
            return;
        }

        foreach (MetricDefinition definition in _definitions)
        {
            if (!_textByMetricId.TryGetValue(definition.Id, out TextBlock? text))
            {
                continue;
            }

            readingById.TryGetValue(definition.Id, out TelemetryValue? reading);
            MetricPreference? preference = FindPreference(definition.Id);
            text.Inlines.Clear();
            AppendMetricParts(
                text,
                MetricFormatter.FormatParts(
                    definition,
                    reading,
                    preference));

            // An error tooltip gives detail without cluttering a pinned overlay.
            ToolTipService.SetToolTip(text, reading?.Error);
        }
    }

    /// <summary>
    /// Converts formatter roles into lightweight XAML runs. Values are
    /// slightly larger and units are slightly smaller. Both intentionally use
    /// normal weight; the user's weight applies only to surrounding text.
    /// </summary>
    private void AppendMetricParts(
        TextBlock target,
        IReadOnlyList<MetricTextPart> parts)
    {
        foreach (MetricTextPart part in parts)
        {
            Run run = new() { Text = part.Text };
            if (part.Role == MetricTextRole.Value)
            {
                run.FontWeight = FontWeights.Normal;
                run.FontSize = _settings.FontSize * 1.06;
            }
            else if (part.Role == MetricTextRole.Unit)
            {
                run.FontWeight = FontWeights.Normal;
                run.FontSize = Math.Max(8, _settings.FontSize * 0.82);
            }

            target.Inlines.Add(run);
        }
    }

    private bool IsMetricEnabled(MetricDefinition definition)
    {
        MetricPreference? preference = FindPreference(definition.Id);
        return preference?.IsEnabled ?? definition.EnabledByDefault;
    }

    private void ApplyTextAppearance(TextBlock text)
    {
        text.FontFamily = SafeFontFamily(_settings.FontFamily);
        text.FontSize = _settings.FontSize;
        text.FontWeight = ParseFontWeight(_settings.FontWeight);
        text.Foreground = new SolidColorBrush(ParseColor(_settings.FontColor));
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Top;
    }

    private void UpdateStatus(TelemetrySnapshot snapshot)
    {
        bool healthy = snapshot.CollectorStatus.Equals(
            CollectorStates.Running,
            StringComparison.Ordinal);
        StatusText.Visibility = healthy ? Visibility.Collapsed : Visibility.Visible;
        StatusText.Text = snapshot.CollectorStatus;

        // A missing backend is a valid frontend state. Show one clear message
        // instead of filling the widget with misleading N/A metric rows.
        if (!healthy)
        {
            HorizontalText.Visibility = Visibility.Collapsed;
            VerticalItems.Visibility = Visibility.Collapsed;
            return;
        }

        bool horizontal = _settings.Layout == LayoutNames.Horizontal;
        HorizontalText.Visibility = horizontal ? Visibility.Visible : Visibility.Collapsed;
        VerticalItems.Visibility = horizontal ? Visibility.Collapsed : Visibility.Visible;
    }

    private void ApplyAppearance()
    {
        double gameBarOpacity = _widget?.RequestedOpacity ?? 1;
        if (gameBarOpacity > 1)
        {
            gameBarOpacity /= PercentageScale;
        }

        SolidColorBrush background = new(
            RequestedTheme == ElementTheme.Light
                ? LightBackground
                : DarkBackground)
        {
            // Only the background fades. Text stays crisp and readable.
            Opacity = Math.Clamp(_settings.BackgroundOpacity * gameBarOpacity, 0, 1),
        };
        BackgroundPanel.Background = background;
    }

    private MetricPreference? FindPreference(string id)
    {
        return _settings.Metrics.FirstOrDefault(item => item.Id == id);
    }

    /// <summary>
    /// Includes only settings that require controls to be rebuilt. Text and
    /// separator edits are applied directly and deliberately omitted.
    /// </summary>
    private string CreateControlSignature()
    {
        string metricVisibility = string.Join(
            "|",
            _settings.Metrics.Select(item => $"{item.Id}:{item.IsEnabled}"));
        return string.Join(
            "|",
            _settings.Layout,
            _settings.FontFamily,
            _settings.FontWeight,
            _settings.FontSize,
            _settings.FontColor,
            metricVisibility);
    }

    private async void Widget_SettingsClicked(XboxGameBarWidget sender, object args)
    {
        await sender.ActivateSettingsAsync();
    }

    private async void Widget_RequestedOpacityChanged(XboxGameBarWidget sender, object args)
    {
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, ApplyAppearance);
    }

    private async void Widget_RequestedThemeChanged(XboxGameBarWidget sender, object args)
    {
        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
        {
            RequestedTheme = sender.RequestedTheme;
            ApplyAppearance();
        });
    }

    private async void Widget_GameBarDisplayModeChanged(
        XboxGameBarWidget sender,
        object args)
    {
        await RunOnUiThreadAsync(UpdatePinnedInputBehavior);
    }

    private void UpdatePinnedInputBehavior()
    {
        // The display surface has no controls. In pinned-only mode, excluding
        // it from XAML hit testing keeps it from taking pointer focus from the
        // game. Hiding this CoreWindow's cursor also prevents Windows from
        // drawing an arrow when a captured in-game pointer crosses the widget.
        bool pinnedOnly = _widget?.GameBarDisplayMode ==
            XboxGameBarDisplayMode.PinnedOnly;
        IsHitTestVisible = !pinnedOnly;
        Window.Current.CoreWindow.PointerCursor = pinnedOnly
            ? null
            : _foregroundCursor;
    }

    private string CreateHorizontalSeparator()
    {
        string separator = _settings.HorizontalSeparator.Trim();
        return separator.Length == 0 ? " " : $" {separator} ";
    }

    private async void TelemetryPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _collectorClient.SnapshotReceived -= CollectorClient_SnapshotReceived;
        SettingsService.PreviewChanged -= SettingsService_PreviewChanged;
        Window.Current.CoreWindow.PointerCursor = _foregroundCursor;
        await _collectorClient.StopAsync();

        if (_widget is not null)
        {
            _widget.SettingsClicked -= Widget_SettingsClicked;
            _widget.RequestedOpacityChanged -= Widget_RequestedOpacityChanged;
            _widget.RequestedThemeChanged -= Widget_RequestedThemeChanged;
            _widget.GameBarDisplayModeChanged -= Widget_GameBarDisplayModeChanged;
        }
    }

    private static FontFamily SafeFontFamily(string family)
    {
        try
        {
            return new FontFamily(family);
        }
        catch
        {
            return new FontFamily(TelemetryDefaults.FontFamily);
        }
    }

    private static FontWeight ParseFontWeight(string weight)
    {
        return weight switch
        {
            FontWeightNames.Light => FontWeights.Light,
            FontWeightNames.Normal => FontWeights.Normal,
            FontWeightNames.Bold => FontWeights.Bold,
            FontWeightNames.Black => FontWeights.Black,
            _ => FontWeights.SemiBold,
        };
    }

    private static Color ParseColor(string text)
    {
        string hex = text.Trim().TrimStart('#');
        if (hex.Length == 6)
        {
            hex = "FF" + hex;
        }

        if (hex.Length == 8 &&
            uint.TryParse(
                hex,
                NumberStyles.HexNumber,
                CultureInfo.InvariantCulture,
                out uint argb))
        {
            return Color.FromArgb(
                (byte)(argb >> 24),
                (byte)(argb >> 16),
                (byte)(argb >> 8),
                (byte)argb);
        }

        return Colors.White;
    }

    /// <summary>
    /// Runs synchronous XAML work on the widget's owning thread. Settings I/O
    /// and pipe callbacks can both resume on a pool thread.
    /// </summary>
    private async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
    }
}
