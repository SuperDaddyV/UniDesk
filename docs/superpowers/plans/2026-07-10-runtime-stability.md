# UniDesk Runtime Stability Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make fatal UI failures terminate safely, move hardware sampling off the UI thread, activate the owned first instance without title lookup, retain only seven days of standard logs, and confirm Todo deletion.

**Architecture:** Keep native metric reads synchronous behind `ISystemMetricsService`, but schedule them sequentially in an owned background monitor. Keep the mutex for ownership and add a current-user named pipe for activation. Isolate one-shot fatal handling, log retention, and Todo deletion confirmation into testable units.

**Tech Stack:** .NET 9, WPF, named pipes, Microsoft.Extensions.DependencyInjection 9, xUnit 2.9.

## Global Constraints

- Follow root `AGENTS.md` and `docs/superpowers/specs/2026-07-10-runtime-privacy-architecture-design.md`.
- Do not add NuGet packages, change the database schema, redesign UI, or add product features.
- Log cleanup may delete only first-level files named exactly `yyyy-MM-dd.log` outside the seven-day window.
- Native hardware reads must never overlap and must never run on the WPF Dispatcher thread.
- Use TDD, run focused tests for every task, and run the full Release suite before each commit.

---

### Task 1: Make fatal UI shutdown one-shot

**Files:**
- Create: `UniDesk/Helpers/FatalExceptionCoordinator.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Test: `UniDesk.Tests/FatalExceptionCoordinatorTests.cs`

**Interfaces:**
- Produces: `bool FatalExceptionCoordinator.TryBeginShutdown()`.
- Consumes: existing `Logger`, `DirectoryHelper.LogsDirectory`, and `ILocalizationService`.

- [ ] Add the failing one-shot test:

```csharp
[Fact]
public void TryBeginShutdown_ShouldSucceedOnlyOnce()
{
    var coordinator = new FatalExceptionCoordinator();

    Assert.True(coordinator.TryBeginShutdown());
    Assert.False(coordinator.TryBeginShutdown());
}
```

- [ ] Run `dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~FatalExceptionCoordinatorTests"`; expect a compile failure because the coordinator does not exist.
- [ ] Add the coordinator:

```csharp
namespace UniDesk.Helpers;

public sealed class FatalExceptionCoordinator
{
    private int _shutdownStarted;

    public bool TryBeginShutdown() =>
        Interlocked.Exchange(ref _shutdownStarted, 1) == 0;
}
```

- [ ] Replace the anonymous Dispatcher handler with a named handler and one-shot shutdown:

```csharp
private readonly FatalExceptionCoordinator _fatalExceptionCoordinator = new();

public App()
{
#if DEBUG
    RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
#endif
    DispatcherUnhandledException += OnDispatcherUnhandledException;
}

private void OnDispatcherUnhandledException(
    object sender,
    DispatcherUnhandledExceptionEventArgs args)
{
    Logger.LogError(args.Exception, "DispatcherUnhandledException");
    args.Handled = true;
    if (!_fatalExceptionCoordinator.TryBeginShutdown()) return;

    var localization = Services?.GetService<ILocalizationService>();
    var message = localization?.Format("App.FatalErrorFormat", DirectoryHelper.LogsDirectory)
        ?? $"UniDesk 遇到无法恢复的错误，即将退出。\n日志：{DirectoryHelper.LogsDirectory}";
    MessageBox.Show(message, "UniDesk", MessageBoxButton.OK, MessageBoxImage.Error);
    Shutdown(-1);
}
```

- [ ] Add `App.FatalErrorFormat` to all four resource dictionaries with equivalent localized text and a `{0}` log-directory placeholder.
- [ ] Re-run the focused test, then `dotnet test UniDesk.sln -c Release --no-restore`; expect all tests pass.
- [ ] Commit:

