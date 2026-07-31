# Extending SensorHUD

[← Back to the README](../README.md)

SensorHUD is explicit, registry-driven, and intentionally small at its
extension points. It does not scan assemblies, discover plugins, or generate
settings UI through reflection.

Most telemetry changes require two edits:

1. Define the metric in the matching `MetricRegistry.*.cs` file.
2. Declare and publish it from the provider or LibreHardwareMonitor mapper
   that owns the raw data.

The shared settings and presentation pipelines handle cards, persistence,
formatting, ordering, placeholders, and rendering. A normal metric addition
does not require XAML or category-specific frontend code.

> [!TIP]
> Start with [Choose the right extension path](#choose-the-right-extension-path).
> Each row links to a complete recipe and names the files that should change.

## Choose the right extension path

| Goal | Use | Typical files changed |
| --- | --- | --- |
| Add an LHM metric to CPU, GPU, or NIC mapping | [Existing LHM mapper](#add-a-metric-to-an-existing-lhm-mapper) | One registry partial + one mapper |
| Add a metric to Windows memory, DXG ETW, ICMP, or another existing source | [Existing provider](#add-a-global-metric-to-an-existing-provider) | One registry partial + one provider |
| Add one metric for every detected device | [Per-device metric](#add-a-per-device-metric) | One registry partial + its mapper/provider |
| Add a new LHM hardware grouping | [New LHM mapper](#add-a-new-lhm-sensor-mapper) | Registry + snapshot + mapper + one mapper registration |
| Add a settings category | [New category](#add-a-category) | Category enum/metadata + metric definitions + source output |
| Add an independent raw-data source | [New provider](#add-an-independent-provider) | Registry + provider + one provider registration |
| Add a widget-wide layout or appearance option | [Widget setting](#add-a-widget-setting) | Core settings + view model + XAML + consumer |

If a change appears to require more files than its row suggests, pause and
check the ownership rules below. Extra edits often mean presentation metadata
or source acquisition has leaked into the wrong layer.

## The mental model

SensorHUD keeps five concepts separate:

| Concept | Meaning | Owner |
| --- | --- | --- |
| Metric definition | Stable identity, category, scope, defaults, and ordering | `MetricRegistry` |
| Metric instance | A detected device slot for a per-device metric | Collector source |
| Metric reading | A numeric value available in the current sample | Collector source |
| Metric override | User changes for one global or per-device metric slot | `WidgetSettings.MetricOverrides` |
| Widget setting | Layout or appearance shared by the whole widget | `SensorHUD.Core/Settings` |

The data flow is:

```text
raw source
    |
    v
provider or LHM mapper
    |
    +-- MetricInstance declarations ----+
    +-- available MetricReading values --+--> TelemetrySnapshot
                                              |
MetricRegistry + MetricOverrides -------------+--> TelemetryPresenter
                                                    |
                                                    v
                                              stable render slots
```

The settings flow is separate:

```text
settings XAML
    <-> section view models
    <-> SettingsValidator
    <-> immediate preview
    <-> debounced, atomic settings.json save
```

### Provider, category, and mapper are different things

A **provider** owns one independent raw-data source. A provider may publish
metrics into several UI categories.

A **category** is a presentation grouping. It may combine metrics from several
providers.

A LibreHardwareMonitor **mapper** owns the mapping for one hardware grouping
inside the single shared LHM provider. It does not own the live LHM
`Computer`.

The built-in sources demonstrate these relationships:

| Source provider | Categories supplied | Internal mapping |
| --- | --- | --- |
| LibreHardwareMonitor | CPU, GPU, Network | CPU, GPU, and NIC mappers |
| Windows system-memory API | Memory | Direct provider mapping |
| DXG presentation ETW | Frame Rate | Direct provider mapping over a bounded capture window |
| ICMP probe loop | Network | Direct provider mapping over a bounded probe window |

> [!IMPORTANT]
> Categories are never provider registrations. Do not create one provider per
> category merely because the settings UI groups metrics that way.

### Global and per-device scope

`MetricScope.Global` means there is one system-wide slot. Examples include
CPU usage, memory usage, FPS, and Internet ping.

`MetricScope.PerDevice` means there can be zero or more slots, each identified
by a stable `DeviceId`. GPU metrics use this scope.

Scope controls three things:

- Which sink method publishes the value.
- Whether a `MetricInstance` declaration is required.
- How settings keys are formed.

Global settings keys are the metric ID:

```text
cpu.usage
```

Per-device keys combine metric and device identity:

```text
gpu.usage@A1B2C3D4E5
```

Providers publish base metric IDs and device IDs. They never construct
settings keys; [`MetricInstanceKey`](../SensorHUD.Core/Metrics/MetricInstanceKey.cs)
owns that rule.

### Missing data is normal

A `MetricReading` exists only when a finite numeric value is available in the
current sample. Do not publish nulls, placeholder values, sentinels, or error
objects.

The frontend handles structure separately from values:

- Every enabled global registry metric has a stable render slot.
- A per-device slot exists after the collector declares its `MetricInstance`.
- A slot without a current reading displays `N/A`.
- When a reading returns, the existing XAML element is updated in place.

For per-device metrics, declaration and publication are deliberately
different operations:

```csharp
sink.DeclareDevice(metricId, deviceId, deviceName);
sink.PublishDevice(metricId, deviceId, deviceName, value);
```

Declare the device even when its sensor has no current value. That keeps the
settings card and overlay slot stable through temporary sensor gaps.

## Core contracts

### Metric provider

The shared provider contract is defined in
[`IMetricProvider.cs`](../SensorHUD.Core/Telemetry/IMetricProvider.cs):

```csharp
public interface IMetricProvider
{
    IReadOnlyList<ProvidedMetricDefinition> Metrics { get; }

    void Sample(IMetricSampleSink sink);
}
```

Every provider declares the metric IDs and scopes it can publish:

```csharp
private static readonly ProvidedMetricDefinition[] Outputs =
[
    ProvidedMetricDefinition.Global(MetricRegistry.Ping),
    ProvidedMetricDefinition.Global(MetricRegistry.PacketLoss),
];

public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;
```

The collector validates declarations against the registry:

- The metric ID must exist.
- The declared scope must match the registry scope.
- A provider cannot declare the same metric twice.
- Two providers cannot claim the same metric.
- A provider cannot publish an undeclared metric.
- A sample cannot publish the same metric slot twice.
- Published values must be finite.

`Sample` is synchronous and should finish quickly. A source that requires
asynchronous work should own bounded background state and copy its latest
result during `Sample`. DXG ETW and ICMP are the built-in examples.

### Sample sink

Use the method that matches the registry scope:

| Method | Purpose |
| --- | --- |
| `PublishGlobal` | Publish one currently available global value |
| `DeclareDevice` | Declare a stable per-device slot independently of value availability |
| `PublishDevice` | Declare the device if needed and publish its current value |

The collector creates a fresh buffered sink for each provider sample. Output
is committed only after the provider completes successfully. An unexpected
provider exception therefore discards that provider's batch without
suppressing other providers.

### LibreHardwareMonitor mapper

The mapper contract is defined in
[`ILibreHardwareMonitorSensorMapper.cs`](../SensorHUD.Collector/Sampling/LibreHardwareMonitor/ILibreHardwareMonitorSensorMapper.cs):

```csharp
internal interface ILibreHardwareMonitorSensorMapper
{
    IReadOnlyList<ProvidedMetricDefinition> Metrics { get; }

    void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink);
}
```

A mapper owns:

- The LHM-derived metrics for one hardware grouping.
- Its sensor-name preferences.
- Its device declarations.
- Conversion from captured sensor units to registry metric units.

A mapper does not:

- Open or close an LHM `Computer`.
- Update live hardware.
- Enumerate the live LHM tree.
- Define UI categories or formatting.
- Know about settings keys, JSON, transport, or XAML.

## Metric registry

[`MetricRegistry`](../SensorHUD.Core/Metrics/MetricRegistry.cs) is the source
of truth for metric identity and presentation metadata. Definitions live in
focused partial files:

| File | Metrics |
| --- | --- |
| `MetricRegistry.FrameRate.cs` | FPS, 1% low, frametime |
| `MetricRegistry.Cpu.cs` | CPU metrics |
| `MetricRegistry.Gpu.cs` | Per-GPU metrics |
| `MetricRegistry.Memory.cs` | Physical-memory metrics |
| `MetricRegistry.NetworkAdapter.cs` | NIC throughput |
| `MetricRegistry.InternetPath.cs` | Ping and packet loss |

The root registry flattens those definitions into immutable indexes. It
validates duplicate IDs, duplicate sort orders, category references, decimal
ranges, and default colors during initialization.

### Category metadata

Every category has one `MetricCategoryDefinition`:

| Property | Meaning |
| --- | --- |
| `Id` | Strongly typed `MetricCategory` value |
| `Name` | User-facing settings heading |
| `Description` | Optional supporting text |
| `SortOrder` | Relative category position; lower values appear first |

### Metric metadata

Every metric has one `MetricDefinition`:

| Property | Required | Meaning |
| --- | --- | --- |
| `Id` | Yes | Durable provider and settings identity |
| `Category` | Yes | UI category containing the metric |
| `Name` | Yes | Settings label and `{name}` token |
| `Unit` | Yes | `{unit}` token; use `string.Empty` for no unit |
| `Format` | Yes | Default overlay format |
| `Decimals` | Yes | Default number of decimal places |
| `TextColor` | Yes | ARGB color for literal text, `{device}`, and `{name}` |
| `ValueUnitColor` | Yes | ARGB color for `{value}` and `{unit}` |
| `SortOrder` | Yes | Relative position inside the category |
| `IsVisibleByDefault` | No | Initial visibility; defaults to `true` |
| `Scope` | No | `Global` or `PerDevice`; defaults to `Global` |

Metric IDs are durable. Once a build is distributed, do not rename an ID or
reuse it for different data.

### Format tokens

| Token | Output |
| --- | --- |
| `{name}` | Metric `Name` |
| `{value}` | Current value using the effective decimal count, or `N/A` |
| `{unit}` | Metric `Unit` |
| `{device}` | Device label, or the category name when no label is available |

Formatting is presentation-only. Providers publish raw numeric values in the
unit documented by the registry definition.

## Add a metric to an existing LHM mapper

Use this path for a new CPU, GPU, or NIC metric derived from data already
present in `LibreHardwareMonitorSnapshot`.

This example adds global CPU package power.

### Files to change

- `SensorHUD.Core/Metrics/MetricRegistry.Cpu.cs`
- `SensorHUD.Collector/Sampling/LibreHardwareMonitor/CpuSensorMapper.cs`

Do not edit `LibreHardwareMonitorMetricProvider`.

### 1. Add registry metadata

In `MetricRegistry.Cpu.cs`, add the ID and definition:

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

Choose a unique sort order within the CPU category. Scope is omitted because
CPU power is global.

### 2. Declare the mapper output

In `CpuSensorMapper.cs`, add the metric to `Outputs`:

```csharp
private static readonly ProvidedMetricDefinition[] Outputs =
[
    ProvidedMetricDefinition.Global(MetricRegistry.CpuUsage),
    ProvidedMetricDefinition.Global(MetricRegistry.CpuTemperature),
    ProvidedMetricDefinition.Global(MetricRegistry.CpuPower),
];
```

This declaration lets the sink validate the mapper's output.

### 3. Map only an available value

Keep sensor preferences and mapping in the same file:

```csharp
private static readonly string[] PackagePowerNames =
    ["CPU Package", "Package"];

double? packagePower = cpu.FindFirstValue(
    SensorType.Power,
    PackagePowerNames,
    allowTypeFallback: false);

if (packagePower is double watts)
{
    sink.PublishGlobal(
        MetricRegistry.CpuPower,
        watts,
        cpu.Name);
}
```

If no package-power sensor exists, publish nothing. The enabled global slot
will still render as `N/A`.

Use `allowTypeFallback: false` when another sensor of the same `SensorType`
would represent different data. For example, a per-core power sensor must not
be reported as package power, and upload throughput must not be reported as
download throughput. Allow the generic type fallback only when any sensor of
that type is a valid representation of the metric.

That is the complete change. Settings, persistence, formatting, ordering, and
overlay rendering are registry-driven.

## Add a global metric to an existing provider

Use this path when an existing non-LHM provider already owns the raw value.

### Files to change

- The focused `MetricRegistry.*.cs` file for the metric.
- The source provider that owns the value.

### Steps

1. Add the stable ID and `MetricDefinition`.
2. Add `ProvidedMetricDefinition.Global(metricId)` to the provider's
   `Outputs`.
3. Publish only when a finite value is available:

   ```csharp
   if (value is double available)
   {
       sink.PublishGlobal(
           MetricRegistry.ExampleMetric,
           available,
           "Example source");
   }
   ```

4. Add or update focused tests for registry shape and any pure calculation
   logic.

Do not add frontend fallback readings. The presenter already maintains the
global slot and displays `N/A` when no reading arrives.

## Add a per-device metric

Use this path when every detected device should have independent visibility,
formatting, and saved preferences.

This example adds fan speed for every detected GPU.

### Files to change

- `SensorHUD.Core/Metrics/MetricRegistry.Gpu.cs`
- `SensorHUD.Collector/Sampling/LibreHardwareMonitor/GpuSensorMapper.cs`

### 1. Define a per-device metric

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
    IsVisibleByDefault = false,
    SortOrder = 5,
    Scope = MetricScope.PerDevice,
},
```

### 2. Declare the mapper output

Add this entry to `GpuSensorMapper.Outputs`:

```csharp
ProvidedMetricDefinition.PerDevice(MetricRegistry.GpuFanSpeed),
```

### 3. Declare every detected device

Declare the slot before checking whether the fan sensor has a value:

```csharp
sink.DeclareDevice(
    MetricRegistry.GpuFanSpeed,
    gpu.DeviceId,
    gpu.Name);
```

### 4. Publish the current value when available

```csharp
if (gpu.FindFirstValue(
        SensorType.Fan,
        FanSpeedNames) is double rpm)
{
    sink.PublishDevice(
        MetricRegistry.GpuFanSpeed,
        gpu.DeviceId,
        gpu.Name,
        rpm);
}
```

### Identity requirements

- `DeviceId` is durable identity. It must be non-empty and stable across
  samples and process restarts.
- `DeviceName` is a presentation label. It may change without losing settings.
- The declaration and reading must use the same metric ID and device ID.
- Declare the device on every sample in which it is detected.
- Never use a list index or enumeration order as identity.

For LHM devices, stable identity is created from the hardware identifier
during shared snapshot capture.

## Shared LibreHardwareMonitor access

[`LibreHardwareMonitorMetricProvider`](../SensorHUD.Collector/Sampling/LibreHardwareMonitor/LibreHardwareMonitorMetricProvider.cs)
is the only owner of the LHM `Computer`.

Each successful sample performs:

1. One live hardware update traversal.
2. One capture into a cycle-local `LibreHardwareMonitorSnapshot`.
3. One mapping pass over that snapshot for each registered mapper.

This design prevents CPU, GPU, and NIC mapping from updating or enumerating
the live tree independently.

The mapper list is intentionally explicit:

```csharp
private readonly ILibreHardwareMonitorSensorMapper[] _mappers =
[
    new CpuSensorMapper(),
    new GpuSensorMapper(),
    new NicThroughputSensorMapper(),
];
```

Adding a metric to an existing grouping changes only that grouping's mapper.
The provider list changes only when a genuinely new hardware grouping gets a
new mapper.

Failure boundaries are layered:

- If LHM cannot open or capture its tree, that LHM sample produces no output.
- If one mapper throws after capture, the other mappers still run.
- `GpuSensorMapper` isolates each adapter, so one malformed adapter cannot
  suppress other GPUs.
- The outer provider boundary keeps LHM failures from suppressing Windows
  memory, ICMP, or frame capture.

## Add a new LHM sensor mapper

Use a new mapper when the raw data comes from LHM but does not belong to CPU,
GPU, or NIC mapping—for example, a new storage-device grouping.

### Files to change

- A focused registry partial for the new metrics.
- `LibreHardwareMonitorSnapshot.cs`.
- A new `<Grouping>SensorMapper.cs`.
- `LibreHardwareMonitorMetricProvider.cs`, for one mapper registration.

If the metrics use a new UI category, also follow
[Add a category](#add-a-category).

### 1. Capture the new hardware grouping

Extend `LibreHardwareMonitorSnapshot` with a focused device collection and
populate it during `Capture`. The snapshot should contain only plain captured
values:

```csharp
internal sealed record LibreHardwareMonitorSnapshot(
    LibreHardwareDeviceSnapshot? Cpu,
    IReadOnlyList<LibreHardwareDeviceSnapshot> Gpus,
    IReadOnlyList<LibreHardwareDeviceSnapshot> NetworkAdapters,
    IReadOnlyList<LibreHardwareDeviceSnapshot> StorageDevices);
```

Reuse `CaptureDevice` so stable device identity and recursive sensor capture
remain centralized. Do not expose the live `IHardware` object to a mapper.

### 2. Implement the mapper

```csharp
internal sealed class StorageSensorMapper :
    ILibreHardwareMonitorSensorMapper
{
    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.PerDevice(
            MetricRegistry.StorageTemperature),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Map(
        LibreHardwareMonitorSnapshot snapshot,
        IMetricSampleSink sink)
    {
        foreach (LibreHardwareDeviceSnapshot storage in
                 snapshot.StorageDevices)
        {
            sink.DeclareDevice(
                MetricRegistry.StorageTemperature,
                storage.DeviceId,
                storage.Name);

            if (storage.FindFirstValue(
                    SensorType.Temperature,
                    TemperatureNames) is double value)
            {
                sink.PublishDevice(
                    MetricRegistry.StorageTemperature,
                    storage.DeviceId,
                    storage.Name,
                    value);
            }
        }
    }
}
```

Keep outputs, sensor preferences, conversions, declarations, and mapping in
this file.

### 3. Register the mapper once

Add one entry to the provider's mapper list:

```csharp
new StorageSensorMapper(),
```

The provider automatically aggregates the mapper's declared metrics and calls
it for every captured snapshot.

## Add a category

A category is settings and presentation metadata, not a data source.

This example adds Storage.

### 1. Add the enum member

Add `Storage` to `MetricCategory`.

Category enum values are not persisted, but keep the declaration readable and
stable.

### 2. Add category metadata

Add one `MetricCategoryDefinition` to the root registry:

```csharp
new()
{
    Id = MetricCategory.Storage,
    Name = "Storage",
    Description = "Drive activity, temperature, and capacity.",
    SortOrder = 500,
},
```

Use a unique category sort order.

### 3. Add focused metric definitions

Create `MetricRegistry.Storage.cs`, define stable IDs, and return its
`MetricDefinition` entries from a focused factory. Add that factory to the
explicit flattened list in `MetricRegistry`.

### 4. Publish the values

Use an existing provider or mapper when it already owns the raw source.
Create a new provider only for an independent source.

Scope belongs to each metric, not to the category:

| Category contents | Generated settings |
| --- | --- |
| Global metrics only | One category card |
| Per-device metrics only | One card for each declared device |
| Mixed scopes | One global card plus one card for each declared device |

A category may combine metrics from several providers without changing its
settings or renderer implementation.

## Add an independent provider

Create a provider only when the data comes from a genuinely independent
source with its own acquisition and lifetime.

> [!IMPORTANT]
> Do not create another LHM `Computer`. Add LHM-derived data through the
> shared snapshot and a sensor mapper.

### Files to change

- One or more focused registry partials.
- A source-named directory under `SensorHUD.Collector/Sampling`.
- One provider implementation.
- `TelemetrySampler.cs`, for one guarded registration.

### 1. Add registry metadata

Define every metric before the provider claims it. If needed, add a category
using the preceding recipe.

### 2. Implement `IMetricProvider`

```csharp
internal sealed class StorageMetricProvider :
    IMetricProvider,
    IDisposable
{
    private static readonly ProvidedMetricDefinition[] Outputs =
    [
        ProvidedMetricDefinition.PerDevice(
            MetricRegistry.StorageReadRate),
    ];

    public IReadOnlyList<ProvidedMetricDefinition> Metrics => Outputs;

    public void Sample(IMetricSampleSink sink)
    {
        foreach (StorageDevice device in CaptureDevices())
        {
            sink.DeclareDevice(
                MetricRegistry.StorageReadRate,
                device.StableId,
                device.Name);

            if (device.ReadMegabytesPerSecond is double value)
            {
                sink.PublishDevice(
                    MetricRegistry.StorageReadRate,
                    device.StableId,
                    device.Name,
                    value);
            }
        }
    }

    public void Dispose()
    {
        // Release handles and stop background work owned by this source.
    }
}
```

Expected unavailability should result in absent readings. Reserve exceptions
for unexpected provider failures.

If acquisition is asynchronous:

- Keep the loop bounded.
- Keep retained history bounded.
- Prevent overlapping operations.
- Make `Sample` copy current state without waiting on I/O.
- Coordinate disposal with in-flight work.

### 3. Register it explicitly

Add one guarded registration in `TelemetrySampler.CreateDefault`:

```csharp
_ = TryRegister(
    providers,
    claimedMetrics,
    static () => new StorageMetricProvider(),
    out _);
```

Explicit registration makes source ownership, construction order, and
failure isolation visible during review.

## Collector health boundary

Telemetry availability and subsystem health are different contracts.

`TelemetrySnapshot` contains:

- Detected per-device `MetricInstance` declarations.
- Currently available numeric `MetricReading` values.
- Coarse `CollectorHealth`.

It does not carry provider identity, per-provider status, unavailable
readings, or per-metric failure reasons.

`CollectorHealth` is limited to system-level diagnostics:

- Administrator state.
- PawnIO state, version, and setup error.
- Whether frame capture is active.
- A coarse frame-capture setup or runtime error.

Frame capture can be active while FPS is unavailable. There may be no
presenting process or not enough timestamps for a calculation. In that case,
frame slots remain stable and display `N/A`; the collector does not invent a
metric error.

Connection state and last-snapshot time belong to the frontend transport
layer. Source-specific diagnostics remain inside the collector unless they
justify a deliberately designed coarse health signal.

## Settings and presentation

The durable settings model is intentionally compact:

```text
WidgetSettings
|-- Layout
|-- Appearance
`-- MetricOverrides[MetricInstanceKey]
```

| File | Responsibility |
| --- | --- |
| `WidgetSettings.cs` | Root settings object |
| `LayoutSettings.cs` | Layout model and enums |
| `AppearanceSettings.cs` | Appearance model and enums |
| `MetricOverrides.cs` | Optional changes from registry defaults |
| `SettingsDefaults.cs` | Defaults, supported ranges, and debounce duration |
| `SettingsValidator.cs` | Deep-copy normalization and override cleanup |
| `*SettingsViewModel.cs` | XAML-facing editor state |
| `SettingsWidgetPage.xaml` | Explicit widget-setting controls |
| `WidgetSettingsStore.cs` | Validated load and atomic save |
| `SettingsAutoSaver.cs` | Preview, debounce, save ordering, and final flush |

Only differences from registry defaults are persisted. A null override
property inherits its metric definition. If an override object changes
nothing, the complete entry is removed.

Metric settings cards are generated from registry definitions and current
device declarations:

- Global definitions create global cards.
- Per-device declarations create device cards.
- A temporary missing reading does not remove either card or overlay slot.

The installed settings file is:

```text
%LOCALAPPDATA%\Packages\<SensorHUD package>\LocalState\settings.json
```

SensorHUD supports its current schema only. Invalid JSON, unknown properties,
or an incompatible shape reset safely to current defaults; there is no legacy
migration layer.

## Add a widget setting

Use this path for a preference that affects the whole widget, such as layout,
background, or typography. Metric visibility, formats, precision, and colors
belong in `MetricDefinition` and `MetricOverrides`, not in a new widget
setting.

### Steps

1. Add the typed property to `LayoutSettings` or `AppearanceSettings`.
2. Add its default or supported range to `SettingsDefaults`.
3. Normalize it in the matching `SettingsValidator` method.
4. Expose editable state and `ApplyTo` mapping in the matching section view
   model.
5. Add one compiled `x:Bind` control to `SettingsWidgetPage.xaml`.
6. Apply the setting in the telemetry page or renderer.
7. Add normalization and serialization tests.

Use shared styles from `Themes/SettingsStyles.xaml`. Keep XAML-specific types
in the frontend and keep file I/O out of view models.

## Design rules

Use these rules during implementation and review:

- Keep metric IDs stable and globally unique.
- Never reuse an existing metric ID for different data.
- Keep device IDs stable across samples and restarts.
- Keep source acquisition in providers.
- Keep LHM category mapping in focused mappers.
- Keep presentation metadata in the registry.
- Keep settings keys out of providers and mappers.
- Publish only finite numeric values that are currently available.
- Declare detected devices independently of reading availability.
- Treat expected sensor absence as normal absence.
- Keep asynchronous work and retained history bounded.
- Do not block `Sample` on network or long-running I/O.
- Do not expose source attribution through UI telemetry contracts.
- Do not add category-specific settings or rendering code for an ordinary
  metric.

## Write extension tests

SensorHUD has two complementary test projects:

| Project | What it validates |
| --- | --- |
| [`SensorHUD.Core.Tests`](../SensorHUD.Core.Tests) | Registry structure, identities, scopes, formatting, telemetry contracts, JSON, and settings normalization |
| [`SensorHUD.Collector.Tests`](../SensorHUD.Collector.Tests) | Production sample-sink validation and source mapping against synthetic hardware states |

The collector tests reference the real collector implementation. They do not
duplicate mapper logic. [`MetricSampleTestHarness`](../SensorHUD.Collector.Tests/MetricSampleTestHarness.cs)
runs a real provider or LHM mapper through the production
`MetricSampleSink`, then returns its committed instances and readings for
assertions.

### What to test for each change

| Change | Required focused coverage |
| --- | --- |
| Registry-only metadata | ID, category, scope, sort order, defaults, and formatting |
| Global mapper/provider metric | Available source value publishes the correct metric, value, unit conversion, and label |
| Optional sensor | Missing or null sensor produces no reading and no exception |
| Per-device metric | Every detected device is declared, missing values retain declarations, and readings use the correct device ID |
| Aggregate metric | Multiple source devices combine correctly; a missing direction/component is not substituted with unrelated data |
| New LHM mapper | Outputs are registry-compatible and unique; empty, partial, and multi-device snapshots are safe |
| New provider | Declared outputs match the registry; expected absence is empty output; unexpected failure remains provider-local |
| Widget setting | Normalization, JSON round-trip, default elision, and invalid-input behavior |

Tests should cover both **presence** and **absence**. A happy-path assertion
alone will not catch a sensor lookup that accidentally substitutes a
different sensor of the same type.

### Test an LHM mapper with synthetic sensors

The harness creates plain snapshot records, so mapper tests are deterministic
and do not open LibreHardwareMonitor:

```csharp
[Fact]
public void CpuMapperPublishesPackagePowerWhenAvailable()
{
    CpuSensorMapper mapper = new();
    LibreHardwareDeviceSnapshot cpu = MetricSampleTestHarness.Device(
        "cpu-0",
        "Validation CPU",
        MetricSampleTestHarness.Sensor(
            SensorType.Power,
            "CPU Package",
            65));

    MetricSampleResult result = MetricSampleTestHarness.Map(
        mapper,
        MetricSampleTestHarness.Snapshot(cpu: cpu));

    MetricReading reading = Assert.Single(
        result.Readings,
        reading => reading.MetricId == MetricRegistry.CpuPower);
    Assert.Equal(65, reading.Value);
}
```

Add the matching absence test:

```csharp
[Fact]
public void CpuMapperOmitsPackagePowerWhenSensorIsMissing()
{
    CpuSensorMapper mapper = new();
    LibreHardwareDeviceSnapshot cpu = MetricSampleTestHarness.Device(
        "cpu-0",
        "Validation CPU");

    MetricSampleResult result = MetricSampleTestHarness.Map(
        mapper,
        MetricSampleTestHarness.Snapshot(cpu: cpu));

    Assert.DoesNotContain(
        result.Readings,
        reading => reading.MetricId == MetricRegistry.CpuPower);
}
```

For a per-device mapper, create at least two devices with different sensor
shapes. Assert declarations separately from readings:

```csharp
Assert.Contains(
    result.Instances,
    instance =>
        instance.MetricId == MetricRegistry.GpuFanSpeed &&
        instance.DeviceId == "gpu-integrated");

Assert.DoesNotContain(
    result.Readings,
    reading =>
        reading.MetricId == MetricRegistry.GpuFanSpeed &&
        reading.DeviceId == "gpu-integrated");
```

This proves the device remains configurable even when that adapter does not
expose the new sensor.

### Test a provider

For a deterministic provider, run it through the same production sink:

```csharp
MetricSampleResult result =
    MetricSampleTestHarness.Sample(provider);
```

If the real source uses network, ETW, operating-system, or hardware APIs,
separate acquisition from pure calculation/mapping. Test the deterministic
part with controlled source state; do not make unit tests depend on Internet
access, elevation, a particular GPU, or timing.

The sink contract itself is covered by
[`MetricSampleSinkTests`](../SensorHUD.Collector.Tests/MetricSampleSinkTests.cs).
Mapper examples—including absent CPU sensors, two GPU shapes, VRAM
derivation, and partial NIC directions—live in
[`LibreHardwareMonitorSensorMapperTests`](../SensorHUD.Collector.Tests/LibreHardwareMonitorSensorMapperTests.cs).
Copy the closest test and adapt it for the new output.

### Keep registry expectations intentional

The Core suite validates unique IDs, category/scope indexes, settings keys,
and the expected total registry shape. When adding a metric or category,
update the corresponding expected list or count deliberately. A failing
registry-shape assertion is a prompt to confirm the new definition is indexed
and ordered as intended.

## Validate the change

Run validation from the repository root.

### Automated checks

1. Run both test projects:

   ```powershell
   dotnet test SensorHUD.Core.Tests\SensorHUD.Core.Tests.csproj `
       -c Debug `
       -p:Platform=x64

   dotnet test SensorHUD.Collector.Tests\SensorHUD.Collector.Tests.csproj `
       -c Debug `
       -p:Platform=x64
   ```

2. In a Visual Studio Developer PowerShell, build the complete solution in
   both configurations:

   ```powershell
   msbuild SensorHUD.slnx /m /p:Configuration=Debug /p:Platform=x64
   msbuild SensorHUD.slnx /m /p:Configuration=Release /p:Platform=x64
   ```

   The full solution build is required so the UWP XAML compiler, collector,
   packaging inputs, and shared contracts are all validated.

3. Check patch hygiene:

   ```powershell
   git diff --check
   ```

### Behavioral checks

For every telemetry change:

- Confirm the available value and unit are correct.
- Confirm an absent value produces `N/A`, not a fabricated reading.
- Confirm a provider failure does not suppress independent providers.
- Confirm hidden/default visibility and formatting overrides still work.

For every per-device change:

- Test at least two synthetic or physical devices.
- Confirm every device gets a distinct, stable key.
- Confirm no slot exists before its first declaration.
- Confirm declaration without a reading creates a stable `N/A` slot.
- Confirm reading loss and recovery do not recreate the XAML element.
- Confirm one malformed device does not suppress its peers.

For every settings change:

- Confirm immediate preview.
- Confirm debounced persistence.
- Confirm restart round-trip.
- Confirm reset-to-default behavior.
- Confirm invalid input normalizes safely.

For provider or protocol changes:

- Confirm construction, sampling, and disposal failure isolation.
- Confirm the pipe payload remains source-agnostic and value-only.
- Increment the protocol version only when the wire contract changes.

### Documentation checks

- Update this guide when an extension path or ownership rule changes.
- Update the README when user-visible behavior or requirements change.
- Keep examples aligned with current class and file names.
- Avoid documenting source-specific behavior as a UI contract.

A change is ready when its ownership is obvious, its absence behavior is
intentional, its failure boundary is verified, and both solution builds are
clean.
