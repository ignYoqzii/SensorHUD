# Extending SensorHUD

[← Back to the README](../README.md)

SensorHUD is intentionally explicit and registry-driven. It does not load
plugins, scan assemblies, or generate behavior through reflection. A
contributor adds a small typed declaration and publishes data; the shared
settings and telemetry pipelines do the rest.

In the common case, adding telemetry requires only:

1. Add the metric ID and metadata to
   [`MetricRegistry`](../SensorHUD.Core/Metrics/MetricRegistry.cs).
2. Publish a
   [`MetricReading`](../SensorHUD.Core/Telemetry/MetricReading.cs) from the
   collector.

No category-specific settings XAML or rendering code is required.

## Choose an extension

| Goal | Required work |
| --- | --- |
| Metric in an existing category | Add its ID and definition, then publish its reading |
| Metric for every detected device | Use `MetricScope.PerDevice` and publish a stable device ID |
| New category | Add its enum member, category metadata, metrics, and readings |
| Independent data source | Implement `ITelemetryProvider` and register it explicitly |
| Widget-wide preference | Extend one settings section from core model through XAML |

## Architecture and terminology

Four concepts have deliberately separate responsibilities:

| Concept | Purpose | Source of truth |
| --- | --- | --- |
| Widget setting | Layout or appearance shared by the complete widget | `SensorHUD.Core/Settings` |
| Metric definition | Identity, category, scope, display defaults, and ordering | `MetricRegistry` |
| Metric reading | One current numeric value and optional device identity or error | Collector provider |
| Metric override | Optional user changes relative to a metric definition | `WidgetSettings.MetricOverrides` |

A **global metric** has one system-wide instance. A **per-device metric** has
one instance and one override key per detected device. Neither term means a
widget-wide setting.

```text
MetricRegistry ───────┐
                     ├─> TelemetryPresenter ─> TelemetryRenderer
collector readings ──┤
metric overrides ────┘

settings XAML <─> section view models ─> SettingsValidator
                                           ├─> immediate preview
                                           └─> debounced atomic settings.json
```

### Important ownership rules

- Providers publish data; they do not define formatting or settings.
- `MetricRegistry` defines metric defaults; it does not collect data.
- View models adapt typed core settings for XAML; they do not perform file I/O.
- `SettingsValidator` is the boundary for loaded and edited settings.
- `WidgetSettingsStore` owns the single durable settings file.
- `SettingsAutoSaver` owns preview, debounce, ordering, and final flush.

## Metric registry

Every category has one `MetricCategoryDefinition`.

| Property | Purpose |
| --- | --- |
| `Id` | Strongly typed category identity |
| `Name` | Heading displayed in settings |
| `Description` | Optional text below the heading |
| `SortOrder` | Display position; lower values appear first |

Every metric has one `MetricDefinition`.

| Property | Required | Purpose |
| --- | --- | --- |
| `Id` | Yes | Stable provider and settings identity |
| `Category` | Yes | Category containing the metric |
| `Name` | Yes | Settings name and `{name}` value |
| `Unit` | Yes | `{unit}` value; use an empty string for no unit |
| `Format` | Yes | Default overlay format |
| `Decimals` | Yes | Default decimal count |
| `TextColor` | Yes | Default ARGB color for text, `{device}`, and `{name}` |
| `ValueUnitColor` | Yes | Default ARGB color for `{value}` and `{unit}` |
| `SortOrder` | Yes | Position inside the category |
| `IsVisibleByDefault` | No | Fresh-install visibility; defaults to `true` |
| `Scope` | No | `Global` or `PerDevice`; defaults to `Global` |

`MetricRegistry` builds immutable ID and category/scope indexes once. It also
checks duplicate IDs and sort orders, missing categories, decimal ranges, and
default colors during initialization. Consumers should use `TryGet`,
`GetCategory`, and `GetMetrics` rather than reimplementing registry queries.
The supported decimal range lives with metric metadata in
`MetricDisplayConstraints`.

### Format tokens

| Token | Replaced with |
| --- | --- |
| `{name}` | Metric `Name` |
| `{value}` | Reading formatted with the effective decimal count |
| `{unit}` | Metric `Unit` |
| `{device}` | Provider-supplied device name, or category name as fallback |

## Add a global metric

This example adds CPU power.

### 1. Add its stable ID and definition

Add both in `MetricRegistry`:

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
    TextColor = "#FFFFFFFF",
    ValueUnitColor = "#FFFFFFFF",
    IsVisibleByDefault = false,
    SortOrder = 2,
},
```

The default scope is `MetricScope.Global`.

### 2. Publish its reading

Add the value to the category's existing reader or provider:

```csharp
readings.Add(new MetricReading
{
    MetricId = MetricRegistry.CpuPower,
    DeviceName = "CPU",
    Value = watts,
});
```

The category card, metric editor, persistence, formatting, ordering, and
overlay output update automatically.

## Add a per-device metric

This example adds fan speed for every detected GPU:

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
    TextColor = "#FFFFFFFF",
    ValueUnitColor = "#FFFFFFFF",
    SortOrder = 5,
    Scope = MetricScope.PerDevice,
},
```

Every reading must include device identity:

