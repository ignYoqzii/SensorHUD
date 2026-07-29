using System;
using System.Collections.Generic;
using System.Globalization;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Telemetry;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// Identifies the semantic role of a formatted template segment.
/// </summary>
public enum MetricTextRole
{
    Text,
    Value,
    Unit,
}

/// <summary>
/// One formatted segment of a metric template.
/// </summary>
public sealed record MetricTextPart(string Text, MetricTextRole Role);

/// <summary>
/// Expands the small, documented metric-template language into typed text
/// parts so the frontend can style values and units independently.
/// </summary>
public static class MetricFormatter
{
    private static readonly TemplateToken[] Tokens =
    [
        new("{value}", MetricTextRole.Value),
        new("{unit}", MetricTextRole.Unit),
        new("{name}", MetricTextRole.Text),
        new("{device}", MetricTextRole.Text),
    ];

    /// <summary>
    /// Formats one reading while preserving semantic value and unit parts.
    /// </summary>
    public static IReadOnlyList<MetricTextPart> Format(
        MetricDefinition definition,
        MetricReading? reading,
        MetricDisplaySettings? settings)
    {
        string template = string.IsNullOrWhiteSpace(settings?.Template)
            ? definition.DefaultTemplate
            : settings.Template;
        int precision = settings?.Precision ?? definition.DefaultPrecision;
        string numericValue = reading?.Value is double value
            ? value.ToString($"F{precision}", CultureInfo.CurrentCulture)
            : "N/A";
        string deviceName = string.IsNullOrWhiteSpace(reading?.DeviceName)
            ? GetFallbackDeviceName(definition.Group)
            : reading.DeviceName;

        List<MetricTextPart> parts = [];
        int position = 0;
        while (position < template.Length)
        {
            TemplateToken? next = null;
            int nextIndex = template.Length;

            foreach (TemplateToken token in Tokens)
            {
                int index = template.IndexOf(
                    token.Text,
                    position,
                    StringComparison.Ordinal);
                if (index >= 0 && index < nextIndex)
                {
                    next = token;
                    nextIndex = index;
                }
            }

            if (next is null)
            {
                Add(parts, template[position..], MetricTextRole.Text);
                break;
            }

            Add(parts, template[position..nextIndex], MetricTextRole.Text);
            Add(
                parts,
                Replace(next.Text, definition, numericValue, deviceName),
                next.Role);
            position = nextIndex + next.Text.Length;
        }

        return parts;
    }

    private static string Replace(
        string token,
        MetricDefinition definition,
        string value,
        string deviceName) => token switch
        {
            "{value}" => value,
            "{unit}" => definition.Unit,
            "{name}" => definition.Label,
            "{device}" => deviceName,
            _ => token,
        };

    private static string GetFallbackDeviceName(MetricGroup group) =>
        group switch
        {
            MetricGroup.Cpu => "CPU",
            MetricGroup.Gpu => "GPU",
            MetricGroup.Memory => "Memory",
            MetricGroup.Network => "Network",
            _ => string.Empty,
        };

    private static void Add(
        ICollection<MetricTextPart> parts,
        string text,
        MetricTextRole role)
    {
        if (text.Length > 0)
        {
            parts.Add(new MetricTextPart(text, role));
        }
    }

    private sealed record TemplateToken(string Text, MetricTextRole Role);
}
