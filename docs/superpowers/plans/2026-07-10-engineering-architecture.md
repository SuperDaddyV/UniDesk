# UniDesk Engineering and Architecture Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add Windows CI, remove interface-to-concrete startup casts, split system-metrics readers by responsibility, and extract six dashboard modules into focused view models and WPF controls without behavior changes.

**Architecture:** Establish CI first, then characterize and extract native readers before touching the dashboard. Keep `MainWindowViewModel` as the shell, preserve its small compatibility facade for settings interactions, and move one module per tested commit in increasing risk order.

**Tech Stack:** .NET 9, WPF, CommunityToolkit.Mvvm 8.4, GitHub Actions windows-latest, xUnit 2.9.

## Global Constraints

- Follow root `AGENTS.md` and `docs/superpowers/specs/2026-07-10-runtime-privacy-architecture-design.md`.
- Do not add/upgrade dependencies, change schema, redesign UI, delete compatibility code, build installers, deploy, or publish.
- Preserve all existing module IDs, resource keys, commands, settings keys, sort behavior, and visual styles.
- Extract mechanically before simplifying; do not combine a move with behavior changes.
- Each module extraction requires focused tests, Release full tests, a local WPF smoke check, and its own commit.

---

### Task 1: Add Windows CI and remove startup concrete casts

**Files:**
- Create: `.github/workflows/ci.yml`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk/Services/IWindowService.cs`
- Modify: `UniDesk/Services/WindowService.cs`
- Modify: `UniDesk/Services/IHotkeyService.cs`
- Modify: `UniDesk/Services/ITrayService.cs`
- Test: existing full suite.

**Interfaces:**
- Produces: `void IWindowService.Initialize(MainWindow mainWindow)`.
- `IHotkeyService` and `ITrayService` extend `IDisposable`.

- [x] Add `.github/workflows/ci.yml` exactly with checkout, setup-dotnet `9.0.x`, restore, Release build `--no-restore`, and Release test `--no-build`; trigger on pull requests and pushes to `main`; use `windows-latest`; add no publish, artifact, secret, or deployment steps.
- [x] Run the three CI commands locally in the same order; expect all succeed.
- [x] Add `Initialize(MainWindow)` to `IWindowService`, rename the existing concrete `SetMainWindow` implementation, type `_hotkeyService` and `_trayService` as interfaces, and remove all three `as WindowService`, `as HotkeyService`, and `as TrayService` casts from `App`.
- [x] Run `rg -n "GetRequiredService<[^>]+>\(\) as" UniDesk/App.xaml.cs`; expect no matches.
- [x] Run the full Release suite and commit:

```powershell
git add -- .github/workflows/ci.yml UniDesk/App.xaml.cs UniDesk/Services/IWindowService.cs UniDesk/Services/WindowService.cs UniDesk/Services/IHotkeyService.cs UniDesk/Services/ITrayService.cs
git commit -m "ci: add Windows build and test checks"
```

### Task 2: Extract sensor-selection policy before native readers

**Files:**
- Create: `UniDesk/Services/SystemMetrics/SensorSelection.cs`
- Create: `UniDesk/Services/SystemMetrics/SensorCandidates.cs`
- Modify: `UniDesk/Services/SystemMetricsService.cs`
- Modify: `UniDesk.Tests/SystemMetricsServiceTests.cs`

**Interfaces:**
- Produces: public candidate/selection record structs and static `SensorSelection` methods matching the existing CPU/GPU selection behavior.

- [x] Add characterization cases for GPU candidate source priority, CPU provider priority, boundary percentages `0/100`, boundary temperature `120`, and mixed valid/invalid candidates.
- [x] Run focused tests against the current service; expect pass and record the baseline count.
- [x] Move `CpuTemperatureSensorCandidate`, `CpuTemperatureSensorSelection`, `CpuUsageSensorCandidate`, `CpuUsageSensorSelection`, `CpuTemperatureProviderSelection`, `GpuSensorCandidate`, and `GpuSensorSelection` into `SensorCandidates.cs` without changing fields.
- [x] Move `SelectCpuTemperatureSensor`, `SelectWindowsThermalZoneTemperatureSensor`, `SelectCpuTemperatureProvider`, `SelectCpuMotherboardTemperatureSensor`, `SelectCpuUsageSensor`, `SelectGpuUsageSensor`, `SelectGpuTemperatureSensor`, and their keyword/normalization helpers into `SensorSelection`. Update native readers and tests to call that class.
- [x] Run focused tests and confirm the same assertions pass; run full Release tests and commit:

```powershell
git add -- UniDesk/Services/SystemMetrics/SensorSelection.cs UniDesk/Services/SystemMetrics/SensorCandidates.cs UniDesk/Services/SystemMetricsService.cs UniDesk.Tests/SystemMetricsServiceTests.cs
git commit -m "refactor: isolate system metric selection policy"
```

### Task 3: Extract CPU readers

**Files:**
- Create: `UniDesk/Services/SystemMetrics/CpuMetrics.cs`
- Create: `UniDesk/Services/SystemMetrics/ICpuMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/CpuMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/PerformanceCounterCpuReader.cs`
- Create: `UniDesk/Services/SystemMetrics/AsusCpuTemperatureReader.cs`
- Create: `UniDesk/Services/SystemMetrics/LibreHardwareCpuReader.cs`
- Modify: `UniDesk/Services/SystemMetricsService.cs`
- Test: `UniDesk.Tests/SystemMetricsServiceTests.cs`

**Interfaces:**
- Produces: `CpuMetrics ICpuMetricsReader.Read()` and `Dispose()` ownership in `CpuMetricsReader`.

- [x] Add a test-only constructor path for `CpuMetricsReader` and characterization tests proving Asus temperature wins, Libre usage fills missing performance-counter usage, and invalid Asus values fall back to Libre.
- [x] Run the new tests; expect compile failure because the focused readers do not exist.
- [x] Move the existing PerformanceCounter, Asus shared-memory, LibreHardware CPU, motherboard, and Windows thermal-zone code verbatim into the named files. `CpuMetricsReader` performs the current combination and Release-vs-Debug fallback decisions.
- [x] Replace CPU fields and combination logic in `SystemMetricsService` with one `ICpuMetricsReader`.
- [x] Run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Services/SystemMetrics UniDesk/Services/SystemMetricsService.cs UniDesk.Tests/SystemMetricsServiceTests.cs
git commit -m "refactor: extract CPU metric readers"
```