```powershell
git add -- UniDesk/Helpers/FatalExceptionCoordinator.cs UniDesk/App.xaml.cs UniDesk/Resources/Strings.*.xaml UniDesk.Tests/FatalExceptionCoordinatorTests.cs
git commit -m "fix: shut down safely after fatal UI errors"
```

### Task 2: Add non-overlapping background metrics monitoring

**Files:**
- Create: `UniDesk/Services/ISystemMetricsMonitor.cs`
- Create: `UniDesk/Services/SystemMetricsMonitor.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Test: `UniDesk.Tests/SystemMetricsMonitorTests.cs`

**Interfaces:**
- Consumes: `ISystemMetricsService.Read()`.
- Produces: `event EventHandler<SystemMetricsSnapshot>? SnapshotAvailable`, `Start()`, `Stop()`, and `Dispose()` on `ISystemMetricsMonitor`.

- [ ] Add a coordinated fake reader and failing tests proving reads do not overlap and late results are ignored after disposal:

```csharp
[Fact]
public async Task Monitor_ShouldNeverOverlapReads()
{
    var reader = new BlockingMetricsReader();
    using var monitor = new SystemMetricsMonitor(
        reader,
        TimeSpan.FromMilliseconds(10),
        TimeSpan.FromSeconds(1));

    monitor.Start();
    await reader.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
    await Task.Delay(80);

    Assert.Equal(1, reader.MaxConcurrentReads);
    reader.ReleaseFirstRead.Set();
}

[Fact]
public async Task Dispose_ShouldSuppressLateSnapshot()
{
    var reader = new BlockingMetricsReader();
    var snapshots = 0;
    var monitor = new SystemMetricsMonitor(reader, TimeSpan.FromMilliseconds(10), TimeSpan.FromSeconds(1));
    monitor.SnapshotAvailable += (_, _) => Interlocked.Increment(ref snapshots);
    monitor.Start();
    await reader.FirstReadStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));

    monitor.Dispose();
    reader.ReleaseFirstRead.Set();
    await Task.Delay(80);

    Assert.Equal(0, snapshots);
}

private sealed class BlockingMetricsReader : ISystemMetricsService
{
    private int _activeReads;
    public int MaxConcurrentReads { get; private set; }
    public TaskCompletionSource FirstReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public ManualResetEventSlim ReleaseFirstRead { get; } = new(false);

    public SystemMetricsSnapshot Read()
    {
        var active = Interlocked.Increment(ref _activeReads);
        MaxConcurrentReads = Math.Max(MaxConcurrentReads, active);
        FirstReadStarted.TrySetResult();
        ReleaseFirstRead.Wait();
        Interlocked.Decrement(ref _activeReads);
        return new SystemMetricsSnapshot { CpuUsage = 42 };
    }
}
```

- [ ] Run the focused test; expect a compile failure because the monitor types do not exist.
- [ ] Add the monitor contract:

```csharp
using UniDesk.Models;

namespace UniDesk.Services;

public interface ISystemMetricsMonitor : IDisposable
{
    event EventHandler<SystemMetricsSnapshot>? SnapshotAvailable;
    void Start();
    void Stop();
}
```

- [ ] Implement `SystemMetricsMonitor` with one owned loop task. `Start` creates one CTS and uses `Task.Run(RunAsync)`; `RunAsync` calls `_reader.Read()` directly on that worker, publishes only when not stopped, logs errors, logs samples slower than `_slowThreshold`, then awaits `_interval`. `Stop` atomically detaches and cancels the CTS. `Dispose` calls `Stop`, suppresses late events through `_disposed`, and disposes an owned reader only after the loop completes.
- [ ] Register the runtime monitor as an owner of its reader so DI cannot dispose the reader while a read is in flight:

```csharp
services.AddSingleton<ISystemMetricsMonitor>(_ =>
    new SystemMetricsMonitor(
        new SystemMetricsService(),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(2),
        ownsReader: true));
