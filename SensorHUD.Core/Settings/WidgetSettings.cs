using System.Collections.Generic;

namespace SensorHUD.Core.Settings;

/// <summary>
/// All durable user preferences for the telemetry widget.
/// </summary>
public sealed class WidgetSettings
{
    /// <summary>
    /// Gets or sets widget layout preferences.
    /// </summary>
    public LayoutSettings Layout { get; set; } = new();

    /// <summary>
    /// Gets or sets widget appearance preferences.
    /// </summary>
    public AppearanceSettings Appearance { get; set; } = new();

    /// <summary>
    /// User overrides keyed by <see cref="Metrics.MetricInstanceKey"/>.
    /// Missing entries and null properties use metric registry defaults.
    /// </summary>
    public Dictionary<string, MetricOverrides> MetricOverrides { get; set; } =
        [];
}
