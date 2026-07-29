namespace SensorHUD.Core.Metrics;

/// <summary>
/// Identifies the section in which a metric appears in the settings widget.
/// </summary>
public enum MetricGroup
{
    FrameRate,
    Cpu,
    Gpu,
    Memory,
    Network,
}

/// <summary>
/// Describes one metric independently of any particular device or reading.
/// This metadata drives both the settings UI and telemetry presentation.
/// </summary>
public sealed record MetricDefinition(
    string Id,
    MetricGroup Group,
    string Label,
    string Unit,
    string DefaultTemplate,
    int DefaultPrecision,
    bool IsVisibleByDefault,
    int SortOrder,
    bool IsPerDevice = false);