```

- [ ] Replace `_systemMetricsService` and `_systemMetricsTimer` in `MainWindowViewModel` with `_systemMetricsMonitor`. Subscribe before `Start()`, dispatch completed snapshots through `Application.Current.Dispatcher`, keep existing formatting methods, and unsubscribe/dispose in `Dispose()`.
- [ ] Re-run the focused tests and the full Release suite; expect all tests pass.
- [ ] Commit:

```powershell
git add -- UniDesk/Services/ISystemMetricsMonitor.cs UniDesk/Services/SystemMetricsMonitor.cs UniDesk/App.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk.Tests/SystemMetricsMonitorTests.cs
git commit -m "fix: sample system metrics off the UI thread"
```

### Task 3: Replace title lookup with named-pipe activation

**Files:**
- Modify: `UniDesk/Helpers/SingleInstanceHelper.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk/Services/IWindowService.cs`
- Modify: `UniDesk/Services/WindowService.cs`
- Test: `UniDesk.Tests/SingleInstanceHelperTests.cs`

**Interfaces:**
- Produces: `StartListening()`, `Task<bool> SignalExistingInstanceAsync(CancellationToken)`, and `event Action? ActivationRequested` on `SingleInstanceHelper`.
- Produces: `void ActivateWindow()` on `IWindowService`.

- [ ] Add an integration test using a random instance name:

```csharp
[Fact]
public async Task SecondInstance_ShouldSignalFirstInstance()
{
    var name = $"UniDesk.Tests.{Guid.NewGuid():N}";
    using var first = new SingleInstanceHelper(name);
    using var second = new SingleInstanceHelper(name);
    var activated = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
    first.ActivationRequested += () => activated.TrySetResult();

    Assert.True(first.TryAcquire());
    first.StartListening();
    Assert.False(second.TryAcquire());
    Assert.True(await second.SignalExistingInstanceAsync(CancellationToken.None));
    await activated.Task.WaitAsync(TimeSpan.FromSeconds(3));
}
```

- [ ] Run the focused test; expect a compile failure because the constructor and pipe API do not exist.
- [ ] Rewrite `SingleInstanceHelper` to derive a mutex name and pipe name from the supplied instance name, create `NamedPipeServerStream` with `PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly`, loop one connection at a time, and raise `ActivationRequested` only for the exact UTF-8 line `Activate`. Remove `FindWindow`, `ShowWindow`, and `SetForegroundWindow` P/Invokes.
- [ ] Implement signaling with five attempts, 100 ms connect timeouts, and 100 ms delays between attempts. Return `false` after the bounded retries; do not throw from the second-instance path.
- [ ] Add and implement `IWindowService.ActivateWindow()`:

```csharp
public void ActivateWindow()
{
    if (_mainWindow == null) return;
    if (_mainWindow.WindowState == WindowState.Minimized)
        _mainWindow.WindowState = WindowState.Normal;
    _mainWindow.Show();
    _mainWindow.Activate();
    _mainWindow.Focus();
}
```

- [ ] Move mutex acquisition to the beginning of `OnStartup`, before database initialization. On a second instance, await `SignalExistingInstanceAsync`, call `Shutdown()`, and return. In the first instance, subscribe to `ActivationRequested`, start listening after `IWindowService` has the main window, and dispatch `ActivateWindow()` through the WPF Dispatcher.
- [ ] Re-run the focused test and full Release suite; expect all tests pass.
- [ ] Commit:

```powershell
git add -- UniDesk/Helpers/SingleInstanceHelper.cs UniDesk/App.xaml.cs UniDesk/Services/IWindowService.cs UniDesk/Services/WindowService.cs UniDesk.Tests/SingleInstanceHelperTests.cs
git commit -m "fix: activate the owned UniDesk instance"
```

### Task 4: Enforce seven-day log retention

**Files:**
- Create: `UniDesk/Helpers/LogRetentionService.cs`
- Modify: `UniDesk/App.xaml.cs`
- Test: `UniDesk.Tests/LogRetentionServiceTests.cs`

**Interfaces:**
- Produces: `int LogRetentionService.DeleteExpiredLogs(string directory, DateOnly today, int retentionDays = 7)`.

- [ ] Add a test directory containing an eight-day-old standard log, a six-day-old log, `notes.log`, and a nested old log. Assert that only the first file is deleted.
- [ ] Run the focused test; expect a compile failure because `LogRetentionService` does not exist.
- [ ] Implement exact-name parsing with `DateOnly.TryParseExact(Path.GetFileNameWithoutExtension(file), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)`, reject non-`.log` extensions, enumerate only `SearchOption.TopDirectoryOnly`, and delete when `date < today.AddDays(-(retentionDays - 1))`.
- [ ] Call the service immediately after `DirectoryHelper.EnsureDirectoriesExist()` in first-instance startup. Catch per-file `IOException` and `UnauthorizedAccessException`; cleanup cannot fail startup.
- [ ] Re-run focused and full Release tests; expect all tests pass.
- [ ] Commit:

```powershell
git add -- UniDesk/Helpers/LogRetentionService.cs UniDesk/App.xaml.cs UniDesk.Tests/LogRetentionServiceTests.cs
git commit -m "fix: retain seven days of application logs"
```

### Task 5: Confirm Todo deletion

**Files:**
- Create: `UniDesk/Services/ITodoDeletionHandler.cs`
- Create: `UniDesk/Services/TodoDeletionHandler.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Test: `UniDesk.Tests/TodoDeletionHandlerTests.cs`