### Task 4: Extract GPU, memory, and network readers

**Files:**
- Create: `UniDesk/Services/SystemMetrics/GpuMetrics.cs`
- Create: `UniDesk/Services/SystemMetrics/IGpuMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/GpuMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/AmdAdlGpuReader.cs`
- Create: `UniDesk/Services/SystemMetrics/NvidiaNvmlGpuReader.cs`
- Create: `UniDesk/Services/SystemMetrics/LibreHardwareGpuReader.cs`
- Create: `UniDesk/Services/SystemMetrics/IMemoryMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/WindowsMemoryMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/INetworkMetricsReader.cs`
- Create: `UniDesk/Services/SystemMetrics/NetworkMetricsReader.cs`
- Modify: `UniDesk/Services/SystemMetricsService.cs`
- Test: `UniDesk.Tests/SystemMetricsServiceTests.cs`

- [x] Add characterization tests for discrete-GPU preference, partial GPU candidate merging, memory percentage normalization, network negative-delta clamping, and virtual-adapter exclusion.
- [x] Run new tests; expect compile failures for the focused reader types.
- [x] Move AMD ADL, NVIDIA NVML, LibreHardware GPU, Windows memory, and network code verbatim into focused files. Keep native signatures, source priority, diagnostics throttling, and disposal behavior unchanged.
- [x] Reduce `SystemMetricsService` to construction/disposal of CPU, GPU, memory, and network readers plus assembly of `SystemMetricsSnapshot`.
- [x] Run `rg -n "private sealed class .*Reader" UniDesk/Services/SystemMetricsService.cs`; expect no matches. Run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Services/SystemMetrics UniDesk/Services/SystemMetricsService.cs UniDesk.Tests/SystemMetricsServiceTests.cs
git commit -m "refactor: extract GPU memory and network readers"
```

### Task 5: Extract Hardware Monitor module

**Files:**
- Create: `UniDesk/ViewModels/HardwareMonitorViewModel.cs`
- Create: `UniDesk/Controls/HardwareMonitorModuleView.xaml`
- Create: `UniDesk/Controls/HardwareMonitorModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Test: `UniDesk.Tests/HardwareMonitorViewModelTests.cs`

