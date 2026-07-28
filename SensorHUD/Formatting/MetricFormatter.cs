using System;
using System.Globalization;
using SensorHUD.Models;
using SensorHUD.Shared;

namespace SensorHUD.Formatting;

/// <summary>
/// Expands the intentionally small public template language used in settings.
/// Unknown text is left untouched, making templates safe and predictable.
/// </summary>
internal static class MetricFormatter
{
    public static string Format(
        MetricDefinition definition,
        TelemetryValue? reading,
        string? userTemplate)
    {
        string template = string.IsNullOrWhiteSpace(userTemplate)
            ? definition.DefaultFormat
            : userTemplate.Trim();

        string numericValue = reading?.Value is double value
            ? value.ToString($"F{definition.DecimalPlaces}", CultureInfo.CurrentCulture)
            : "N/A";

        return template
            .Replace("{value}", numericValue, StringComparison.Ordinal)
            .Replace("{unit}", definition.Unit, StringComparison.Ordinal)
            .Replace("{name}", definition.Name, StringComparison.Ordinal)
            .Replace("{device}", definition.DeviceName, StringComparison.Ordinal);
    }
}
