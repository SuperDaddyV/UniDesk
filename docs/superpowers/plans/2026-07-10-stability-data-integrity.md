# UniDesk Stability and Data Integrity Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make backup restore atomic and truthful, make database/settings failures observable, enable WAL, and fix database version ordering without changing schema.

**Architecture:** Add an `IDatabaseSession` bound to one connection and optional transaction. Validate a complete backup before mutation, restore all included sections through one transactional session, then invalidate caches and rebuild derived shortcut icons. Keep settings debounce but synchronize its state and preserve failed writes for retry.

**Tech Stack:** .NET 9, WPF, Microsoft.Data.Sqlite 9, xUnit 2.9.

## Global Constraints

- Follow root `AGENTS.md` and update `docs/DESIGN.md` before behavior changes.
- Do not add NuGet dependencies or change database schema.
- Do not modify CI/CD, secrets, log deletion, hardware monitoring, single-instance behavior, or UI architecture.
- Use TDD and run the full Release test suite after each task.

---

### Task 1: Establish transactional database sessions

**Files:**
- Create: `UniDesk/Services/IDatabaseSession.cs`
- Create: `UniDesk/Services/DatabaseSession.cs`
- Modify: `UniDesk/Services/IDatabaseService.cs`
- Modify: `UniDesk/Services/DatabaseService.cs`
- Test: `UniDesk.Tests/DatabaseServiceTests.cs`

**Interfaces:**
- Produces: `Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation)`.
- Produces: session query methods matching existing parameter and reader mapping behavior.

- [x] Add a failing integration test that creates a table, inserts inside `ExecuteInTransactionAsync`, throws, and asserts the inserted row is absent.
- [x] Run `dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~ExecuteInTransactionAsync"`; expect failure because the API is missing.
- [x] Add the session interface and implementation. Every command created by `DatabaseSession` must set `command.Transaction` when a transaction exists.
- [x] Implement `ExecuteInTransactionAsync` with one connection, commit on success, rollback on exception, and no exception swallowing.
- [x] Re-run the focused test; expect pass.
- [x] Run the full Release suite; expect all tests pass.
- [x] Commit with `feat: add transactional database sessions`.

### Task 2: Validate backup semantics before writes

**Files:**
- Modify: `UniDesk/Services/TodoBackupService.cs`
- Test: `UniDesk.Tests/TodoBackupServiceTests.cs`

**Interfaces:**
- Consumes: `IDatabaseService.ExecuteInTransactionAsync` from Task 1.
- Produces: a private validated restore plan containing only normalized entries.

- [x] Add `ImportFromFileAsync_InvalidTodo_ShouldPreserveExistingData` using a v4 JSON file with an empty Todo title.
- [x] Add `ImportFromFileAsync_FutureVersion_ShouldPreserveExistingData` using version 5.
- [x] Run both focused tests; expect the invalid file to clear data or be accepted under the old implementation.
- [x] Implement full pre-validation for versions 1-4 and every included section. Empty lists remain valid clear operations; invalid list entries throw `InvalidDataException` with section and index.
- [x] Re-run focused tests; expect pass.
- [x] Run the full Release suite; expect all tests pass.
- [x] Commit with `fix: validate backups before restore`.

### Task 3: Restore every included section in one transaction

**Files:**
- Modify: `UniDesk/Services/ISettingsService.cs`
- Modify: `UniDesk/Services/SettingsService.cs`
- Modify: `UniDesk/Services/IShortcutService.cs`
- Modify: `UniDesk/Services/ShortcutService.cs`
- Modify: `UniDesk/Services/TodoBackupService.cs`
- Test: `UniDesk.Tests/TodoBackupServiceTests.cs`

**Interfaces:**
- Produces: `void InvalidateCache()` on `ISettingsService`.
- Produces: `Task RefreshMissingIconsAsync()` on `IShortcutService`.

