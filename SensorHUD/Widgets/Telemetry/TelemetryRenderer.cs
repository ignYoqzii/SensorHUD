using System;
using System.Collections.Generic;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Presentation;
using Windows.UI;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Media;

namespace SensorHUD.Widgets.Telemetry;

/// <summary>
/// Owns metric controls, typed runs, separators, tooltips, and structure
/// caching. Ordinary samples update existing run text in place.
/// </summary>
internal sealed class TelemetryRenderer(
    ItemsControl verticalItems,
    TextBlock horizontalText)
{
    private readonly Dictionary<string, RenderNode> _nodes = [];
    private readonly Dictionary<uint, SolidColorBrush> _brushes = [];
    private readonly List<string> _renderedKeys = [];

    private WidgetSettings? _renderedSettings;

    public void Render(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        if (NeedsRebuild(model, settings))
        {
            Rebuild(model, settings);
        }
        else
        {
            UpdateValues(model);
        }

        UpdateTooltips(model, settings.Layout.Direction);
        bool horizontal =
            settings.Layout.Direction == WidgetLayout.Horizontal;
        horizontalText.Visibility =
            horizontal ? Visibility.Visible : Visibility.Collapsed;
        verticalItems.Visibility =
            horizontal ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Rebuild(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        _renderedSettings = settings;
        _nodes.Clear();
        _brushes.Clear();
        _renderedKeys.Clear();
        foreach (PresentedMetric metric in model.Metrics)
        {
            _renderedKeys.Add(metric.Key);
        }

        verticalItems.Items.Clear();
        horizontalText.Inlines.Clear();
        FontFamily fontFamily = XamlTextStyle.CreateFontFamily(
            settings.Appearance.FontFamily);
        ApplyTextStyle(
            horizontalText,
            settings.Appearance,
            fontFamily);

        if (settings.Layout.Direction == WidgetLayout.Horizontal)
        {
            BuildHorizontal(model, settings);
        }
        else
        {
            BuildVertical(model, settings, fontFamily);
        }
    }

    private void BuildHorizontal(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        bool first = true;
        foreach (PresentedMetric metric in model.Metrics)
        {
            if (!first)
            {
                horizontalText.Inlines.Add(new Run
                {
                    Text = FormatSeparator(
                        settings.Layout.HorizontalSeparator),
                });
            }

            RenderNode node = CreateNode(horizontalText, metric, settings);
            _nodes.Add(metric.Key, node);
            first = false;
        }
    }

    private void BuildVertical(
        TelemetryDisplayModel model,
        WidgetSettings settings,
        FontFamily fontFamily)
    {
        foreach (PresentedMetric metric in model.Metrics)
        {
            TextBlock text = new()
            {
                TextWrapping = TextWrapping.Wrap,
            };
            ApplyTextStyle(text, settings.Appearance, fontFamily);

            Border item = new()
            {
                Child = text,
                Padding = new Thickness(2, 0, 2, 2),
                BorderBrush = GetBrush(
                    Color.FromArgb(65, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            verticalItems.Items.Add(item);
            _nodes.Add(metric.Key, CreateNode(text, metric, settings));
        }
    }

    private RenderNode CreateNode(
        TextBlock target,
        PresentedMetric metric,
        WidgetSettings settings)
    {
        List<Run> runs = new(metric.Parts.Count);
        foreach (MetricTextPart part in metric.Parts)
        {
            Run run = new() { Text = part.Text };
            ApplyRoleStyle(
                run,
                part.Role,
                settings.Appearance.FontSize,
                metric.Definition,
                metric.Overrides);
            target.Inlines.Add(run);
            runs.Add(run);
        }

        return new RenderNode(target, runs);
    }

    private void UpdateValues(TelemetryDisplayModel model)
    {
        foreach (PresentedMetric metric in model.Metrics)
        {
            if (!_nodes.TryGetValue(metric.Key, out RenderNode? node))
            {
                continue;
            }

            for (int index = 0;
                 index < node.Runs.Count && index < metric.Parts.Count;
                 index++)
            {
                node.Runs[index].Text = metric.Parts[index].Text;
            }
        }
    }

    private void UpdateTooltips(
        TelemetryDisplayModel model,
        WidgetLayout layout)
    {
        if (layout == WidgetLayout.Horizontal)
        {
            List<string>? errors = null;
            foreach (PresentedMetric metric in model.Metrics)
            {
                if (!string.IsNullOrWhiteSpace(metric.Reading?.Error))
                {
                    errors ??= [];
                    errors.Add(
                        $"{metric.Definition.Name}: {metric.Reading.Error}");
                }
            }

            ToolTipService.SetToolTip(
                horizontalText,
                errors is null
                    ? null
                    : string.Join(Environment.NewLine, errors));
            return;
        }

        foreach (PresentedMetric metric in model.Metrics)
        {
            if (_nodes.TryGetValue(metric.Key, out RenderNode? node))
            {
                ToolTipService.SetToolTip(
                    node.Target,
                    metric.Reading?.Error);
            }
        }
    }

    private void ApplyTextStyle(
        TextBlock text,
        AppearanceSettings appearance,
        FontFamily fontFamily)
    {
        text.FontFamily = fontFamily;
        text.FontSize = appearance.FontSize;
        text.FontWeight = XamlTextStyle.ToFontWeight(
            appearance.FontWeight);
        text.Foreground = GetBrush(Colors.White);
        text.TextWrapping = TextWrapping.Wrap;
        text.TextAlignment = XamlTextStyle.ToTextAlignment(
            appearance.HorizontalTextAlignment);
    }

    private void ApplyRoleStyle(
        Run run,
        MetricTextRole role,
        double fontSize,
        MetricDefinition definition,
        MetricOverrides? overrides)
    {
        string color = role == MetricTextRole.Text
            ? GetColor(overrides?.TextColor, definition.TextColor)
            : GetColor(
                overrides?.ValueUnitColor,
                definition.ValueUnitColor);
        run.Foreground = GetBrush(XamlTextStyle.ParseColor(color));

        if (role == MetricTextRole.Value)
        {
            run.FontWeight = FontWeights.Normal;
            run.FontSize = fontSize * 1.06;
        }
        else if (role == MetricTextRole.Unit)
        {
            run.FontWeight = FontWeights.Normal;
            run.FontSize = Math.Max(
                SettingsDefaults.MinimumFontSize,
                fontSize * 0.82);
        }
    }

    private static string GetColor(string? preference, string fallback) =>
        string.IsNullOrWhiteSpace(preference) ? fallback : preference;

    private SolidColorBrush GetBrush(Color color)
    {
        uint key =
            (uint)color.A << 24 |
            (uint)color.R << 16 |
            (uint)color.G << 8 |
            color.B;
        if (!_brushes.TryGetValue(key, out SolidColorBrush? brush))
        {
            brush = new SolidColorBrush(color);
            _brushes.Add(key, brush);
        }

        return brush;
    }

    private static string FormatSeparator(string separator)
    {
        string trimmed = separator.Trim();
        return trimmed.Length == 0 ? " " : $" {trimmed} ";
    }

    private bool NeedsRebuild(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        if (!ReferenceEquals(_renderedSettings, settings) ||
            _nodes.Count != model.Metrics.Count)
        {
            return true;
        }

        for (int index = 0; index < model.Metrics.Count; index++)
        {
            PresentedMetric metric = model.Metrics[index];
            if (!string.Equals(
                    _renderedKeys[index],
                    metric.Key,
                    StringComparison.Ordinal))
            {
                return true;
            }

            if (!_nodes.TryGetValue(metric.Key, out RenderNode? node) ||
                node.Runs.Count != metric.Parts.Count)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record RenderNode(
        TextBlock Target,
        IReadOnlyList<Run> Runs);
}
