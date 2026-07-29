using System.Collections.Generic;
using Windows.UI.Xaml;

namespace SensorHUD.Widgets.Settings;

/// <summary>
/// One category settings card containing global or device-specific metrics.
/// </summary>
public sealed class MetricCategoryViewModel
{
    internal MetricCategoryViewModel(
        string name,
        string? description,
        IReadOnlyList<MetricSettingsViewModel> metrics)
    {
        Name = name;
        Description = description ?? string.Empty;
        DescriptionVisibility = string.IsNullOrWhiteSpace(description)
            ? Visibility.Collapsed
            : Visibility.Visible;
        Metrics = metrics;
        MetricCountText = metrics.Count == 1
            ? "1 metric"
            : $"{metrics.Count} metrics";
    }

    /// <summary>
    /// Gets the category-card heading.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the optional category description as bindable text.
    /// </summary>
    public string Description { get; }

    /// <summary>
    /// Gets whether the optional category description is displayed.
    /// </summary>
    public Visibility DescriptionVisibility { get; }

    /// <summary>
    /// Gets the user-facing metric-count summary for the card.
    /// </summary>
    public string MetricCountText { get; }

    /// <summary>
    /// Gets the global or device-specific metric editors in display order.
    /// </summary>
    public IReadOnlyList<MetricSettingsViewModel> Metrics { get; }
}
