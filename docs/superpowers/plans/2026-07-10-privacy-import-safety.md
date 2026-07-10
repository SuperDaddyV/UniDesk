# UniDesk Privacy and Import Safety Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Protect user weather keys and clipboard bodies with DPAPI, migrate existing plaintext atomically, make portable backup sensitivity explicit, and prevent any import mutation before validated preview and confirmation.

**Architecture:** A versioned `IUserDataProtector` owns DPAPI encoding. Settings, clipboard storage, privacy migration, and backup restore all consume that one codec. Backup v5 separates prepare from apply, excludes secrets by default, and carries an immutable validated plan into the existing atomic restore transaction.

**Tech Stack:** .NET 9 Windows Desktop DPAPI, Microsoft.Data.Sqlite 9, System.Text.Json, WPF, xUnit 2.9.

## Global Constraints

- Follow root `AGENTS.md` and `docs/superpowers/specs/2026-07-10-runtime-privacy-architecture-design.md`.
- Do not add NuGet packages or change database schema/version.
- DPAPI scope is `CurrentUser`; stored values use `dpapi:v1:`.
- Weather keys never enter new backups; clipboard history is excluded unless the user explicitly opts in.
- Portable clipboard backup content remains plaintext and must be disclosed before file creation.
- Every migration and restore mutation must be transactional, idempotent, and tested for rollback.

---

### Task 1: Add versioned DPAPI protection

**Files:**
- Create: `UniDesk/Services/IUserDataProtector.cs`
- Create: `UniDesk/Services/DpapiUserDataProtector.cs`
- Modify: `UniDesk/App.xaml.cs`
- Test: `UniDesk.Tests/DpapiUserDataProtectorTests.cs`

**Interfaces:**
- Produces: `string Protect(string plaintext)`, `bool TryUnprotect(string storedValue, out string plaintext)`, and `bool IsProtected(string storedValue)`.

- [ ] Add tests for round-trip, empty string, wrong prefix, and malformed Base64.
- [ ] Run the focused test; expect a compile failure because the protection types do not exist.
- [ ] Implement `DpapiUserDataProtector` using `ProtectedData.Protect` and `ProtectedData.Unprotect`, `DataProtectionScope.CurrentUser`, UTF-8, fixed application entropy `SHA256.HashData("UniDesk.UserData.v1"u8.ToArray())`, and prefix `dpapi:v1:`. `TryUnprotect` catches `FormatException` and `CryptographicException`, clears the out value, and returns `false` without logging ciphertext.
- [ ] Register `IUserDataProtector` as a singleton, re-run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Services/IUserDataProtector.cs UniDesk/Services/DpapiUserDataProtector.cs UniDesk/App.xaml.cs UniDesk.Tests/DpapiUserDataProtectorTests.cs
git commit -m "feat: add versioned user data protection"
```

### Task 2: Protect the user weather key transparently

**Files:**
- Modify: `UniDesk/Services/SettingsService.cs`
- Modify: `UniDesk.Tests/SettingsServiceTests.cs`
- Modify: all direct `new SettingsService(...)` test call sites.

**Interfaces:**
- Consumes: `IUserDataProtector` from Task 1.
- Preserves: existing public `ISettingsService` plaintext semantics.

- [ ] Add a deterministic fake protector and tests asserting that `SetSettingAsync("WeatherApiKey", "secret")` stores only `fake:v1:secret`, `GetSettingAsync` returns `secret`, and a malformed protected value returns empty without exposing ciphertext.
- [ ] Run focused tests; expect raw plaintext storage under the current implementation.
- [ ] Inject `IUserDataProtector` into `SettingsService`. Add private `EncodeForStorage(key, value)` and `DecodeFromStorage(key, value)` methods that act only on `WeatherApiKey`, preserve null/empty deletion behavior, avoid double protection, and log only the key name on decode failure.
- [ ] Apply encoding inside `SaveSettingToDatabaseAsync` and decoding inside `GetSettingFromDatabaseAsync`. Keep `_cache` plaintext.
- [ ] Update tests and service construction to supply the fake or real protector. Re-run Settings tests and the full Release suite, then commit:

```powershell
git add -- UniDesk/Services/SettingsService.cs UniDesk.Tests
git commit -m "fix: protect the user weather key at rest"
```

### Task 3: Protect clipboard-history content

**Files:**
- Modify: `UniDesk/Services/QuickTextService.cs`
- Modify: `UniDesk.Tests/QuickTextServiceTests.cs`
- Modify: direct `new QuickTextService(...)` call sites.

**Interfaces:**
- Consumes: `IUserDataProtector`.
- Preserves: `IQuickTextService` returns plaintext `ClipboardHistoryItem.Content`.

- [ ] Replace the existing plaintext test with a test that queries raw SQLite and asserts the body is absent while the service returns the original text. Add a malformed protected-row test that omits that row from results and does not delete it.
- [ ] Run focused tests; expect the raw database assertion to fail.
- [ ] Inject the protector. Protect normalized content in the clipboard insert statement, retain `ComputeHash(normalized)` for `ContentHash`, and change history mapping to a `TryMapHistory` path that decrypts protected values and treats unprotected values as legacy plaintext until migration runs.
- [ ] Ensure lookup by hash and duplicate updates remain unchanged. Text snippets remain plaintext.
- [ ] Re-run QuickText tests and the full Release suite, then commit:

```powershell
git add -- UniDesk/Services/QuickTextService.cs UniDesk.Tests
git commit -m "fix: protect clipboard history at rest"
```

### Task 4: Migrate existing plaintext atomically

**Files:**
- Create: `UniDesk/Services/IPrivacyMigrationService.cs`
- Create: `UniDesk/Services/PrivacyMigrationService.cs`
- Modify: `UniDesk/App.xaml.cs`
- Test: `UniDesk.Tests/PrivacyMigrationServiceTests.cs`

**Interfaces:**
- Produces: `Task MigrateAsync()`.
- Consumes: `IDatabaseService.ExecuteInTransactionAsync` and `IUserDataProtector`.

- [ ] Add real-SQLite tests for: plaintext key and clipboard rows become protected; a second migration performs zero writes; a forced clipboard update trigger rolls back the weather-key update.
- [ ] Run focused tests; expect a compile failure because the migration service does not exist.
- [ ] Implement one transaction that queries `Settings` for `WeatherApiKey`, queries `ClipboardHistory` id/content, filters null/empty/already-protected values, protects plaintext, and updates rows by key/id. Do not alter `ContentHash`.
- [ ] Register the migration service and call it after `ISettingsService.InitializeAsync()` but before localization initialization and main-window construction. Allow failures to reach the existing startup-failure dialog.
- [ ] Invalidate the settings cache after migration so the first key read cannot return the pre-migration raw value.
- [ ] Re-run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Services/IPrivacyMigrationService.cs UniDesk/Services/PrivacyMigrationService.cs UniDesk/App.xaml.cs UniDesk.Tests/PrivacyMigrationServiceTests.cs
git commit -m "fix: migrate sensitive data to DPAPI atomically"
```