**Interfaces:**
- Produces: `HardwareMonitorViewModel` properties for the seven displayed metric strings and monitor lifecycle.
- `MainWindowViewModel.HardwareMonitor` exposes the child.

- [x] Add tests using a fake `ISystemMetricsMonitor` to verify snapshot formatting, error/empty formatting, and no update after disposal.
- [x] Run focused tests; expect a compile failure for the child view model.
- [x] Move metrics properties, formatting methods, snapshot dispatch, subscription, start, and disposal from the shell into `HardwareMonitorViewModel`.
- [x] Move the complete element beginning with `<Border x:Name="HardwareMonitorModule">` through its matching closing tag into `HardwareMonitorModuleView`; bind the control instance as `<controls:HardwareMonitorModuleView x:Name="HardwareMonitorModule" DataContext="{Binding HardwareMonitor}"/>`. Convert `FontScale` bindings inside the control to the ancestor Window data context.
- [x] Run focused/full tests; verify visible values and panel collapse in the final non-destructive WPF smoke. Commit:

```powershell
git add -- UniDesk/ViewModels/HardwareMonitorViewModel.cs UniDesk/Controls/HardwareMonitorModuleView.xaml UniDesk/Controls/HardwareMonitorModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk.Tests/HardwareMonitorViewModelTests.cs
git commit -m "refactor: extract hardware monitor module"
```

### Task 6: Extract Todos module

**Files:**
- Create: `UniDesk/ViewModels/TodosViewModel.cs`
- Create: `UniDesk/Controls/TodosModuleView.xaml`
- Create: `UniDesk/Controls/TodosModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Test: `UniDesk.Tests/TodosViewModelTests.cs`

**Interfaces:**
- Produces: `Todos`, `CollapsedPanelTodo`, CRUD/toggle commands, `ReloadAsync()`, and `RefreshCollapsedPanelTodo()` on `TodosViewModel`.
- Shell `ReloadTodosAsync()` delegates to `Todos.ReloadAsync()`.

- [x] Add tests for load ordering, toggle/reload, confirmed and cancelled deletion through `ITodoDeletionHandler`, and collapsed Todo selection.
- [x] Run focused tests; expect a compile failure.
- [x] Move Todo properties and methods `AddTodoAsync` through `BuildTodoDueText` into the child. Preserve generation-based stale-load protection and dialog ownership behavior.
- [x] Move the complete element beginning with `<Border x:Name="TodosModule">` through its matching closing tag into `TodosModuleView`, keep `TodoSwipeRow`, and bind its commands to the child control data context.
- [x] Update collapsed-header bindings to `Todos.CollapsedPanelTodo`, `Todos.CollapsedPanelTodoDueText`, and `Todos.HasCollapsedPanelTodo`.
- [x] Run focused/full tests; cover add/edit/toggle/delete/cancel behavior in automated tests and verify collapsed Todo display in the final non-destructive WPF smoke. Commit:

```powershell
git add -- UniDesk/ViewModels/TodosViewModel.cs UniDesk/Controls/TodosModuleView.xaml UniDesk/Controls/TodosModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk.Tests/TodosViewModelTests.cs
git commit -m "refactor: extract todos module"
```

### Task 7: Extract Quick Notes and legacy Notes responsibility

**Files:**
- Create: `UniDesk/ViewModels/QuickNotesViewModel.cs`
- Create: `UniDesk/Controls/QuickNotesModuleView.xaml`
- Create: `UniDesk/Controls/QuickNotesModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Test: `UniDesk.Tests/QuickNotesViewModelTests.cs`