**Interfaces:**
- Produces: `Task<bool> ConfirmAndDeleteAsync(TodoItem? todo)`.

- [ ] Add tests using complete fakes for `ITodoService`, `INotificationService`, and `ILocalizationService`: cancellation must leave `DeletedIds` empty; confirmation must add the Todo id and return `true`.
- [ ] Run the focused test; expect a compile failure because the handler does not exist.
- [ ] Implement the handler:

```csharp
public async Task<bool> ConfirmAndDeleteAsync(TodoItem? todo)
{
    if (todo == null) return false;
    var confirmed = _notificationService.ShowConfirmDialog(
        _localizationService.Format("Todo.DeleteConfirmFormat", todo.Title),
        _localizationService.GetString("Dialog.DeleteConfirmTitle"));
    if (!confirmed) return false;

    await _todoService.DeleteTodoAsync(todo.Id);
    return true;
}
```

- [ ] Add `Todo.DeleteConfirmFormat` in all four languages, register `ITodoDeletionHandler`, inject it into `MainWindowViewModel`, and change `DeleteTodoAsync` to reload only when the handler returns `true`.
- [ ] Re-run focused and full Release tests; expect all tests pass.
- [ ] Commit:

```powershell
git add -- UniDesk/Services/ITodoDeletionHandler.cs UniDesk/Services/TodoDeletionHandler.cs UniDesk/App.xaml.cs UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/Resources/Strings.*.xaml UniDesk.Tests/TodoDeletionHandlerTests.cs
git commit -m "fix: confirm before deleting todos"
```

### Task 6: Phase 3 verification and documentation

**Files:**
- Modify: `docs/DESIGN.md`
- Modify: `docs/superpowers/plans/2026-07-10-runtime-stability.md`

- [ ] Record the fatal-exit policy, background non-overlapping sampling, named-pipe activation, seven-day retention, and Todo confirmation in `docs/DESIGN.md`.
- [ ] Run `dotnet test UniDesk.sln -c Release --no-restore`; expect zero failures.
- [ ] Run `git diff --check` and `git status --short`; expect no whitespace errors and only phase 3 documentation changes after implementation commits.
- [ ] Mark completed checkboxes in this plan and commit:

```powershell
git add -- docs/DESIGN.md docs/superpowers/plans/2026-07-10-runtime-stability.md
git commit -m "docs: record runtime stability hardening"
```
