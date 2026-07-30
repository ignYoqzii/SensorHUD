using System.Text.Json.Serialization;

namespace SensorHUD.Core.Settings;

/// <summary>
/// Optional user overrides for one global or per-device metric instance.
/// Null properties inherit the corresponding metric registry default.
/// </summary>
public sealed class MetricOverrides
{
    /// <summary>
    /// Gets or sets an optional visibility override.
    /// </summary>
    public bool? IsVisible { get; set; }

    /// <summary>
    /// Gets or sets an optional format override.
    /// </summary>
    public string? Format { get; set; }

    /// <summary>
    /// Gets or sets an optional decimal-count override.
    /// </summary>
    public int? Decimals { get; set; }

    /// <summary>
    /// Gets or sets an optional literal-text ARGB color override.
    /// </summary>
    public string? TextColor { get; set; }

    /// <summary>
    /// Gets or sets an optional value-and-unit ARGB color override.
    /// </summary>
    public string? ValueUnitColor { get; set; }

    /// <summary>
    /// Gets whether this object changes any registry default.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty =>
        IsVisible is null &&
        Format is null &&
        Decimals is null &&
        TextColor is null &&
        ValueUnitColor is null;
}