**Interfaces:**
- Produces: QuickNote collection/commands and `ReloadAsync()`; retains the non-visible legacy `Notes` collection and its current operations without deleting compatibility behavior.

- [x] Add tests for QuickNote load, create/edit reload callback, pinning, copy, delete confirmation, and stale-load suppression. Add one legacy Notes load test.
- [x] Run focused tests; expect a compile failure.
- [x] Move current `Notes` methods and current QuickNotes methods into one notes-domain child, keeping their separate services and models. The constructor initiates legacy load exactly as the shell currently does.
- [x] Move the complete element beginning with `<Border x:Name="QuickNotesModule">` through its matching closing tag into `QuickNotesModuleView`; change command bindings to the child.
- [x] Keep shell `ReloadQuickNotesAsync()` as a delegate. Run focused/full tests; cover create/edit/pin/copy/delete/localization behavior without mutating live user data. Commit:

```powershell
git add -- UniDesk/ViewModels/QuickNotesViewModel.cs UniDesk/Controls/QuickNotesModuleView.xaml UniDesk/Controls/QuickNotesModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk.Tests/QuickNotesViewModelTests.cs
git commit -m "refactor: extract quick notes module"
```

### Task 8: Extract Quick Text module

**Files:**
- Create: `UniDesk/ViewModels/QuickTextViewModel.cs`
- Create: `UniDesk/Controls/QuickTextModuleView.xaml`
- Create: `UniDesk/Controls/QuickTextModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Test: `UniDesk.Tests/QuickTextViewModelTests.cs`

**Interfaces:**
- Produces: history/snippet collections, selected mode, commands, clipboard event ownership, and `ReloadAsync()`.

- [x] Add tests for mode switching, clipboard-triggered reload, copy/delete/clear/favorite, snippet create/edit/delete, and disposal unsubscribe.
- [x] Run focused tests; expect a compile failure.
- [x] Move Quick Text properties and methods from `ClipboardMonitor_OnHistoryChanged` through `LoadQuickTextAsync` into the child, preserving generation guards and notification behavior.
- [x] Move the complete element beginning with `<Border x:Name="QuickTextModule">` through its matching closing tag into `QuickTextModuleView`; replace Window-relative shell command bindings with UserControl-relative child bindings.
- [x] Keep shell `ReloadQuickTextAsync()` as a delegate. Run focused/full tests; cover history/snippet commands and encrypted history without clearing or rewriting live user data. Commit:

```powershell
git add -- UniDesk/ViewModels/QuickTextViewModel.cs UniDesk/Controls/QuickTextModuleView.xaml UniDesk/Controls/QuickTextModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk.Tests/QuickTextViewModelTests.cs
git commit -m "refactor: extract quick text module"
```

### Task 9: Extract Shortcuts module and its input handlers

**Files:**
- Create: `UniDesk/ViewModels/ShortcutsViewModel.cs`
- Create: `UniDesk/Controls/ShortcutsModuleView.xaml`
- Create: `UniDesk/Controls/ShortcutsModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Modify: `UniDesk/MainWindow.xaml.cs`
- Test: `UniDesk.Tests/ShortcutsViewModelTests.cs`

**Interfaces:**
- Produces: shortcut collections/display entries, add/edit/launch/delete/move commands, `ReloadAsync()`, `SetLimitPreview(int?)`, and `AddFromPathsAsync(IEnumerable<string>?)`.

- [x] Add tests for duplicate prevention, max-count truncation, add result counts, display placeholder, move boundaries, persisted ordering, and stale-load suppression.
- [x] Run focused tests; expect a compile failure.
- [x] Move shortcut properties and methods from `LaunchShortcutAsync` through `LoadShortcutsAsync` into the child. Preserve the public result model and settings preview behavior.
- [x] Move the complete element beginning with `<Border x:Name="ShortcutsModule">` through its matching closing tag into `ShortcutsModuleView`. Move shortcut drag/drop state and handlers from `MainWindow.xaml.cs` into the control code-behind; the control calls its `ShortcutsViewModel` only.
- [x] Keep shell `ReloadShortcutsAsync()` and `SetShortcutLimitPreview()` as delegates for `SettingsViewModel`. Remove shortcut-only fields and methods from MainWindow code-behind.
- [x] Run focused/full tests; cover add, duplicate, reorder, invalid path, and max-count behavior without altering live shortcut data. Commit:

