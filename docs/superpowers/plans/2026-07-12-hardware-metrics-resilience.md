# Hardware Metrics Resilience Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add explainable hardware-metric availability, vendor-neutral GPU usage fallback, one shared LHM host, diagnostic export, and reusable compatibility fixtures without elevating the UniDesk process.

**Architecture:** `SystemMetricsService` coordinates one immutable LHM sensor snapshot per sample. CPU/GPU readers attach provider and device metadata, GPU Engine supplies usage-only fallback, and diagnostics consume detached snapshots plus a three-sample history. Settings exposes a sanitized export command.

**Tech Stack:** C# 13, .NET 9 WPF, `System.Diagnostics.PerformanceCounter`, LibreHardwareMonitorLib 0.9.6, xUnit.

## Global Constraints

- Keep the existing `SystemMetricsMonitor` background loop.
- Do not add vendor DLLs or NuGet dependencies.
- Do not elevate UniDesk, install services, create scheduled tasks, or change system configuration.
- Diagnostic output must exclude user names, paths, network identifiers, serial numbers, secrets, and user content.
- Use TDD for every behavior and commit each task separately.

---

### Task 1: Metric metadata, backoff, and temperature quality

**Files:**
- Create: `UniDesk/Models/HardwareMetricAvailability.cs`
- Create: `UniDesk/Services/SystemMetrics/ReaderFailureBackoff.cs`
- Create: `UniDesk/Services/SystemMetrics/TemperatureSpikeFilter.cs`
- Modify: `UniDesk/Models/SystemMetricsSnapshot.cs`
- Modify: `UniDesk/Services/SystemMetrics/CpuMetrics.cs`
- Modify: `UniDesk/Services/SystemMetrics/GpuMetrics.cs`
- Test: `UniDesk.Tests/HardwareMetricResilienceTests.cs`

**Interfaces:**
- Produces: `HardwareMetricAvailability`, `ReaderFailureBackoff`, `TemperatureSpikeFilter`, and source/device fields consumed by later tasks.

- [ ] Write failing tests for three-failure backoff, retry expiry, one-off temperature spike rejection, sustained transition acceptance, source-change reset, and device-aware GPU merge metadata.
- [ ] Run `dotnet test UniDesk.Tests/UniDesk.Tests.csproj --filter FullyQualifiedName~HardwareMetricResilienceTests` and confirm failures are caused by missing types.
- [ ] Implement the enum and the two small state machines; extend models with optional metadata while retaining existing constructors and nullable value properties.
- [ ] Run focused tests and all existing `SystemMetricsServiceTests`.
- [ ] Commit with `feat: add hardware metric quality metadata`.

### Task 2: One shared LibreHardwareMonitor snapshot

**Files:**
- Create: `UniDesk/Services/SystemMetrics/HardwareSensorSnapshot.cs`
- Create: `UniDesk/Services/SystemMetrics/LibreHardwareComputerHost.cs`
- Modify: `UniDesk/Services/SystemMetrics/LibreHardwareCpuReader.cs`
- Modify: `UniDesk/Services/SystemMetrics/LibreHardwareGpuReader.cs`
- Modify: `UniDesk/Services/SystemMetrics/CpuMetricsReader.cs`
- Modify: `UniDesk/Services/SystemMetrics/GpuMetricsReader.cs`
- Modify: `UniDesk/Services/SystemMetricsService.cs`
- Test: `UniDesk.Tests/LibreHardwareComputerHostTests.cs`

**Interfaces:**
- Produces: `ILibreHardwareComputerHost.Refresh()`, `CurrentSensors`, `DiagnosticStatus`, and single-owner disposal.
- Consumes: metadata and quality filters from Task 1.

- [ ] Write failing tests using a fake host snapshot for CPU/GPU selection, one refresh per service sample, and one disposal.
- [ ] Confirm focused tests fail before production changes.
- [ ] Implement a locked host that updates the complete hardware tree once and publishes detached immutable sensor records.
- [ ] Refactor LHM readers to select from detached records and preserve CPU, motherboard, ACPI, and Intel GPU behavior.
- [ ] Run host, service, monitor, and selection tests.
- [ ] Commit with `refactor: share libre hardware sensor snapshot`.

### Task 3: GPU Engine fallback and device-scoped selection

**Files:**
- Create: `UniDesk/Services/SystemMetrics/GpuEngineCounterReader.cs`
- Modify: `UniDesk/Services/SystemMetrics/GpuMetricsReader.cs`
- Modify: `UniDesk/Services/SystemMetricsService.cs` if service routing requires it
- Test: `UniDesk.Tests/GpuEngineCounterReaderTests.cs`
- Test: `UniDesk.Tests/SystemMetricsServiceTests.cs`

