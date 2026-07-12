# UniDesk Hardware Metrics Resilience Design

## Goal

Increase hardware-metric coverage and make every missing value explainable without claiming universal temperature support that Windows and device firmware cannot provide.

## Scope

1. Add sanitized, on-demand sensor diagnostics and metric availability metadata.
2. Add Windows `GPU Engine` utilization as a vendor-neutral usage fallback.
3. Replace the two LibreHardwareMonitor `Computer` instances with one shared host and one update per sample.
4. Add an anonymized sensor-fixture corpus that turns future user reports into regression tests.
5. Add privilege-state detection and a boundary for a future least-privilege reader, while explicitly avoiding elevated UniDesk autostart and avoiding installation of a privileged service in this release.

## Current-State Decisions

- Keep `SystemMetricsMonitor`; it already performs serial background sampling and suppresses reads while the module is disabled.
- Keep LibreHardwareMonitor `0.9.6`; it is the current stable release.
- Keep NVML and ADL as preferred vendor sources.
- Do not add another vendor-specific DLL.
- Do not run the entire UniDesk process as administrator.

## Architecture

### Metric metadata

`SystemMetricsSnapshot` retains its nullable numeric properties for binding compatibility and gains source, device, availability, reason, capture-time, and freshness metadata. CPU/GPU readers return separate source information for usage and temperature so partial values are not falsely attributed to one provider.

Availability values are `Available`, `NeedsElevation`, `NoSensor`, `ProviderUnavailable`, and `Stale`. A missing CPU temperature is `NeedsElevation` only when the process is not elevated and no non-privileged fallback succeeded; an elevated process with no valid candidate reports `NoSensor`.

### Shared LibreHardwareMonitor host

`LibreHardwareComputerHost` owns one `Computer` configured for CPU, motherboard, and GPU hardware. `SystemMetricsService.Read()` refreshes it once before invoking the aggregate CPU and GPU readers. The host publishes an immutable detached sensor snapshot, preventing diagnostics and reader selection from accessing the live LHM tree concurrently.

The host records initialization state, process privilege, hardware names, and any open/update error. Disposal closes the shared computer once.

### GPU Engine fallback

`GpuEngineCounterReader` keeps `PerformanceCounter` instances alive across samples. It parses instance names into LUID, physical adapter, engine index, and engine type. Per-process values are summed per engine and clamped to 100; the busiest engine is the adapter utilization, and the busiest adapter is selected for the single-value dashboard.

Invalid individual counter samples are skipped. Missing categories enter a five-minute retry backoff. The first sample warms counters and does not overwrite a valid vendor reading.

GPU candidates may merge only when their device identities match. An unknown identity never authorizes cross-provider merging. This prevents integrated-GPU usage from being paired with discrete-GPU temperature.

### Diagnostics

`ISensorDiagnosticsService.ExportDiagnosticsAsync()` writes UTF-8 text under `DirectoryHelper.LogsDirectory` and returns the path. The report contains:

- application and Windows versions;
- administrator state;
- provider initialization and backoff state;
- CPU/GPU/motherboard hardware names;
- sensor type, display name, and current value;
- selected metric source, device identity, availability, and reason;
- the last three metric snapshots.

It excludes user names, home paths, MAC addresses, IP addresses, serial numbers, process lists, clipboard data, API keys, and database content. Settings → Data exposes an export button and opens Explorer with the generated file selected.

### Failure control and temperature quality

Deterministic initialization failures remain unavailable for the process lifetime. Repeated WMI, thermal-zone, and GPU Engine failures use a five-minute retry window. Temperature readings pass through a stateful spike filter: a single change over 40°C is held; three consecutive readings near the new level accept the transition. A source change resets the filter rather than comparing unrelated sensors.

The existing Windows ACPI WMI fallback remains. Windows Thermal Zone performance counters are optional and only labelled as a system thermal zone, never as CPU core temperature.

### Compatibility fixtures

Tests load sanitized JSON fixtures containing only provider, hardware type/name, sensor type/name/value, and expected selections. The initial corpus covers Intel CPU, AMD Ryzen CPU, NVIDIA GPU, Intel integrated GPU, dual-GPU separation, missing sensors, invalid values, and thermal-zone-only data. Future diagnostic reports can be manually sanitized into the same schema.

## UI behavior

- Existing numeric layout remains unchanged.
- Missing CPU/GPU temperatures show `--℃` with a localized tooltip explaining the availability reason.
- Available values expose source and freshness in the tooltip.
- Data settings offers `Export hardware diagnostics` and reports the generated path or a localized error.
- No UAC prompt, administrator autostart, scheduled task, service installation, or system configuration change is introduced.

## Verification

- Pure tests cover GPU Engine parsing/aggregation, device-scoped merge, backoff, spike filtering, availability reasons, sanitization, and every JSON fixture.
- Monitor tests continue proving serial background reads and disabled-module suppression.
- WPF structural tests cover the export button and localized tooltip bindings.
- Release build and full test suite must pass with zero warnings before packaging.