```csharp
readings.Add(new MetricReading
{
    MetricId = MetricRegistry.GpuFanSpeed,
    DeviceId = stableDeviceId,
    DeviceName = gpu.Name,
    Value = fanRpm,
});
```

- `DeviceId` is durable identity and must remain stable across samples and
  restarts.
- `DeviceName` is a user-facing label and may change.
- A settings card appears after a reading supplies that device ID.
- LibreHardwareMonitor readers should use `SensorLookup.StableDeviceId`.

The durable key is `metricId@deviceId`. Global metrics use only `metricId`.
Providers never construct these keys; `MetricInstanceKey` owns that rule.

## Add a category

To create a Storage category:

1. Add `Storage` to the `MetricCategory` enum.
2. Add its metadata to `CategoryDefinitions`.
3. Add one or more definitions using `MetricCategory.Storage`.
4. Publish their readings.

```csharp
new()
{
    Id = MetricCategory.Storage,
    Name = "Storage",
    Description = "Drive activity, capacity, and health.",
    SortOrder = 500,
},
```

Scope belongs to each metric, not its category:

| Category contents | Generated settings |
| --- | --- |
| Global metrics only | One category card |
| Per-device metrics only | One card per detected device |
| Mixed scopes | One global card plus one card per device |

## Publish unavailable data

Expected unavailable data should still be a reading containing:

- Its registered metric ID.
- Device identity when applicable.
- A `null` value.
- A concise explanatory error.

Reserve provider exceptions for unexpected failures. `TelemetrySampler`
isolates providers so one failure does not suppress independent sources.

## Add a telemetry provider

Use a provider for an independent data source:

1. Implement `ITelemetryProvider` under `SensorHUD.Collector/Sampling`.
2. Keep `Sample` short and non-blocking.
3. Publish expected unavailable states as readings.
4. Implement `IDisposable` when owning handles, timers, or background work.
5. Construct it explicitly in `TelemetrySampler.CreateDefault`.

Explicit registration is intentional. It is easy to trace, friendly to
Native AOT, and avoids hidden reflection or assembly scanning.

When data already comes from the shared LibreHardwareMonitor `Computer`,
prefer a focused reader called by `HardwareMetricsProvider`. This preserves
one hardware update and enumeration pass. Sources that do not require that
shared computer should remain independent providers.

## Settings architecture

The root model is intentionally small:

```text
WidgetSettings
├── Layout      -> LayoutSettings
├── Appearance  -> AppearanceSettings
└── MetricOverrides[MetricInstanceKey]
```

Files have one clear role:

| File | Responsibility |
| --- | --- |
| `WidgetSettings.cs` | Root composition |
| `LayoutSettings.cs` | Layout model and enum |
| `AppearanceSettings.cs` | Appearance model and enums |
| `MetricOverrides.cs` | Optional differences from registry defaults |
| `SettingsDefaults.cs` | Defaults, supported ranges, and save debounce |
| `SettingsValidator.cs` | Deep-copy normalization and override cleanup |
| `*SettingsViewModel.cs` | Bindable editor state for one section |
| `SettingsWidgetPage.xaml` | Explicit widget-setting controls |
| `WidgetSettingsStore.cs` | Validated load and atomic save |
| `SettingsAutoSaver.cs` | Preview, debounce, ordered save, and final flush |

Only differences from metric registry defaults are persisted. Null override
properties inherit the registry. If every property matches its default, the
complete metric override entry is removed. This keeps defaults authoritative
and the JSON compact.

The installed settings file is:

```text
%LOCALAPPDATA%\Packages\<SensorHUD package>\LocalState\settings.json
```

## Add a widget setting

Do not add metric-specific behavior as a widget setting. Metrics remain
registry-driven.

For a layout or appearance preference:

1. Add the typed property to `LayoutSettings` or `AppearanceSettings`.
2. Add its default or supported range to `SettingsDefaults`.
3. Normalize it in the matching `SettingsValidator` section method.
4. Add bindable state and `ApplyTo` mapping in the matching view model.
5. Add one compiled `x:Bind` control to `SettingsWidgetPage.xaml`.
6. Apply the setting in the telemetry widget or renderer.

Use an existing shared `Settings*Style` from `SettingsStyles.xaml`. Keep
specialized control behavior in a focused view model rather than weakening
the core model with XAML types.

This path is intentionally explicit. Widget settings can require different
control types and runtime behavior; a generic dynamic-form system would be
harder to understand and debug.

## Durable identity rules

- Never change a released metric ID.
- Never reuse an old metric ID for different data.
- Keep per-device IDs stable.
- Category enum values are not persisted.
- Providers publish base metric IDs, never preference keys.
- Registry defaults are not copied into settings unless the user changes
  them.

## Validate the change

1. Run the core tests:

   ```powershell
   dotnet test SensorHUD.Core.Tests\SensorHUD.Core.Tests.csproj -c Debug -p:Platform=x64
   ```

2. Build `Debug|x64` with Visual Studio MSBuild so the UWP XAML compiler runs.
3. Build `SensorHUD.Collector` in `Release|x64`.
4. Exercise expected unavailable and provider-failure behavior.
5. Test at least two devices for per-device metrics.
6. Run `git diff --check`.

The core tests verify registry identity and grouping, metric-instance keys,
override normalization, and compact settings serialization.
