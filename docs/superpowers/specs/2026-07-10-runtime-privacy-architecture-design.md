# UniDesk Runtime, Privacy, and Architecture Design

## Goal

Complete remediation phases 3, 4, and 5 after the data-integrity work: make runtime failures safe, move slow hardware sampling off the UI thread, protect user-sensitive data at rest, make backup import explicit and inspectable, add Windows CI, and split the largest services and dashboard modules without changing product behavior.

## Scope

### Included

- Fatal UI exception handling that logs, informs the user once, and exits.
- Non-overlapping background system-metrics sampling.
- Mutex ownership plus current-user named-pipe activation for the existing instance.
- Deletion of standard log files older than the seven-day retention window.
- A localized confirmation before deleting a Todo.
- DPAPI `CurrentUser` protection for the user-supplied weather API key and clipboard-history content.
- Transactional, idempotent migration of existing plaintext sensitive values.
- Backup v5 options, sensitive-content disclosure, and prepare/preview/apply import flow.
- Removal of the unencrypted IP-location fallback and extension of clipboard-sensitive filtering.
- Windows GitHub Actions restore, Release build, and test checks.
- Removal of interface-to-concrete service casts in `App.xaml.cs`.
- Responsibility-based extraction of hardware readers and six dashboard modules.

### Excluded

- Database schema changes or database-version changes.
- Encryption of the built-in shared weather credential as a claim of secrecy.
- New reminders, recurring Todos, automatic backup, global search, or other product features.
- Visual redesign, dependency upgrades, installer generation, deployment, tags, or GitHub Releases.
- Deletion of unknown files, recursive log cleanup, or removal of unverified compatibility code.

## Delivery Strategy

Work proceeds in risk order and remains independently testable after every task:

1. Phase 3: runtime stability and user-protection behavior.
2. Phase 4: sensitive-data migration, protected storage, and safe backup/import flow.
3. Phase 5A: CI and DI boundary cleanup.
4. Phase 5B: system-metrics reader extraction.
5. Phase 5C: one dashboard module extraction per commit.

Each phase must finish with focused tests, `dotnet test UniDesk.sln -c Release --no-restore`, and a clean scope review before the next phase begins.

## Phase 3: Runtime Stability

### Fatal UI exceptions

Recoverable command, service, validation, and network failures remain locally caught and user-visible. An exception reaching `DispatcherUnhandledException` is considered unrecoverable because application state may already be inconsistent.

The handler uses an atomic one-shot guard, writes the exception, presents one fallback-safe error dialog, sets `Handled = true` only to suppress duplicate WPF handling, and calls `Shutdown(-1)`. It never resumes normal application execution. Startup exceptions keep the existing log, dialog, and shutdown path. `TaskScheduler.UnobservedTaskException` remains logged and observed; `AppDomain.UnhandledException` remains logging-only because the runtime controls termination.

### Background system-metrics monitor

The synchronous native readers remain behind `ISystemMetricsService.Read()`. A separate monitor owns the two-second schedule and executes reads sequentially on a background worker. It never starts a second read while one is outstanding.

Completed snapshots are dispatched to the UI for property updates. A failed read keeps the previous valid snapshot and records a rate-limited warning. A read taking more than two seconds records a rate-limited slow-sample warning after it returns. The design does not claim that cancellation can stop arbitrary driver or native calls. If a call hangs, future sampling stops behind that call while the UI remains responsive.

Disposal stops the schedule, prevents late snapshots from reaching disposed view models, and does not wait indefinitely for a blocked native read.

### Reliable existing-instance activation

The mutex remains the ownership primitive. The first instance also starts a named-pipe listener restricted to the current user. A later instance that does not acquire the mutex sends an `Activate` message with a bounded retry window, then exits.

The first instance dispatches activation to the UI thread and asks `IWindowService` to show, restore, activate, and focus the owned main window. No code searches global windows by title, so custom display titles and unrelated windows cannot redirect activation.

### Log retention

Startup invokes a focused retention component on the configured log directory. It considers only first-level files whose names parse exactly as `yyyy-MM-dd.log`, keeps the current day and the previous six calendar days, and deletes older matching files. It never recurses or deletes unknown names. Failure to inspect or delete one file does not block startup.

The current low-volume append implementation remains. This phase does not add an asynchronous logging framework.

### Todo deletion confirmation

