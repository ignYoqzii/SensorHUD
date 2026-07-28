using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SensorHUD.Models;
using SensorHUD.Services;
using SensorHUD.Shared;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace SensorHUD;

/// <summary>
/// Game Bar settings widget. Changes are previewed in memory immediately and
/// persisted after a short debounce to avoid writing once per slider pixel.
/// </summary>
public sealed partial class SettingsPage : Page
{
    private const double PercentageScale = 100;

    private readonly Dictionary<string, MetricEditor> _editors = [];
    private readonly DispatcherTimer _saveTimer = new()
    {
        Interval = CollectorProtocol.SettingsSaveDelay,
    };

    private TelemetrySettings _settings = SettingsService.CreateDefaults();
    private IReadOnlyList<MetricDefinition> _definitions = [];
    private bool _loading = true;
    private bool _saving;
    private bool _saveRequested;

    public SettingsPage()
    {
        InitializeComponent();
        _saveTimer.Tick += SaveTimer_Tick;
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    private async void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= SettingsPage_Loaded;

        TelemetrySettings settings = await SettingsService.LoadAsync();
        TelemetrySnapshot? snapshot = CollectorClient.Shared.LatestSnapshot;
        IReadOnlyList<MetricDefinition> definitions =
            MetricCatalog.CreateForSnapshot(snapshot);

        await RunOnUiThreadAsync(() =>
        {
            _settings = settings;
            _definitions = definitions;
            PopulateControls();
            _loading = false;
        });
    }

    private void PopulateControls()
    {
        SelectComboItem(LayoutBox, _settings.Layout);
        HorizontalSeparatorBox.Text = _settings.HorizontalSeparator;
        SelectComboItem(FontWeightBox, _settings.FontWeight);
        OpacitySlider.Value = _settings.BackgroundOpacity * PercentageScale;
        FontSizeSlider.Value = _settings.FontSize;
        FontFamilyBox.Text = _settings.FontFamily;
        FontColorBox.Text = _settings.FontColor;

        MetricSections.Children.Clear();
        _editors.Clear();

        foreach (IGrouping<string, MetricDefinition> group in _definitions
            .GroupBy(definition => definition.Section.Id))
        {
            MetricSection section = group.First().Section;
            StackPanel content = new() { Spacing = 9 };
            content.Children.Add(new TextBlock
            {
                Text = section.Name,
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
            });

            foreach (MetricDefinition definition in group)
            {
                AddMetricEditor(content, definition);
            }

            MetricSections.Children.Add(new Border
            {
                Style = (Style)Resources["SectionCardStyle"],
                Child = content,
            });
        }
    }

    private void AddMetricEditor(StackPanel section, MetricDefinition definition)
    {
        MetricPreference? preference = _settings.Metrics
            .FirstOrDefault(item => item.Id == definition.Id);

        ToggleSwitch toggle = new()
        {
            Header = definition.Name,
            IsOn = preference?.IsEnabled ?? true,
            OnContent = "On",
            OffContent = "Off",
        };
        toggle.Toggled += MetricSetting_Changed;

        TextBox format = new()
        {
            Header = "Format",
            Text = string.IsNullOrWhiteSpace(preference?.Format)
                ? definition.DefaultFormat
                : preference.Format,
            Description = $"Unit: {definition.Unit}",
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        format.TextChanged += MetricSetting_Changed;

        StackPanel row = new() { Spacing = 4 };
        row.Children.Add(toggle);
        row.Children.Add(format);
        section.Children.Add(row);
        _editors[definition.Id] = new MetricEditor(toggle, format);
    }

    private TelemetrySettings ReadSettingsFromControls()
    {
        HashSet<string> visibleIds = _definitions
            .Select(definition => definition.Id)
            .ToHashSet(StringComparer.Ordinal);

        // Keep preferences for temporarily absent GPUs. Otherwise opening
        // settings while the collector is unavailable would silently erase
        // those user choices on the next save.
        List<MetricPreference> preferences = [.. _settings.Metrics.Where(preference => !visibleIds.Contains(preference.Id))];
        preferences.AddRange(_definitions.Select(definition =>
        {
            MetricEditor editor = _editors[definition.Id];
            return new MetricPreference
            {
                Id = definition.Id,
                IsEnabled = editor.Toggle.IsOn,
                Format = editor.Format.Text,
            };
        }));

        return new TelemetrySettings
        {
            Layout = SelectedText(LayoutBox, TelemetryDefaults.Layout),
            HorizontalSeparator = HorizontalSeparatorBox.Text,
            BackgroundOpacity = OpacitySlider.Value / PercentageScale,
            FontFamily = FontFamilyBox.Text,
            FontWeight = SelectedText(FontWeightBox, TelemetryDefaults.FontWeight),
            FontSize = FontSizeSlider.Value,
            FontColor = FontColorBox.Text,
            Metrics = preferences,
        };
    }

    private void QueueAutoSave()
    {
        if (_loading)
        {
            return;
        }

        TelemetrySettings preview = ReadSettingsFromControls();
        SettingsService.Preview(preview);

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
        if (_saving || !_saveRequested)
        {
            return;
        }

        _saving = true;
        _saveRequested = false;
        TelemetrySettings settings = ReadSettingsFromControls();

        try
        {
            await SettingsService.SaveAsync(settings);
            _settings = settings;
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

    private void Setting_Changed(object sender, SelectionChangedEventArgs e) => QueueAutoSave();

    private void Setting_TextChanged(object sender, TextChangedEventArgs e) => QueueAutoSave();

    private void MetricSetting_Changed(object sender, RoutedEventArgs e) => QueueAutoSave();

    private void OpacitySlider_ValueChanged(
        object sender,
        Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        OpacityValue?.Text = $"{e.NewValue:F0}%";

        QueueAutoSave();
    }

    private void FontSizeSlider_ValueChanged(
        object sender,
        Windows.UI.Xaml.Controls.Primitives.RangeBaseValueChangedEventArgs e)
    {
        FontSizeValue?.Text = $"{e.NewValue:F0}";

        QueueAutoSave();
    }

    private void ResetButton_Click(object sender, RoutedEventArgs e)
    {
        _loading = true;
        _settings = SettingsService.CreateDefaults();
        PopulateControls();
        _loading = false;
        QueueAutoSave();
    }

    private async void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        _saveTimer.Stop();
        _saveTimer.Tick -= SaveTimer_Tick;
        Unloaded -= SettingsPage_Unloaded;

        if (_saveRequested)
        {
            try
            {
                await SettingsService.SaveAsync(ReadSettingsFromControls());
            }
            catch
            {
                // The previous atomic settings file remains valid.
            }
        }
    }

    private static void SelectComboItem(ComboBox box, string value)
    {
        box.SelectedItem = box.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(
                item.Content?.ToString(),
                value,
                StringComparison.Ordinal))
            ?? box.Items.FirstOrDefault();
    }

    private static string SelectedText(ComboBox box, string fallback)
    {
        return (box.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? fallback;
    }

    private async Task RunOnUiThreadAsync(Action action)
    {
        if (Dispatcher.HasThreadAccess)
        {
            action();
            return;
        }

        await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => action());
    }

    private sealed record MetricEditor(ToggleSwitch Toggle, TextBox Format);
}
