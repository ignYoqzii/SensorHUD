namespace SensorHUD.Core.Metrics;

/// <summary>
/// Identifies a metric category. Durable settings use metric IDs rather than
/// enum values, so category members may be arranged for source readability.
/// </summary>
public enum MetricCategory
{
    FrameRate,
    Cpu,
    Gpu,
    Memory,
    Network,
}

/// <summary>
/// Determines whether a metric has one system-wide instance or one instance
/// for every detected device.
/// </summary>
public enum MetricScope
{
    Global,
    PerDevice,
}

/// <summary>
/// Describes one settings and presentation category.
/// </summary>
public sealed record MetricCategoryDefinition
{
    /// <summary>
    /// Gets the category identifier used by metric definitions.
    /// </summary>
    public required MetricCategory Id { get; init; }

    /// <summary>
    /// Gets the user-facing category name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the optional text displayed directly below the category name.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Gets the category's stable relative display position.
    /// </summary>
    public required int SortOrder { get; init; }
}

/// <summary>
/// Describes one metric independently of any device or reading. This metadata
/// drives settings, ordering, formatting, and presentation.
/// </summary>
public sealed record MetricDefinition
{
    /// <summary>
    /// Gets the stable metric identity used by providers and saved settings.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Gets the category containing the metric.
    /// </summary>
    public required MetricCategory Category { get; init; }

    /// <summary>
    /// Gets the user-facing metric name and the value of the {name} token.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the unit exposed through the {unit} format token.
    /// </summary>
    public required string Unit { get; init; }

    /// <summary>
    /// Gets the default format using SensorHUD's documented format tokens.
    /// </summary>
    public required string Format { get; init; }

    /// <summary>
    /// Gets the default number of decimal places.
    /// </summary>
    public required int Decimals { get; init; }

    /// <summary>
    /// Gets the default color of literal text, device names, and metric names.
    /// </summary>
    public required string TextColor { get; init; }

    /// <summary>
    /// Gets the default color of the formatted metric value and unit.
    /// </summary>
    public required string ValueUnitColor { get; init; }

    /// <summary>
    /// Gets whether a new installation displays the metric by default.
    /// </summary>
    public bool IsVisibleByDefault { get; init; } = true;

    /// <summary>
    /// Gets the metric's stable relative position inside its category.
    /// </summary>
    public required int SortOrder { get; init; }

    /// <summary>
    /// Gets how readings and user overrides are scoped.
    /// </summary>
    public MetricScope Scope { get; init; }
}