**Interfaces:**
- Produces: `GpuMetrics Read()` with `UsageSource="Windows GPU Engine"` and parsed LUID device identity.
- Consumes: `ReaderFailureBackoff` and device metadata from Task 1.

- [ ] Write failing pure tests for instance parsing, per-engine process summation, busiest-engine selection, invalid samples, warm-up behavior, and dual-adapter selection.
- [ ] Add failing selection tests proving values from different device IDs are never merged.
- [ ] Implement persistent dynamic counters with per-counter exception isolation and five-minute category retry.
- [ ] Insert GPU Engine after NVML/ADL and before usage remains missing; preserve complete vendor candidates.
- [ ] Run GPU and service tests.
- [ ] Commit with `feat: add windows gpu engine fallback`.

### Task 4: Sanitized diagnostics and compatibility fixtures

**Files:**
- Create: `UniDesk/Services/ISensorDiagnosticsService.cs`
- Create: `UniDesk/Services/SensorDiagnosticReporter.cs`
- Create: `UniDesk.Tests/SensorDiagnosticReporterTests.cs`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/intel-cpu.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/amd-ryzen.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/nvidia-gpu.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/intel-igpu.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/dual-gpu.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/missing-sensors.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/invalid-values.json`
- Create: `UniDesk.Tests/Fixtures/HardwareSensors/thermal-zone.json`
- Modify: `UniDesk/Services/SystemMetricsService.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk.Tests/UniDesk.Tests.csproj`

**Interfaces:**
- Produces: `Task<string> ExportDiagnosticsAsync(CancellationToken cancellationToken = default)`.
- Consumes: host diagnostic snapshot, provider metadata, backoff state, and last three service snapshots.

- [ ] Write failing tests asserting required report sections and forbidden sensitive-field tokens.
- [ ] Write failing theory tests loading all fixture JSON files and checking expected metric selection.
- [ ] Implement the reporter and a thread-safe three-snapshot history.
- [ ] Register the shared service instance for both monitoring and diagnostics without double disposal.
- [ ] Run diagnostics, fixtures, and full system metrics tests.
- [ ] Commit with `feat: export sanitized sensor diagnostics`.

### Task 5: Settings and hardware-monitor explanations

**Files:**
- Modify: `UniDesk/ViewModels/SettingsViewModel.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/ViewModels/HardwareMonitorViewModel.cs`
- Modify: `UniDesk/Controls/Settings/DataSettingsPage.xaml`
- Modify: `UniDesk/Controls/HardwareMonitorModuleView.xaml`
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Test: `UniDesk.Tests/HardwareMonitorViewModelTests.cs`
- Test: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Consumes: `ISensorDiagnosticsService` and snapshot availability/source metadata.
- Produces: `ExportSensorDiagnosticsCommand`, localized tooltips, and success/error notifications.

- [ ] Write failing tests for availability-to-tooltip mapping, export command wiring, and four-language resource coverage.
- [ ] Inject diagnostics into settings through the existing MainWindowViewModel construction path.
- [ ] Add the Data settings export button and hardware value tooltips without changing the card layout.
- [ ] Open Explorer with the generated file selected only after a successful export.
- [ ] Run view-model and WPF regression tests.
- [ ] Commit with `feat: explain and export hardware availability`.

### Task 6: Final verification and packaging

**Files:**
- Modify: `docs/release-unidesk.md`
- Generated: `installer/UniDesk_Setup_2.0.0.exe`

**Interfaces:**
- Consumes all prior tasks; produces the user-test installer only.

- [ ] Update English and Chinese v2.0.0 notes with diagnostics, GPU Engine fallback, shared LHM host, and permission-safe behavior.
- [ ] Run `dotnet build UniDesk.sln -c Release --no-restore` and require zero warnings and errors.
- [ ] Run `dotnet test UniDesk.sln -c Release --no-build --no-restore` and require zero failures.
- [ ] Publish self-contained win-x64 with `DebugSymbols=false` and `DebugType=None`; verify zero PDB files.
- [ ] Compile `UniDesk.iss`, copy the installer to `C:\Users\Administrator\Documents\UniDesk\installer\UniDesk_Setup_2.0.0.exe`, compare SHA-256 hashes, and run a targeted Defender scan.
- [ ] Keep the branch and worktree for user testing; do not push or publish.
