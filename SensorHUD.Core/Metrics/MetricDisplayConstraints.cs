namespace SensorHUD.Core.Metrics;

/// <summary>
/// Supported ranges shared by metric definitions, overrides, and editors.
/// </summary>
public static class MetricDisplayConstraints
{
    /// <summary>
    /// Gets the minimum supported decimal count.
    /// </summary>
    public const int MinimumDecimals = 0;

    /// <summary>
    /// Gets the maximum supported decimal count.
    /// </summary>
    public const int MaximumDecimals = 2;
}
