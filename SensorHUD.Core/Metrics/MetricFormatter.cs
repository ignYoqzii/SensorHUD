using System;
using System.Collections.Generic;
using System.Globalization;
using SensorHUD.Core.Settings;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// Identifies the semantic role of a metric-format segment.
/// </summary>
public enum MetricTextRole
{
    Text,
    Value,
    Unit,
}

/// <summary>
/// One formatted segment of a metric format.
/// </summary>
public readonly record struct MetricTextPart(
    string Text,
    MetricTextRole Role);

/// <summary>
/// Expands the small, documented metric-format language into typed text
/// parts so the frontend can style values and units independently.
/// </summary>
public static class MetricFormatter
{
    private static readonly FormatToken[] Tokens =
    [
        new("{value}", MetricTextRole.Value),
        new("{unit}", MetricTextRole.Unit),
        new("{name}", MetricTextRole.Text),
        new("{device}", MetricTextRole.Text),
    ];
    private static readonly string[] NumericFormats = ["F0", "F1", "F2"];

    /// <summary>
    /// Formats one presentation slot while preserving semantic value and unit
    /// parts. A missing value becomes a frontend-only placeholder and is never
    /// serialized as telemetry.
    /// </summary>
    public static IReadOnlyList<MetricTextPart> Format(
        MetricDefinition definition,
        double? value,
        string? deviceName,
        MetricOverrides? overrides)
    {
        ArgumentNullException.ThrowIfNull(definition);

        string format = string.IsNullOrWhiteSpace(overrides?.Format)
            ? definition.Format
            : overrides.Format;
        int decimals = overrides?.Decimals ?? definition.Decimals;
        string numericValue = value is double available
            ? available.ToString(
                GetNumericFormat(decimals),
                CultureInfo.CurrentCulture)
            : "N/A";
        string resolvedDeviceName = string.IsNullOrWhiteSpace(deviceName)
            ? GetFallbackDeviceName(definition.Category)
            : deviceName;

        List<MetricTextPart> parts = new(4);
        int position = 0;
        while (position < format.Length)
        {
            FormatToken? next = null;
            int nextIndex = format.Length;

            foreach (FormatToken token in Tokens)
            {
                int index = format.IndexOf(
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
                Add(parts, format[position..], MetricTextRole.Text);
                break;
            }

            Add(parts, format[position..nextIndex], MetricTextRole.Text);
            Add(
                parts,
                Replace(
                    next.Text,
                    definition,
                    numericValue,
                    resolvedDeviceName),
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
            "{name}" => definition.Name,
            "{device}" => deviceName,
            _ => token,
        };

    private static string GetFallbackDeviceName(
        MetricCategory category) =>
        MetricRegistry.GetCategory(category).Name;

    private static string GetNumericFormat(int decimals) =>
        (uint)decimals < NumericFormats.Length
            ? NumericFormats[decimals]
            : $"F{decimals}";

    private static void Add(
        List<MetricTextPart> parts,
        string text,
        MetricTextRole role)
    {
        if (text.Length > 0)
        {
            parts.Add(new MetricTextPart(text, role));
        }
    }

    private sealed record FormatToken(string Text, MetricTextRole Role);
}
