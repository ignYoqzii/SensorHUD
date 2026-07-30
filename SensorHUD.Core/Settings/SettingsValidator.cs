using System;
using System.Collections.Generic;
using SensorHUD.Core.Metrics;

namespace SensorHUD.Core.Settings;

/// <summary>
/// Produces safe, independent settings values after file loading or UI edits.
/// Defaults and supported ranges are intentionally defined in one place.
/// </summary>
public static class SettingsValidator
{
    /// <summary>
    /// Returns a validated deep copy without mutating the caller's object.
    /// </summary>
    public static WidgetSettings Normalize(WidgetSettings? source)
    {
        WidgetSettings defaults = SettingsDefaults.Create();
        if (source is null)
        {
            return defaults;
        }

        WidgetSettings result = defaults;
        result.Layout = NormalizeLayout(source.Layout, defaults.Layout);
        result.Appearance = NormalizeAppearance(
            source.Appearance,
            defaults.Appearance);

        foreach ((string key, MetricOverrides overrides) in
                 source.MetricOverrides ?? [])
        {
            if (!MetricInstanceKey.TryParse(
                    key,
                    out string metricId,
                    out string? deviceId) ||
                !MetricRegistry.TryGet(
                    metricId,
                    out MetricDefinition definition) ||
                (definition.Scope == MetricScope.PerDevice) !=
                    (deviceId is not null) ||
                overrides is null)
            {
                continue;
            }

            MetricOverrides normalized = new()
            {
                IsVisible = overrides.IsVisible is bool visible &&
                    visible != definition.IsVisibleByDefault
                        ? visible
                        : null,
                Format = NormalizeFormat(
                    overrides.Format,
                    definition.Format),
                Decimals = NormalizeDecimals(
                    overrides.Decimals,
                    definition.Decimals),
                TextColor = NormalizeColorOverride(
                    overrides.TextColor,
                    definition.TextColor),
                ValueUnitColor = NormalizeColorOverride(
                    overrides.ValueUnitColor,
                    definition.ValueUnitColor),
            };
            if (!normalized.IsEmpty)
            {
                result.MetricOverrides[key] = normalized;
            }
        }

        return result;
    }

    private static LayoutSettings NormalizeLayout(
        LayoutSettings? source,
        LayoutSettings defaults) => source is null
            ? defaults
            : new LayoutSettings
            {
                Direction = Enum.IsDefined(source.Direction)
                    ? source.Direction
                    : defaults.Direction,
                HorizontalSeparator =
                    source.HorizontalSeparator ?? string.Empty,
            };

    private static AppearanceSettings NormalizeAppearance(
        AppearanceSettings? source,
        AppearanceSettings defaults)
    {
        if (source is null)
        {
            return defaults;
        }

        return new AppearanceSettings
        {
            BackgroundOpacity = Math.Clamp(
                source.BackgroundOpacity,
                SettingsDefaults.MinimumBackgroundOpacity,
                SettingsDefaults.MaximumBackgroundOpacity),
            FontFamily = string.IsNullOrWhiteSpace(source.FontFamily)
                ? defaults.FontFamily
                : source.FontFamily.Trim(),
            FontWeight = Enum.IsDefined(source.FontWeight)
                ? source.FontWeight
                : defaults.FontWeight,
            FontSize = Math.Clamp(
                source.FontSize,
                SettingsDefaults.MinimumFontSize,
                SettingsDefaults.MaximumFontSize),
            HorizontalTextAlignment =
                Enum.IsDefined(source.HorizontalTextAlignment)
                    ? source.HorizontalTextAlignment
                    : defaults.HorizontalTextAlignment,
            VerticalTextAlignment =
                Enum.IsDefined(source.VerticalTextAlignment)
                    ? source.VerticalTextAlignment
                    : defaults.VerticalTextAlignment,
        };
    }

    private static bool IsArgbColor(string? value) =>
        value is { Length: 9 } &&
        value[0] == '#' &&
        uint.TryParse(
            value.AsSpan(1),
            System.Globalization.NumberStyles.HexNumber,
            provider: null,
            out _);

    private static string? NormalizeFormat(
        string? value,
        string fallback) =>
        string.IsNullOrWhiteSpace(value) ||
        string.Equals(value, fallback, StringComparison.Ordinal)
            ? null
            : value;

    private static int? NormalizeDecimals(
        int? value,
        int fallback)
    {
        if (value is not int decimals)
        {
            return null;
        }

        int normalized = Math.Clamp(
            decimals,
            MetricDisplayConstraints.MinimumDecimals,
            MetricDisplayConstraints.MaximumDecimals);
        return normalized == fallback ? null : normalized;
    }

    private static string? NormalizeColorOverride(
        string? value,
        string fallback)
    {
        if (!IsArgbColor(value))
        {
            return null;
        }

        string normalized = value!.ToUpperInvariant();
        return string.Equals(
            normalized,
            fallback,
            StringComparison.OrdinalIgnoreCase)
                ? null
                : normalized;
    }
}