All Todo deletion entry points continue to bind to the same generated `DeleteTodoCommand`. The command asks for localized confirmation containing the Todo title. Cancellation returns before `ITodoService.DeleteTodoAsync`; confirmation deletes and reloads the list.

## Phase 4: Privacy and Import Safety

### Versioned user-data protection

A small protection abstraction exposes protect, unprotect, and protected-format detection operations. The Windows implementation uses the native DPAPI with `CurrentUser` scope and application-specific optional entropy, without adding a NuGet dependency. Persisted ciphertext uses the prefix `dpapi:v1:` followed by Base64 data.

Plaintext remains inside settings caches and view models only as needed. Logs, exception messages, test output, and import summaries never contain the weather key or clipboard body.

The built-in shared weather credential remains an obfuscated client credential, not a protected user secret. Documentation must not describe it as secure against extraction; a server-side proxy would be required to provide that property.

### Idempotent plaintext migration

A privacy migration service runs after database initialization and before the main window is constructed. In one database transaction it reads the non-empty user `WeatherApiKey` value and every clipboard-history row whose content lacks a recognized protection prefix, protects them, and updates the original rows. Clipboard `ContentHash` remains the SHA-256 of normalized plaintext for deduplication.

Protection or SQL failure while migrating plaintext rolls back the whole migration and follows the existing startup-failure path. Re-running after a successful migration performs no writes. Already protected data that cannot be decrypted is not deleted or replaced: the weather key is treated as unavailable, and the affected clipboard row is omitted from display with a content-free warning.

### Protected read and write paths

`SettingsService` transparently encodes `WeatherApiKey` before persistence and decodes it after reading. Its cache continues to hold plaintext. Other setting keys retain their current behavior.

`QuickTextService` protects normalized clipboard content before insert and decrypts it while mapping database rows. Hashing and equality checks use normalized plaintext. Text snippets are not part of this encryption scope because they are intentionally user-managed reusable content, not passive clipboard capture.

Backup restore uses the same storage codec before direct transactional inserts, so it cannot bypass protection. Legacy v1-v4 backup files containing a plaintext weather key remain importable and are protected before storage.

### Backup v5 privacy contract

The export API accepts explicit options. The default excludes clipboard history. The user-supplied weather key is always excluded; ordinary settings such as the weather host remain included.

If the user explicitly includes clipboard history, the UI warns before writing that portable JSON must contain readable clipboard text. The v5 payload declares included sections and whether portable sensitive plaintext is present. Versions 1 through 4 remain accepted.

### Prepare, preview, and apply import

Import changes from a path-only mutation call to two steps:

1. `PrepareImportAsync` reads once, deserializes, validates, normalizes, and returns an immutable in-memory import plan plus a display preview.
2. After user confirmation, `ApplyImportAsync` writes only that validated plan through the existing single-transaction restore path.

The preview shows counts for every included section, whether clipboard plaintext is present, and every shortcut path with its launch arguments. Executables, scripts, URLs, links, and entries with arguments are visually emphasized in a scrollable dialog. Cancellation performs no flush, delete, insert, cache invalidation, or icon refresh.

Holding the normalized plan in memory prevents a file from changing between preview and apply.

### Location and sensitive filtering

The `http://ip-api.com` fallback is removed without adding a new uncontrolled third-party provider. Automatic location first uses the existing HTTPS Amap path when configured; otherwise the application uses the saved city.

Clipboard filtering adds tested patterns for common raw API keys, GitHub tokens, AWS access-key identifiers, and private-key headers. Tests include both matching and nearby non-sensitive examples to control false positives.

## Phase 5: Engineering and Architecture

### Windows CI

`.github/workflows/ci.yml` runs on pull requests and pushes to `main` using `windows-latest` and .NET 9. It executes restore, Release build without restore, and tests without rebuilding. It does not publish artifacts, build the installer, deploy, or consume secrets.

### DI composition boundary

Resolving services in `App`, the composition root, remains valid. The defect to remove is resolving an interface and casting it back to a concrete implementation.

`App` fields use `ITrayService` and `IHotkeyService`. Required lifecycle operations live on their interfaces. `IWindowService` gains explicit main-window initialization and existing-instance activation behavior. `App` no longer depends on `TrayService`, `HotkeyService`, or `WindowService` concrete types.

