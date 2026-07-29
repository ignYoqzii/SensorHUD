using System;
using System.Collections.Generic;
using System.Linq;
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
internal sealed class TelemetryRenderer
{
    private readonly ItemsControl _verticalItems;
    private readonly TextBlock _horizontalText;
    private readonly Dictionary<string, RenderNode> _nodes = [];

    private string _structureSignature = string.Empty;

    public TelemetryRenderer(
        ItemsControl verticalItems,
        TextBlock horizontalText)
    {
        _verticalItems = verticalItems;
        _horizontalText = horizontalText;
    }

    public void Render(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        string signature = CreateStructureSignature(model, settings);
        if (!string.Equals(
                signature,
                _structureSignature,
                StringComparison.Ordinal))
        {
            _structureSignature = signature;
            Rebuild(model, settings);
        }
        else
        {
            UpdateValues(model);
        }

        UpdateTooltips(model, settings.Layout);
        bool horizontal = settings.Layout == WidgetLayout.Horizontal;
        _horizontalText.Visibility =
            horizontal ? Visibility.Visible : Visibility.Collapsed;
        _verticalItems.Visibility =
            horizontal ? Visibility.Collapsed : Visibility.Visible;
    }

    private void Rebuild(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        _nodes.Clear();
        _verticalItems.Items.Clear();
        _horizontalText.Inlines.Clear();
        ApplyTextStyle(_horizontalText, settings.Appearance);

        if (settings.Layout == WidgetLayout.Horizontal)
        {
            BuildHorizontal(model, settings);
        }
        else
        {
            BuildVertical(model, settings);
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
                _horizontalText.Inlines.Add(new Run
                {
                    Text = FormatSeparator(settings.HorizontalSeparator),
                });
            }

            RenderNode node = CreateNode(_horizontalText, metric, settings);
            _nodes.Add(metric.Key, node);
            first = false;
        }

    }

    private void BuildVertical(
        TelemetryDisplayModel model,
        WidgetSettings settings)
    {
        foreach (PresentedMetric metric in model.Metrics)
        {
            TextBlock text = new()
            {
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Top,
            };
            ApplyTextStyle(text, settings.Appearance);

            Border item = new()
            {
                Child = text,
                Padding = new Thickness(2, 0, 2, 2),
                BorderBrush = new SolidColorBrush(
                    Color.FromArgb(65, 255, 255, 255)),
                BorderThickness = new Thickness(0, 0, 0, 1),
            };
            _verticalItems.Items.Add(item);
            _nodes.Add(metric.Key, CreateNode(text, metric, settings));
        }
    }

    private static RenderNode CreateNode(
        TextBlock target,
        PresentedMetric metric,
        WidgetSettings settings)
    {
        List<Run> runs = [];
        foreach (MetricTextPart part in metric.Parts)
        {
            Run run = new() { Text = part.Text };
            ApplyRoleStyle(run, part.Role, settings.Appearance.FontSize);
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
            string[] errors = model.Metrics
                .Where(metric =>
                    !string.IsNullOrWhiteSpace(metric.Reading?.Error))
                .Select(metric =>
                    $"{metric.Definition.Label}: {metric.Reading!.Error}")
                .ToArray();
            ToolTipService.SetToolTip(
                _horizontalText,
                errors.Length == 0
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

    private static void ApplyTextStyle(
        TextBlock text,
        AppearanceSettings appearance)
    {
        text.FontFamily = XamlTextStyle.CreateFontFamily(
            appearance.FontFamily);
        text.FontSize = appearance.FontSize;
        text.FontWeight = XamlTextStyle.ToFontWeight(
            appearance.FontWeight);
        text.Foreground = new SolidColorBrush(
            XamlTextStyle.ParseColor(appearance.FontColor));
        text.TextWrapping = TextWrapping.Wrap;
        text.VerticalAlignment = VerticalAlignment.Top;
    }

    private static void ApplyRoleStyle(
        Run run,
        MetricTextRole role,
        double fontSize)
    {
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

    private static string FormatSeparator(string separator)
    {
        string trimmed = separator.Trim();
        return trimmed.Length == 0 ? " " : $" {trimmed} ";
    }

    private static string CreateStructureSignature(
        TelemetryDisplayModel model,
        WidgetSettings settings) => string.Join(
            "|",
            settings.Layout,
            settings.HorizontalSeparator,
            settings.Appearance.FontFamily,
            settings.Appearance.FontWeight,
            settings.Appearance.FontSize,
            settings.Appearance.FontColor,
            string.Join(
                ";",
                model.Metrics.Select(metric =>
                    $"{metric.Key}:{metric.Settings?.Template ??
                        metric.Definition.DefaultTemplate}")));

    private sealed record RenderNode(
        TextBlock Target,
        IReadOnlyList<Run> Runs);
}