```powershell
git add -- UniDesk/ViewModels/ShortcutsViewModel.cs UniDesk/Controls/ShortcutsModuleView.xaml UniDesk/Controls/ShortcutsModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk/MainWindow.xaml.cs UniDesk.Tests/ShortcutsViewModelTests.cs
git commit -m "refactor: extract shortcuts module"
```

### Task 10: Extract Time, Calendar, and Weather module

**Files:**
- Create: `UniDesk/ViewModels/TimeWeatherViewModel.cs`
- Create: `UniDesk/Controls/TimeWeatherModuleView.xaml`
- Create: `UniDesk/Controls/TimeWeatherModuleView.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Test: `UniDesk.Tests/TimeWeatherViewModelTests.cs`

**Interfaces:**
- Produces: clock/calendar/weather properties and commands, weather timer ownership, `RefreshAfterSettingsAsync()`, and disposal.

- [x] Add tests for clock formatting by language, calendar month navigation/today selection, cached weather initialization, cancellation of superseded refresh, failed-refresh state, and timer disposal.
- [x] Run focused tests; expect a compile failure.
- [x] Move clock/calendar members `ClockService_OnTimeChanged` through `BuildCalendarSelectedDetail` and weather members `InitializeWeatherAsync` through `ResolveIconCode` into the child. Move the 30-minute timer and weather CTS ownership with them.
- [x] Move the complete element beginning with `<Border x:Name="TimeWeatherModule">` through its matching closing tag into `TimeWeatherModuleView`. Update collapsed header bindings to `TimeWeather.ClockTimeText`, `TimeWeather.ClockDateText`, `TimeWeather.ClockLunarText`, and the child Todo summary bindings already established in Task 6.
- [x] Keep shell `RefreshWeatherAfterSettingsAsync()` as a delegate. Run focused/full tests; verify clock/weather rendering, calendar popup, and collapsed state in the final non-destructive WPF smoke. Commit:

```powershell
git add -- UniDesk/ViewModels/TimeWeatherViewModel.cs UniDesk/Controls/TimeWeatherModuleView.xaml UniDesk/Controls/TimeWeatherModuleView.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk.Tests/TimeWeatherViewModelTests.cs
git commit -m "refactor: extract time and weather module"
```

### Task 11: Final shell cleanup, verification, and documentation

**Files:**
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/MainWindow.xaml`
- Modify: `UniDesk/MainWindow.xaml.cs`
- Modify: `docs/DESIGN.md`
- Modify: `docs/superpowers/plans/2026-07-10-engineering-architecture.md`

- [x] Remove only imports, fields, and helpers made orphaned by the six extractions. Do not delete pre-existing compatibility services or database objects.
- [x] Confirm the shell directly owns only window state, module layout, settings coordination, child composition, and compatibility delegates; confirm module CRUD is absent.
- [x] Run `dotnet test UniDesk.sln -c Release --no-restore`; expect zero failures.
- [x] Launch the Release build and execute a non-destructive WPF smoke against live data: verify rendered modules and metrics, six-module settings list, cancel-without-save, panel collapse/restore, calendar open/close, and second-instance activation. Rely on focused tests for destructive CRUD and setting mutations.
- [x] Run `git diff --check`, `git status --short`, and scope checks for schema/dependency/installer/deployment changes; expect none outside approved documentation.
- [x] Update `docs/DESIGN.md`, mark plan checkboxes complete, and commit:

```powershell
git add -- UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/MainWindow.xaml UniDesk/MainWindow.xaml.cs docs/DESIGN.md docs/superpowers/plans/2026-07-10-engineering-architecture.md
git commit -m "docs: record dashboard architecture boundaries"
```
