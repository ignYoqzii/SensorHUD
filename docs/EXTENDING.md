# Extending SensorHUD

[← Back to the README](../README.md)

SensorHUD generates its metric settings and overlay presentation from shared
metadata. In most cases, adding telemetry requires only two changes:

1. Describe the metric in
   [`MetricRegistry`](../SensorHUD.Core/Metrics/MetricRegistry.cs).
2. Publish a
   [`MetricReading`](../SensorHUD.Core/Telemetry/MetricReading.cs) from the
   collector.

No category-specific settings XAML is required.

## Contents

- [Choose an extension](#choose-an-extension)
- [Category metadata](#category-metadata)
- [Metric metadata](#metric-metadata)
- [Add a global metric](#add-a-global-metric)
- [Add a per-device metric](#add-a-per-device-metric)
- [Add a category](#add-a-category)
- [Choose category behavior](#choose-category-behavior)
- [Add a telemetry provider](#add-a-telemetry-provider)
- [Add a global setting](#add-a-global-setting)
- [Compatibility rules](#compatibility-rules)
- [Validate the change](#validate-the-change)

## Choose an extension

| Goal | Required work |
| --- | --- |
| Add a metric to an existing category | Add its ID and definition, then publish its reading |
| Add a metric for every detected device | Do the above with `IsPerDevice = true` and stable device identity |
| Add a category | Add one enum member, category metadata, metric definitions, and readings |
| Add a new data source | Implement `ITelemetryProvider` and register it in `TelemetrySampler` |
| Add a widget-wide preference | Extend the settings model, validation, view model, and XAML |

## Category metadata

Every category has one `MetricCategoryDefinition`.

| Property | Purpose |
| --- | --- |
| `Id` | Strongly typed category identity used by metric definitions |
| `Name` | Heading displayed in settings |
| `Description` | Optional text directly below the heading; use `null` to omit it |
| `SortOrder` | Display position; lower values appear first |

```csharp
new()
{
    Id = MetricCategory.Cpu,
    Name = "CPU",
    Description = "Processor utilization and temperature.",
    SortOrder = 100,
},
```

For a per-device category, the name and description are shared metadata.
SensorHUD appends the provider-supplied device name to each generated card:

```text
GPU - NVIDIA GeForce RTX ...
GPU - AMD Radeon Graphics
```

Larger gaps between category sort orders make inserting a future category
possible without renumbering the existing categories.

## Metric metadata

Every metric has one `MetricDefinition`.

| Property | Required | Purpose |
| --- | --- | --- |
| `Id` | Yes | Stable provider and settings identity |
| `Category` | Yes | Category containing the metric |
| `Name` | Yes | Name in settings and value of `{name}` |
| `Unit` | Yes | Value of `{unit}`; use an empty string for no unit |
| `Format` | Yes | Default overlay format |
| `Decimals` | Yes | Default number of decimal places |
| `SortOrder` | Yes | Position inside the category |
| `IsVisibleByDefault` | No | Whether a fresh configuration shows it; defaults to `true` |
| `IsPerDevice` | No | Whether every detected device has an independent reading and preference |

### Format tokens

| Token | Replaced with |
| --- | --- |
| `{name}` | Metric `Name` |
| `{value}` | Reading formatted with the selected decimals |
| `{unit}` | Metric `Unit` |
| `{device}` | Provider-supplied device name, or the category name as a fallback |

The settings widget offers the registry default and zero, one, or two decimal
places. The supported range is defined by `MinimumDecimals` and
`MaximumDecimals` in `SettingsDefaults`.

## Add a global metric

This example adds CPU power to the existing CPU category.

### 1. Add the stable ID and definition

Add the ID near the top of `MetricRegistry`, then add its definition to
`OrderedDefinitions`:

```csharp
public const string CpuPower = "cpu.power";

new()
{
    Id = CpuPower,
    Category = MetricCategory.Cpu,
    Name = "Power",
    Unit = "W",
    Format = "{device} Power: {value} {unit}",
    Decimals = 1,
    IsVisibleByDefault = false,
    SortOrder = 2,
},
```

### 2. Publish the reading

Add the value from the category's existing reader or provider:

```csharp
readings.Add(new MetricReading
{
    MetricId = MetricRegistry.CpuPower,
    DeviceName = "CPU",
    Value = watts,
});
```

The CPU category and format editor update automatically.

## Add a per-device metric

This example adds an independent fan-speed metric for every detected GPU.

### 1. Add a per-device definition

```csharp
public const string GpuFanSpeed = "gpu.fanSpeed";

new()
{
    Id = GpuFanSpeed,
    Category = MetricCategory.Gpu,
    Name = "Fan Speed",
    Unit = "RPM",
    Format = "{device} Fan: {value} {unit}",
    Decimals = 0,
    SortOrder = 5,
    IsPerDevice = true,
},
```

### 2. Include device identity in every reading

```csharp
readings.Add(new MetricReading
{
    MetricId = MetricRegistry.GpuFanSpeed,
    DeviceId = stableDeviceId,
    DeviceName = gpu.Name,
    Value = fanRpm,
});
```

- `DeviceId` is the durable settings identity. It must remain stable across
  samples and restarts.
- `DeviceName` is the user-facing label and may change.

SensorHUD creates one settings card and one saved preference per device. A
card appears after the collector publishes a reading with that device ID.
LibreHardwareMonitor readers should use `SensorLookup.StableDeviceId`.

## Add a category

This example creates a Storage category.

### 1. Add the category identity

Add a member to `MetricCategory`:

```csharp
public enum MetricCategory
{
    // Existing categories...
    Storage,
}
```

### 2. Add category metadata

Add a definition to `CategoryDefinitions` in `MetricRegistry`:

```csharp
new()
{
    Id = MetricCategory.Storage,
    Name = "Storage",
    Description = "Drive activity, capacity, and health.",
    SortOrder = 500,
},
```

### 3. Add metrics and readings

Add one or more metric definitions assigned to `MetricCategory.Storage`, then
publish their readings. SensorHUD generates the category heading,
description, settings cards, ordering, and overlay output automatically.

## Choose category behavior

Scope belongs to each metric rather than to the category.

| Behavior | Definitions | Generated settings |
| --- | --- | --- |
| Global | Every metric has `IsPerDevice = false` | One category card |
| Per-device | Every metric has `IsPerDevice = true` | One card per detected device |
| Mixed | Combine global and per-device metrics | One global card plus one card per device |

For example, Storage can expose global `Total Activity` and per-drive
`Temperature` without special mixed-category code.

Expected unavailable data should still be published with:

- Its metric ID.
- Device identity when applicable.
- A `null` value.
- A concise explanatory error.

Reserve provider exceptions for unexpected failures.

## Add a telemetry provider

Use a provider for an independent data source:

1. Create a focused `ITelemetryProvider` under
   `SensorHUD.Collector/Sampling`.
2. Keep `Sample` short and non-blocking. Run slower I/O in the background and
   expose only its latest bounded result.
3. Return unavailable readings for expected startup, permission, hardware, or
   connectivity states.
4. Implement `IDisposable` when the provider owns sessions, timers, handles,
   or background resources.
5. Construct and register it in `TelemetrySampler.CreateDefault`.

An unexpected provider exception is reported through collector health without
suppressing independent providers.

When data already comes from the shared LibreHardwareMonitor `Computer`,
prefer a focused reader called by `HardwareMetricsProvider`. This preserves
one hardware update and enumeration pass. Readers own their fallback
readings; do not duplicate metric lists in the provider. Sources that do not
need LibreHardwareMonitor, such as Windows physical-memory status, should
remain independent of it.

## Add a global setting

1. Add the model property, default, and validation rule under
   `SensorHUD.Core/Settings`.
2. Expose it through the focused layout or appearance view model.
3. Add its compiled `x:Bind` control to `SettingsWidgetPage.xaml`.

Metric-specific settings should remain registry-driven instead of becoming
global properties or category-specific XAML.

## Compatibility rules

- Keep released metric IDs unchanged; they are durable settings identities.
- Keep released per-device IDs stable.
- Category enum values are not persisted.
- Change a metric's registry `Format` to change its default. Existing saved
  custom formats remain unchanged until the user resets them.

## Validate the change

1. Build the complete `Debug|x64` solution with Visual Studio MSBuild. This
   validates the UWP frontend and generated XAML.
2. Build `SensorHUD.Collector` in `Release|x64`.
3. Test unavailable data and provider failure behavior.
4. For per-device metrics, test at least two devices and verify that their
   settings remain independent.
5. Run `git diff --check` before submitting the change.
