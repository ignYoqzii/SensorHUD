using System;
using System.Collections.Generic;
using System.Globalization;
using SensorHUD.Models;
using SensorHUD.Shared;

namespace SensorHUD.Formatting;

/// <summary>
/// Expands the intentionally small public template language used in settings.
/// Typed parts let the widget emphasize values and units without losing the
/// user's exact token order. Unknown text remains untouched.
/// </summary>
internal static class MetricFormatter
{
    private static readonly TemplateToken[] Tokens =
    [
        new("{value}", MetricTextRole.Value),
        new("{unit}", MetricTextRole.Unit),
        new("{name}", MetricTextRole.Text),
        new("{device}", MetricTextRole.Text),
    ];

    public static IReadOnlyList<MetricTextPart> FormatParts(
        MetricDefinition definition,
        TelemetryValue? reading,
        MetricPreference? preference)
    {
        string template = string.IsNullOrWhiteSpace(preference?.Format)
            ? definition.DefaultFormat
            : preference.Format.Trim();

        int decimalPlaces =
            preference?.DecimalPlaces ?? definition.DecimalPlaces;
        string numericValue = reading?.Value is double value
            ? value.ToString(
                $"F{decimalPlaces}",
                CultureInfo.CurrentCulture)
            : "N/A";

        List<MetricTextPart> parts = [];
        int position = 0;

        while (position < template.Length)
        {
            TemplateToken? next = null;
            int nextIndex = template.Length;

            foreach (TemplateToken token in Tokens)
            {
                int index = template.IndexOf(
                    token.Token,
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
                AddPart(parts, template[position..], MetricTextRole.Text);
                break;
            }

            AddPart(
                parts,
                template[position..nextIndex],
                MetricTextRole.Text);
            AddPart(
                parts,
                Replacement(next.Token, definition, numericValue),
                next.Role);
            position = nextIndex + next.Token.Length;
        }

        return parts;
    }

    private static string Replacement(
        string token,
        MetricDefinition definition,
        string numericValue)
    {
        return token switch
        {
            "{value}" => numericValue,
            "{unit}" => definition.Unit,
            "{name}" => definition.Name,
            "{device}" => definition.DeviceName,
            _ => token,
        };
    }

    private static void AddPart(
        List<MetricTextPart> parts,
        string text,
        MetricTextRole role)
    {
        if (text.Length > 0)
        {
            parts.Add(new MetricTextPart(text, role));
        }
    }

    private sealed record TemplateToken(string Token, MetricTextRole Role);
}

internal enum MetricTextRole
{
    Text,
    Value,
    Unit,
}

internal sealed record MetricTextPart(string Text, MetricTextRole Role);