### Task 5: Define backup v5 and privacy-safe export defaults

**Files:**
- Create: `UniDesk/Models/BackupExportOptions.cs`
- Modify: `UniDesk/Services/ITodoBackupService.cs`
- Modify: `UniDesk/Services/TodoBackupService.cs`
- Modify: `UniDesk/ViewModels/SettingsViewModel.cs`
- Modify: four localization dictionaries.
- Test: `UniDesk.Tests/TodoBackupServiceTests.cs`

**Interfaces:**
- Produces: `Task ExportToFileAsync(string filePath, BackupExportOptions? options = null)`.
- Produces: `BackupExportOptions(bool IncludeClipboardHistory = false)`.

- [ ] Add export tests asserting: default JSON has version 5, omits `WeatherApiKey`, and has no clipboard section; explicit inclusion emits plaintext clipboard content and `containsSensitivePlaintext: true`.
- [ ] Update the future-version rejection test from version 5 to version 6, then run focused tests; expect failures under backup v4 behavior.
- [ ] Increment `CurrentBackupVersion` to 5. Always add `WeatherApiKey` to excluded setting keys. Add v5 metadata fields `includedSections` and `containsSensitivePlaintext`. Populate clipboard entries only when `IncludeClipboardHistory` is true.
- [ ] In `SettingsViewModel.BackupTodosAsync`, ask whether to include clipboard history before opening the save dialog. If included, show a second localized warning that the portable JSON contains readable clipboard text; cancellation returns before export.
- [ ] Preserve v1-v4 import acceptance. Re-run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Models/BackupExportOptions.cs UniDesk/Services/ITodoBackupService.cs UniDesk/Services/TodoBackupService.cs UniDesk/ViewModels/SettingsViewModel.cs UniDesk/Resources/Strings.*.xaml UniDesk.Tests/TodoBackupServiceTests.cs
git commit -m "feat: add privacy-safe backup v5 exports"
```

### Task 6: Split import into prepare, preview, and apply

**Files:**
- Create: `UniDesk/Models/BackupImportPreview.cs`
- Create: `UniDesk/Models/BackupShortcutPreview.cs`
- Create: `UniDesk/Services/BackupImportPlan.cs`
- Create: `UniDesk/Windows/BackupImportPreviewWindow.xaml`
- Create: `UniDesk/Windows/BackupImportPreviewWindow.xaml.cs`
- Modify: `UniDesk/Services/ITodoBackupService.cs`
- Modify: `UniDesk/Services/TodoBackupService.cs`
- Modify: `UniDesk/ViewModels/SettingsViewModel.cs`
- Modify: four localization dictionaries.
- Test: `UniDesk.Tests/TodoBackupServiceTests.cs`

**Interfaces:**
- Produces: `Task<BackupImportPlan> PrepareImportAsync(string filePath)`.
- Produces: `Task<TodoBackupImportResult> ApplyImportAsync(BackupImportPlan plan)`.
- `BackupImportPlan` exposes `BackupImportPreview Preview` and keeps the normalized backup document internal.

- [ ] Add tests proving `PrepareImportAsync` performs no write, preview lists every shortcut path/argument, an invalid file fails before a plan exists, and `ApplyImportAsync` preserves the existing rollback test.
- [ ] Run focused tests; expect compile failures for missing plan APIs.
- [ ] Move file read, deserialization, validation, version checks, and normalization into `PrepareImportAsync`. Build immutable preview counts and a shortcut list where `IsRisky` is true for non-empty arguments, URI paths, or `.exe`, `.com`, `.bat`, `.cmd`, `.ps1`, `.vbs`, `.js`, `.msi`, and `.lnk` extensions.
- [ ] Move flush, transaction, cache invalidation, and icon refresh into `ApplyImportAsync`. Reject null or externally constructed plans by making the plan constructor internal.
- [ ] Before direct inserts, use `IUserDataProtector` to encode legacy `WeatherApiKey` settings and clipboard bodies. Preserve clipboard hash recomputation from plaintext.
- [ ] Build a scrollable preview window bound to `BackupImportPreview`, showing section counts, sensitive-plaintext warning, and every shortcut path/argument with risky rows emphasized. It returns `true` only from the explicit import button.
- [ ] Change restore UI order to select file, prepare, show preview, and apply only after confirmation. Remove the old pre-file confirmation.
- [ ] Re-run focused and full Release tests, then commit:

```powershell
git add -- UniDesk/Models/BackupImportPreview.cs UniDesk/Models/BackupShortcutPreview.cs UniDesk/Services/BackupImportPlan.cs UniDesk/Windows/BackupImportPreviewWindow.xaml UniDesk/Windows/BackupImportPreviewWindow.xaml.cs UniDesk/Services/ITodoBackupService.cs UniDesk/Services/TodoBackupService.cs UniDesk/ViewModels/SettingsViewModel.cs UniDesk/Resources/Strings.*.xaml UniDesk.Tests/TodoBackupServiceTests.cs
git commit -m "feat: preview validated backups before import"
```

### Task 7: Remove insecure location fallback and extend sensitive filtering

**Files:**
- Modify: `UniDesk/Helpers/LocationProvider.cs`
- Modify: `UniDesk/Services/QuickTextService.cs`
- Modify: `UniDesk.Tests/QuickTextServiceTests.cs`
- Create: `UniDesk.Tests/LocationProviderTests.cs`

- [ ] Add sensitive positive cases for `sk-proj-...`, `ghp_...`, `github_pat_...`, `AKIA...`, and `-----BEGIN PRIVATE KEY-----`; add nearby negative cases such as `sketch-project` and ordinary words containing `session` only as part of a larger harmless word.
- [ ] Change `GetCityByAmapIpAsync` to `protected virtual`, derive a test provider that returns null from that method, and add a behavior test proving `ResolveCityAsync` returns the saved city. The final source search separately proves the HTTP endpoint is absent.
- [ ] Run focused tests; expect at least the raw-key cases to fail.
- [ ] Remove `IpApiEndpoint`, `IpApiResponse`, and `GetLocationByIpAsync`. `ResolveCityAsync` tries only HTTPS Amap automatic location and then saved city. `GetLocationAsync` returns null because no approved coordinate-only provider remains.
- [ ] Add anchored/generated regexes for the approved secret forms. Keep keyword detection for labeled values, but avoid substring-only false positives by applying word-boundary or separator rules.
- [ ] Re-run focused and full Release tests. Run `rg -n "http://ip-api\.com" UniDesk`; expect no matches. Commit:

```powershell
git add -- UniDesk/Helpers/LocationProvider.cs UniDesk/Services/QuickTextService.cs UniDesk.Tests/QuickTextServiceTests.cs UniDesk.Tests/LocationProviderTests.cs
git commit -m "fix: remove insecure location fallback"
```

### Task 8: Phase 4 verification and documentation

**Files:**
- Modify: `docs/DESIGN.md`
- Modify: `docs/superpowers/plans/2026-07-10-privacy-import-safety.md`

- [ ] Record DPAPI scope, prefix, migration timing, unreadable-data behavior, v5 backup privacy defaults, preview/apply separation, and location fallback removal.
- [ ] Run `dotnet test UniDesk.sln -c Release --no-restore`; expect zero failures.
- [ ] Query a test SQLite database and assert no known weather key or clipboard fixture appears in raw text fields.
- [ ] Run `git diff --check`, `git status --short`, and a scope review; expect only phase 4 documentation changes after implementation commits.
- [ ] Mark plan checkboxes complete and commit:

```powershell
git add -- docs/DESIGN.md docs/superpowers/plans/2026-07-10-privacy-import-safety.md
git commit -m "docs: record privacy and import protections"
```
