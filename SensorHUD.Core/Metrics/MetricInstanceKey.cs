using System;

namespace SensorHUD.Core.Metrics;

/// <summary>
/// Creates stable settings keys for global metrics and device-specific metric
/// instances. Providers never need to know how preferences are persisted.
/// </summary>
public static class MetricInstanceKey
{
    public const char Separator = '@';

    /// <summary>
    /// Returns the preference key for a metric reading.
    /// </summary>
    public static string Create(MetricDefinition definition, string? deviceId)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!definition.IsPerDevice)
        {
            return definition.Id;
        }

        if (string.IsNullOrWhiteSpace(deviceId))
        {
            throw new ArgumentException(
                $"Metric '{definition.Id}' requires a device identifier.",
                nameof(deviceId));
        }

        return $"{definition.Id}{Separator}{deviceId}";
    }

    /// <summary>
    /// Tries to separate a preference key into its base metric and device ID.
    /// </summary>
    public static bool TryParse(
        string key,
        out string metricId,
        out string? deviceId)
    {
        int separatorIndex = key.IndexOf(Separator);
        if (separatorIndex <= 0 || separatorIndex == key.Length - 1)
        {
            metricId = key;
            deviceId = null;
            return !string.IsNullOrWhiteSpace(key);
        }

        metricId = key[..separatorIndex];
        deviceId = key[(separatorIndex + 1)..];
        return true;
    }
}
