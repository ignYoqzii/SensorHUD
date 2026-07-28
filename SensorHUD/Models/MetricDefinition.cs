namespace SensorHUD.Models;

/// <summary>
/// Immutable display metadata for one metric. Providers publish data; this
/// model describes how the frontend groups and formats it.
/// </summary>
internal sealed record MetricDefinition(
    string Id,
    string Name,
    MetricSection Section,
    string Unit,
    string DefaultFormat,
    int DecimalPlaces,
    int Order,
    string DeviceName = "");

/// <summary>
/// One hardware group in settings. Dynamic GPUs receive their own section.
/// </summary>
internal sealed record MetricSection(string Id, string Name, int Order);
