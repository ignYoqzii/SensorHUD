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

        WidgetSettings result = new()
        {
            Layout = Enum.IsDefined(source.Layout)
                ? source.Layout
                : defaults.Layout,
            HorizontalSeparator = source.HorizontalSeparator ?? string.Empty,
            Appearance = NormalizeAppearance(source.Appearance),
            Metrics = [],
        };

        foreach ((string key, MetricDisplaySettings preference) in
                 source.Metrics ?? [])
        {
            if (!MetricInstanceKey.TryParse(
                    key,
                    out string metricId,
                    out string? deviceId) ||
                !MetricRegistry.TryGet(
                    metricId,
                    out MetricDefinition definition) ||
                definition.IsPerDevice != (deviceId is not null) ||
                preference is null)
            {
                continue;
            }

            result.Metrics[key] = new MetricDisplaySettings
            {
                IsVisible = preference.IsVisible,
                Format = string.IsNullOrWhiteSpace(preference.Format)
                    ? definition.Format
                    : preference.Format,
                Decimals = preference.Decimals is int decimals
                    ? Math.Clamp(
                        decimals,
                        SettingsDefaults.MinimumDecimals,
                        SettingsDefaults.MaximumDecimals)
                    : null,
            };
        }

        return result;
    }

    private static AppearanceSettings NormalizeAppearance(
        AppearanceSettings? source)
    {
        AppearanceSettings defaults = SettingsDefaults.Create().Appearance;
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
            FontColor = IsArgbColor(source.FontColor)
                ? source.FontColor.ToUpperInvariant()
                : defaults.FontColor,
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
}