### System-metrics reader extraction

The large metrics service is split by result shape rather than forced through one universal interface:

- CPU readers and CPU sensor selection.
- GPU readers and GPU sensor selection.
- Windows memory reader.
- Network speed reader and adapter filtering.
- Shared normalization and candidate-selection helpers.

`SystemMetricsService` composes those focused readers into `SystemMetricsSnapshot`. Existing source priorities, fallback rules, throttled diagnostics, and disposal behavior remain unchanged. Selection and fallback characterization tests are added before moving implementations.

### Dashboard module extraction

`MainWindowViewModel` remains the dashboard shell and composes:

- `TimeWeatherViewModel`
- `HardwareMonitorViewModel`
- `ShortcutsViewModel`
- `TodosViewModel`
- `QuickNotesViewModel`
- `QuickTextViewModel`

Each has a matching WPF `UserControl`. Extraction order is HardwareMonitor, Todos, QuickNotes, QuickText, Shortcuts, then TimeWeather. The shell retains window state, module layout, settings coordination, and a small compatibility facade for existing settings-window refresh calls. Module CRUD, collections, commands, timers, and service dependencies move to the owning child.

Module-specific input handlers move with their views. The main window code-behind retains only window-level behavior such as dragging, sizing, edge clamping, closing, and module-container coordination. Existing resource keys, visual styles, binding semantics, module ordering, and empty-state behavior remain unchanged.

No unverified legacy code is deleted as part of extraction.

## Error Handling

- Fatal UI failures terminate once after logging and notification.
- Metrics-reader failures preserve the last valid snapshot and do not reach the UI exception boundary.
- Named-pipe activation failures are bounded, logged, and do not start a second full application instance.
- Log cleanup failures never prevent startup.
- Plaintext privacy migration failures roll back and stop startup.
- DPAPI decryption failures never expose ciphertext as user content and never delete the source row.
- Invalid backup data fails during prepare, before any database mutation.
- Apply failures roll back every included section and propagate to the existing user-visible error path.

## Testing and Verification

### Automated

- Fatal exception coordinator is one-shot and requests shutdown.
- Metrics monitor does not overlap reads, preserves the last snapshot on error, and ignores late results after disposal.
- Two coordinators with a random mutex/pipe name exercise activation without window-title lookup.
- Retention deletes only matching files outside seven days.
- Todo cancellation does not call the service.
- DPAPI round-trip, prefix detection, wrong-user/unreadable handling boundary, migration idempotence, and migration rollback.
- Raw SQLite assertions show no plaintext user weather key or clipboard body after writes and migration.
- Backup defaults, explicit clipboard inclusion, v1-v4 compatibility, v5 metadata, old weather-key import protection, preview cancellation, and shortcut-risk preview.
- Sensitive-filter positive and negative cases.
- CI YAML is reviewed against the exact local restore/build/test commands.
- Metrics selection and fallback characterization tests pass before and after extraction.
- Each module view model receives focused command, loading, cancellation, and error tests before extraction.

### Manual WPF regression gate

After each module extraction, launch the application and verify both themes, minimum and maximum panel sizes, font-scale bounds, module show/hide/order, scrolling, localization, and the module's interactions. Shortcut drag/drop and edit ordering, Todo swipe/delete, QuickNote editing, QuickText copy/manage, weather/calendar popups, tray activation, and second-instance activation are verified in their relevant extraction step.

### Final gate

- `dotnet test UniDesk.sln -c Release --no-restore` passes with zero failures.
- `git diff --check` reports no whitespace errors.
- Changed files remain within the approved phase 3-5 scope.
- No database schema, dependency, installer, deployment, or new-feature changes are present.

## Success Criteria

- The application never continues normal execution after an unhandled UI exception.
- Hardware sampling cannot block the UI thread or overlap driver reads.
- A second instance activates the owned first-instance window regardless of title.
- Only standard log files older than seven days are removed.
- User weather keys and clipboard bodies are not plaintext in SQLite.
- Default backups exclude both the user weather key and clipboard history.
- Import cannot mutate data before a complete preview and explicit confirmation.
- The unencrypted IP-location endpoint is absent.
- Windows CI protects restore, Release build, and test.
- App startup no longer casts resolved interfaces to concrete service types.
- System metrics readers and all six dashboard modules have focused files and tests while existing behavior remains intact.
