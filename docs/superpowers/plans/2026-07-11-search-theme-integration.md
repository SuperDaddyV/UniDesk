# Global Search and System Theme Integration Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Integrate five-kind global search and follow-system light/dark themes into the approved Glass 2.0 main window and Settings center.

**Architecture:** Add two independent services, `ISearchService` and `ISystemThemeService`, with focused view models and no database schema change. Search uses bounded escaped SQLite reads and delegates activation to existing module view models; system-theme observation remains the only static-event subscriber and applies schemes through `AppColorSchemeCatalog`.

**Tech Stack:** .NET 9, WPF, Microsoft.Data.Sqlite, CommunityToolkit.Mvvm, xUnit, Windows registry and `SystemEvents` already available in the framework.

## Global Constraints

- Add no NuGet dependency and make no database schema change.
- Search only local UniDesk data and return at most five items per kind.
- Keep SQL and debounce logic out of `MainWindowViewModel`.
- Add all labels in Simplified Chinese, English, Japanese, and Spanish.
- Follow-system defaults are `Taro` for light and `DarkGrey` for dark.

---

### Task 1: Search domain and database service

**Files:**
- Create: `UniDesk/Models/SearchResultItem.cs`
- Create: `UniDesk/Services/ISearchService.cs`
- Create: `UniDesk/Services/SearchService.cs`
- Create: `UniDesk.Tests/SearchServiceTests.cs`
- Modify: `UniDesk/App.xaml.cs`

**Interfaces:**
- Produces: `Task<IReadOnlyList<SearchResultItem>> SearchAsync(string keyword, int limitPerKind = 5, CancellationToken cancellationToken = default)`.
- `SearchResultItem` contains `SearchResultKind Kind`, `int Id`, `string Title`, `string Snippet`, and `string ActionValue`.

- [ ] Write tests for literal `%`, `_`, and `\\`, five result kinds, incomplete-Todo ordering, empty input, and per-kind limit.
- [ ] Run `dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~SearchServiceTests` and confirm compile/test failure because the types do not exist.
- [ ] Implement the models and one bounded query per kind through `IDatabaseService.QueryAsync`, sequentially with cancellation checks. Escape patterns with `ESCAPE '\\'`; catch and log per-kind failures while retaining successful groups.
- [ ] Register `ISearchService` as singleton and rerun the focused tests to green.
- [ ] Commit with `git commit -m "feat: add local global search service"`.

### Task 2: Search view model and module activation

**Files:**
- Create: `UniDesk/ViewModels/SearchResultGroupViewModel.cs`
- Create: `UniDesk/ViewModels/SearchViewModel.cs`
- Create: `UniDesk.Tests/SearchViewModelTests.cs`
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/ViewModels/QuickNotesViewModel.cs`
- Modify: `UniDesk/ViewModels/TodosViewModel.cs`
- Modify: `UniDesk/ViewModels/QuickTextViewModel.cs`
- Modify: `UniDesk/ViewModels/ShortcutsViewModel.cs`

**Interfaces:**
- Produces: `SearchViewModel.OpenCommand`, `CloseCommand`, `ActivateResultCommand`, `SearchText`, `Groups`, `IsOpen`, and `StatusText`.
- Produces module methods `OpenSearchResultAsync`, `HighlightSearchResultAsync`, `CopySearchResultAsync`, and `LaunchSearchResultAsync` using existing services and commands.

- [ ] Write view-model tests for empty query, 250ms supersession, grouping order, failure status, and activation callback.
- [ ] Run the focused tests and confirm RED because `SearchViewModel` is missing.
- [ ] Implement `SearchViewModel` using the existing `Debouncer`; make stale queries unable to replace newer results.
- [ ] Add narrow public activation methods to module view models and coordinate them in `MainWindowViewModel` without adding SQL.
- [ ] Run search service and view-model tests to green and commit with `git commit -m "feat: coordinate global search results"`.

### Task 3: Search glass interface

**Files:**
- Modify: `UniDesk/MainWindow.xaml`
- Modify: `UniDesk/MainWindow.xaml.cs`
- Modify: `UniDesk/Controls/TodosModuleView.xaml`
- Modify: `UniDesk/Controls/TodosModuleView.xaml.cs`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel.Search` and the shared Glass 2.0 resources.
- Produces keyboard `Ctrl+F`, Escape close, focused search box, result activation, Todo module bring-into-view, and two-second highlight.

- [ ] Add structural tests for `SearchButton`, `GlobalSearchBox`, `SearchSurface`, `Ctrl+F`, five group templates, and Todo highlight state; run to confirm RED.
- [ ] Add the search title-bar button and glass surface below the title row while preserving the one-column module layout.
- [ ] Add keyboard focus/navigation and module bring-into-view in code-behind; use a shared highlight brush for the Todo row.
- [ ] Run WPF regression tests and build to green; commit with `git commit -m "feat: add glass global search interface"`.

### Task 4: System-theme service

**Files:**
- Create: `UniDesk/Services/ISystemThemeService.cs`
- Create: `UniDesk/Services/SystemThemeService.cs`
- Create: `UniDesk/Helpers/SystemThemeSelection.cs`
- Create: `UniDesk.Tests/SystemThemeServiceTests.cs`
- Modify: `UniDesk/App.xaml.cs`

**Interfaces:**
- Produces: `ISystemThemeService.IsLightTheme`, `ThemeChanged`, `Initialize()`, and `Dispose()`.
- Produces: `SystemThemeSelection.GetEffectiveScheme(bool followSystem, bool isSystemLight, string manual, string light, string dark)`.

- [ ] Write failing pure tests for missing/zero/nonzero registry values and effective scheme selection.
- [ ] Implement registry reading, dispatcher-marshaled `SystemEvents.UserPreferenceChanged`, deterministic unsubscription, and the pure selector.
- [ ] Register and initialize/dispose the singleton through `App`; run focused tests to green.
- [ ] Commit with `git commit -m "feat: add follow-system theme service"`.

### Task 5: Appearance settings integration

**Files:**
- Modify: `UniDesk/ViewModels/SettingsViewModel.cs`
- Modify: `UniDesk/SettingsWindow.xaml`
- Modify: `UniDesk/Services/DatabaseService.cs`
- Modify: `UniDesk.Tests/SettingsServiceTests.cs`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Persists: `FollowSystemTheme`, `ColorSchemeLight`, and `ColorSchemeDark` through existing settings APIs.
- Preserves: existing `ColorScheme` as the manual selection restored when follow-system is disabled.

- [ ] Write failing tests for defaults, preview, save, cancel, and manual selection restoration.
- [ ] Add default setting values without changing schema; implement view-model preview and event subscription disposal.
- [ ] Add Appearance-page controls using existing color options and shared glass styles.
- [ ] Run Settings and WPF tests to green; commit with `git commit -m "feat: integrate follow-system appearance settings"`.

### Task 6: Localization, documentation, and full verification

**Files:**
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Modify: `docs/DESIGN.md`
- Modify: `docs/release-unidesk.md`

**Interfaces:**
- Produces complete localized search and system-theme labels and accurate v2.0.0 release notes.

- [ ] Add structural localization tests and confirm RED.
- [ ] Add all four language sets and update design/release documentation.
- [ ] Run `dotnet restore UniDesk.sln`, `dotnet build UniDesk.sln -c Release --no-restore`, and `dotnet test UniDesk.sln -c Release --no-build`.
- [ ] Perform a local visual smoke test of both themes, all Settings pages, search actions, opacity, and existing module interactions.
- [ ] Run `git diff --check` and inspect final scope before packaging.