- [x] Add `ImportFromFileAsync_InsertFailure_ShouldRollbackSettingsAndTodos`. Create a SQLite trigger that raises `ABORT` for the imported Todo title, then assert the original setting and Todo remain.
- [x] Add a successful restore test asserting exact counts, continuous shortcut sort order, and refreshed setting values.
- [x] Run focused tests; expect rollback test failure under delete-then-insert behavior.
- [x] Flush pending settings before the transaction. Within the transaction, delete only included sections and insert normalized rows using `IDatabaseSession`; use affected row counts.
- [x] After commit, call `InvalidateCache()` and `RefreshMissingIconsAsync()`. Icon refresh may log individual failures but must not alter restore success.
- [x] Re-run focused tests; expect pass.
- [x] Run the full Release suite; expect all tests pass.
- [x] Commit with `fix: make backup restore atomic`.

### Task 4: Surface initialization failures, enable WAL, and compare versions correctly

**Files:**
- Modify: `UniDesk/Services/DatabaseService.cs`
- Test: `UniDesk.Tests/DatabaseServiceTests.cs`

**Interfaces:**
- Produces: private `CompareDatabaseVersions(string left, string right)` for migration conditions.

- [x] Add `InitializeAsync_InvalidPath_ShouldThrow`, `InitializeAsync_ShouldEnableWalMode`, `InitializeAsync_FutureDatabaseVersion_ShouldThrowAndPreserveVersion`, and `InitializeAsync_MigrationFailure_ShouldRollbackEarlierMigrationWrites`.
- [x] Run focused tests; expect invalid path, WAL, future-version, and migration-rollback failures under the old implementation.
- [x] Configure WAL immediately after opening the connection. Wrap schema initialization in a transaction and rethrow after logging failures.
- [x] Replace ordinal string comparisons with parsed `Version` comparisons; reject invalid stored versions.
- [x] Re-run focused tests; expect pass.
- [x] Run the full Release suite; expect all tests pass.
- [x] Commit with `fix: harden database initialization`.

### Task 5: Preserve failed settings writes and synchronize mutable state

**Files:**
- Modify: `UniDesk/Services/SettingsService.cs`
- Modify: `UniDesk.Tests/SettingsServiceTests.cs`
- Modify: `UniDesk.Tests/WeatherServiceTests.cs`

**Interfaces:**
- Consumes: unchanged public `ISettingsService` save and flush methods plus `InvalidateCache` from Task 3.

- [x] Add a complete `IDatabaseService` test double whose writes can fail and later recover.
- [x] Add `FlushPendingSavesAsync_WriteFailure_ShouldThrowAndRetryPendingValues`.
- [x] Add `ConcurrentSetAndGet_ShouldPersistEveryValueWithoutErrors` with coordinated concurrent callers.
- [x] Run focused tests; expect failure because writes are swallowed or pending data is lost.
- [x] Protect cache, pending writes, and debounce source ownership with one private state lock. Swap cancellation sources under the lock and cancel/dispose the previous source outside it.
- [x] Make database write helpers log and rethrow. On batch failure, merge the failed batch back without overwriting newer values, then rethrow. The fire-and-forget debounce boundary logs exceptions.
- [x] Update the WeatherService test double with `InvalidateCache()`.
- [x] Re-run focused tests; expect pass.
- [x] Run `dotnet test UniDesk.sln -c Release --no-restore`; expect all tests pass.
- [x] Commit with `fix: make settings persistence reliable`.

### Task 6: Final verification and scope audit

**Files:**
- Review only: all changed files.

- [x] Run `dotnet test UniDesk.sln -c Release --no-restore`; expect zero failures.
- [x] Run `git diff --check`; expect no whitespace errors.
- [x] Run `git status --short` and confirm only approved stage 0-2 files changed.
- [x] Confirm no schema, CI/CD, secret, log deletion, hardware, single-instance, or UI architecture changes exist.
- [x] Commit any final documentation-only corrections with `docs: align stability design`.
