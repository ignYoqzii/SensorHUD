using System.Text.Json;
using SensorHUD.Core.Metrics;
using SensorHUD.Core.Settings;
using SensorHUD.Core.Transport;

namespace SensorHUD.Core.Tests;

public sealed class SettingsValidatorTests
{
    [Fact]
    public void NormalizeRemovesValuesMatchingRegistryDefaults()
    {
        MetricDefinition definition = MetricRegistry.Get(MetricRegistry.Fps);
        WidgetSettings source = new();
        source.MetricOverrides.Add(
            definition.Id,
            new MetricOverrides
            {
                IsVisible = definition.IsVisibleByDefault,
                Format = definition.Format,
                Decimals = definition.Decimals,
                TextColor = definition.TextColor,
                ValueUnitColor = definition.ValueUnitColor,
            });

        WidgetSettings result = SettingsValidator.Normalize(source);

        Assert.Empty(result.MetricOverrides);
    }

    [Fact]
    public void NormalizePreservesValidCustomOverrides()
    {
        MetricDefinition definition =
            MetricRegistry.Get(MetricRegistry.GpuUsage);
        string key = MetricInstanceKey.Create(definition, "gpu-0");
        WidgetSettings source = new();
        source.MetricOverrides.Add(
            key,
            new MetricOverrides
            {
                IsVisible = false,
                Format = "{name} {value}",
                Decimals = 2,
                TextColor = "#ff123456",
            });

        WidgetSettings result = SettingsValidator.Normalize(source);

        MetricOverrides overrides = Assert.Single(result.MetricOverrides).Value;
        Assert.False(overrides.IsVisible);
        Assert.Equal("{name} {value}", overrides.Format);
        Assert.Equal(2, overrides.Decimals);
        Assert.Equal("#FF123456", overrides.TextColor);
        Assert.Null(overrides.ValueUnitColor);
    }

    [Fact]
    public void SettingsJsonOmitsNullOverrideProperties()
    {
        WidgetSettings settings = new();
        settings.MetricOverrides.Add(
            MetricRegistry.Fps,
            new MetricOverrides { IsVisible = false });

        string json = JsonSerializer.Serialize(
            settings,
            SettingsJsonContext.Default.WidgetSettings);

        Assert.Contains("\"isVisible\": false", json);
        Assert.DoesNotContain("\"format\"", json);
        Assert.DoesNotContain("\"isEmpty\"", json);
    }

    [Fact]
    public void NormalizeRejectsKeysWithTheWrongScope()
    {
        WidgetSettings source = new();
        source.MetricOverrides.Add(
            $"{MetricRegistry.Fps}@device",
            new MetricOverrides { IsVisible = false });
        source.MetricOverrides.Add(
            MetricRegistry.GpuUsage,
            new MetricOverrides { IsVisible = false });

        WidgetSettings result = SettingsValidator.Normalize(source);

        Assert.Empty(result.MetricOverrides);
    }

    [Fact]
    public void SettingsJsonRoundTripsSectionsAndOverrides()
    {
        WidgetSettings source = new()
        {
            Layout = new LayoutSettings
            {
                Direction = WidgetLayout.Horizontal,
                HorizontalSeparator = "•",
            },
            Appearance = new AppearanceSettings
            {
                BackgroundOpacity = 0.5,
                FontFamily = "Arial",
                FontSize = 24,
                FontWeight = WidgetFontWeight.Bold,
                HorizontalTextAlignment = WidgetHorizontalAlignment.Right,
                VerticalTextAlignment = WidgetVerticalAlignment.Bottom,
            },
        };
        source.MetricOverrides.Add(
            MetricRegistry.Fps,
            new MetricOverrides { IsVisible = false });

        string json = JsonSerializer.Serialize(
            source,
            SettingsJsonContext.Default.WidgetSettings);
        WidgetSettings? deserialized = JsonSerializer.Deserialize(
            json,
            SettingsJsonContext.Default.WidgetSettings);
        WidgetSettings result = SettingsValidator.Normalize(deserialized);

        Assert.Equal(WidgetLayout.Horizontal, result.Layout.Direction);
        Assert.Equal("•", result.Layout.HorizontalSeparator);
        Assert.Equal(0.5, result.Appearance.BackgroundOpacity);
        Assert.Equal("Arial", result.Appearance.FontFamily);
        Assert.Equal(24, result.Appearance.FontSize);
        Assert.Equal(WidgetFontWeight.Bold, result.Appearance.FontWeight);
        Assert.Equal(
            WidgetHorizontalAlignment.Right,
            result.Appearance.HorizontalTextAlignment);
        Assert.Equal(
            WidgetVerticalAlignment.Bottom,
            result.Appearance.VerticalTextAlignment);
        Assert.False(
            Assert.Single(result.MetricOverrides).Value.IsVisible);
    }
}
