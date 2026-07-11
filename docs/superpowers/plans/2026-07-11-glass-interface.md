# UniDesk Glass 2.0 Interface Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild Settings as a 720px shared-theme glass settings center and refine the main sidebar glass hierarchy without adding product features.

**Architecture:** Keep WPF and the existing view models. Put reusable colors and control styles in the existing theme dictionaries, keep page selection as a view-only `ListBox`/`TabControl` binding, isolate DWM capability detection in one helper, and preserve a theme-brush fallback for Windows 10 and failed DWM calls.

**Tech Stack:** .NET 9, WPF XAML, C#, xUnit, Win32 DWM P/Invoke already available from the OS.

## Global Constraints

- Support Windows 10 1903+ and Windows 11.
- Add no NuGet package and no second UI framework.
- Do not change database schema, persistence formats, commands, or end-user workflows.
- Keep the main window between `320px` and `520px` wide and retain its single-column module order.
- Settings is `720px × 620px`, with a minimum of `680px × 560px` and work-area-clamped placement.
- `WindowOpacity` affects only the glass background layer; text, icons, and controls remain fully opaque.
- New labels must exist in Simplified Chinese, English, Japanese, and Spanish.

---

### Task 1: Lock the visual contracts with failing tests

**Files:**
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`
- Create: `UniDesk.Tests/BackdropMaterialServiceTests.cs`

**Interfaces:**
- Consumes: repository XAML files and the planned `BackdropMaterialService.IsSupported(Version)` method.
- Produces: regression contracts for shared Settings resources, seven navigation pages, background-only opacity, and Windows build capability.

- [ ] **Step 1: Add structural XAML regression tests**

Add tests that read `SettingsWindow.xaml`, `MainWindow.xaml`, and all four language dictionaries. Assert these exact contracts:

```csharp
Assert.Contains("Width=\"720\"", settingsXaml, StringComparison.Ordinal);
Assert.Contains("MinWidth=\"680\"", settingsXaml, StringComparison.Ordinal);
Assert.Contains("x:Name=\"SettingsNavigation\"", settingsXaml, StringComparison.Ordinal);
Assert.Contains("x:Name=\"SettingsPages\"", settingsXaml, StringComparison.Ordinal);
Assert.Equal(7, Regex.Matches(settingsXaml, "<TabItem").Count);
Assert.DoesNotContain("x:Key=\"DlgBackground\"", settingsXaml, StringComparison.Ordinal);
Assert.Contains("x:Name=\"MainGlassBackground\"", mainXaml, StringComparison.Ordinal);
Assert.Contains("Opacity=\"{Binding WindowOpacity}\"", mainXaml, StringComparison.Ordinal);
Assert.DoesNotContain("x:Name=\"WindowContainer\"\r\n                Style=\"{StaticResource GlassWindowBorderStyle}\"\r\n                ClipToBounds=\"True\"\r\n                Opacity=", mainXaml, StringComparison.Ordinal);
```

For every language file, assert the seven keys `Settings.NavGeneral`, `Settings.NavAppearance`, `Settings.NavModules`, `Settings.NavDesktop`, `Settings.NavData`, `Settings.NavShortcuts`, and `Settings.NavAbout`.

- [ ] **Step 2: Add backdrop capability tests**

Create tests for the intended pure capability check:

```csharp
[Theory]
[InlineData(10, 0, 19045, false)]
[InlineData(10, 0, 22000, false)]
[InlineData(10, 0, 22621, true)]
[InlineData(10, 0, 26100, true)]
public void IsSupported_ShouldRequireWindows11Build22621(
    int major, int minor, int build, bool expected)
{
    Assert.Equal(expected, BackdropMaterialService.IsSupported(new Version(major, minor, build)));
}
```

- [ ] **Step 3: Run the focused tests and confirm RED**

Run:

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WpfInteractionRegressionTests|FullyQualifiedName~BackdropMaterialServiceTests"
```

Expected: failure because the new XAML structure and `BackdropMaterialService` do not exist.

- [ ] **Step 4: Commit the red tests**

```powershell
git add UniDesk.Tests/WpfInteractionRegressionTests.cs UniDesk.Tests/BackdropMaterialServiceTests.cs
git commit -m "test: define Glass 2.0 interface contracts"
```

### Task 2: Add the shared glass resources and DWM fallback helper

**Files:**
- Modify: `UniDesk/Resources/Themes/Shared.xaml`
- Modify: `UniDesk/Resources/Themes/Light.xaml`
- Modify: `UniDesk/Resources/Themes/Dark.xaml`
- Create: `UniDesk/Helpers/BackdropMaterialService.cs`
- Modify: `UniDesk.Tests/ModuleResourceScopeTests.cs`

**Interfaces:**
- Produces: `BackdropMaterialService.Apply(Window, BackdropKind): bool`, `BackdropMaterialService.IsSupported(Version): bool`, and `BackdropKind.MainWindow|TransientWindow`.
- Produces shared resources used by both top-level windows: `GlassWindowBackgroundBrush`, `GlassSidebarBrush`, `GlassCardBrush`, `GlassInputBrush`, `GlassHoverBrush`, `GlassSelectedBrush`, `GlassBorderBrush`, `GlassWindowStyle`, `GlassNavigationItemStyle`, and `GlassSectionStyle`.

- [ ] **Step 1: Extend the resource-scope test**

Add Settings and both top-level windows to resource resolution coverage so every new `{StaticResource ...}` key must resolve from application or local scope.

- [ ] **Step 2: Run the resource test and confirm RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~ModuleResourceScopeTests
```

Expected: failure after the test references the planned keys and Settings still owns its local palette.

- [ ] **Step 3: Add theme colors and shared styles**

Add light and dark values for all planned brushes. Keep text brushes opaque. Define the reusable navigation, section, input, chip, secondary button, and primary button styles in `Shared.xaml`; do not duplicate them in Settings.

- [ ] **Step 4: Implement the no-dependency DWM helper**

Use `DwmSetWindowAttribute` with `DWMWA_SYSTEMBACKDROP_TYPE = 38`. Return `false` before the call unless `Environment.OSVersion.Version` is Windows build `22621` or newer. Map `MainWindow` to `DWMSBT_MAINWINDOW = 2` and `TransientWindow` to `DWMSBT_TRANSIENTWINDOW = 3`. Catch `DllNotFoundException`, `EntryPointNotFoundException`, and failed HRESULTs by returning `false`; do not log or throw from the visual helper.

- [ ] **Step 5: Run focused tests and confirm GREEN**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~BackdropMaterialServiceTests|FullyQualifiedName~ModuleResourceScopeTests"
```

Expected: all selected tests pass.

- [ ] **Step 6: Commit shared glass infrastructure**

```powershell
git add UniDesk/Resources/Themes UniDesk/Helpers/BackdropMaterialService.cs UniDesk.Tests/ModuleResourceScopeTests.cs
git commit -m "feat: add shared Glass 2.0 visual system"
```

### Task 3: Separate main-window background opacity from content

**Files:**
- Modify: `UniDesk/MainWindow.xaml`
- Modify: `UniDesk/MainWindow.xaml.cs`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Consumes: shared glass resources and `BackdropMaterialService.Apply`.
- Preserves: all existing `MainWindowViewModel` bindings, module controls, drag handlers, and commands.

- [ ] **Step 1: Tighten the main-window structural test**

Assert that only `MainGlassBackground` has `Opacity="{Binding WindowOpacity}"`, while `WindowContainer` has no `Opacity` attribute.

- [ ] **Step 2: Run the focused test and confirm RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WpfInteractionRegressionTests
```

Expected: failure because opacity still applies to the whole container.

- [ ] **Step 3: Add a dedicated glass background layer**

Keep `WindowContainer` as the clipping and border container. Add `MainGlassBackground` as the first child under it, bind only that layer to `WindowOpacity`, and render the title bar and module scroll viewer above it. Normalize header hit targets and spacing through the shared styles without changing commands or module order.

- [ ] **Step 4: Apply the system backdrop best-effort**

In `SourceInitialized`, call:

```csharp
BackdropMaterialService.Apply(this, BackdropKind.MainWindow);
```

The WPF glass layer remains visible as the fallback.

- [ ] **Step 5: Run focused tests and confirm GREEN**

Run the same focused test command. Expected: all `WpfInteractionRegressionTests` pass.

- [ ] **Step 6: Commit the main-window visual refinement**

```powershell
git add UniDesk/MainWindow.xaml UniDesk/MainWindow.xaml.cs UniDesk.Tests/WpfInteractionRegressionTests.cs
git commit -m "feat: refine main window glass hierarchy"
```

### Task 4: Rebuild Settings as a seven-page glass center

**Files:**
- Modify: `UniDesk/SettingsWindow.xaml`
- Modify: `UniDesk/SettingsWindow.xaml.cs`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Consumes: the existing `SettingsViewModel` without adding persistence properties.
- Produces: `SettingsNavigation` `ListBox`, `SettingsPages` `TabControl`, and seven view-only pages with unchanged command bindings.

- [ ] **Step 1: Run the Settings structural tests and confirm RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WpfInteractionRegressionTests
```

Expected: failure because Settings is still narrow and single-column.

- [ ] **Step 2: Replace the local palette with shared theme resources**

Remove `DlgBackground`, `DlgTextPrimary`, `DlgTextSecondary`, `DlgBorder`, `DlgInputBackground`, `DlgAccent`, and `DlgAccentSoft`. Replace their uses with the shared glass and theme brushes.

- [ ] **Step 3: Build the two-column shell**

Set `Width="720"`, `Height="620"`, `MinWidth="680"`, and `MinHeight="560"`. Add a `160px` navigation column and a flexible content column. Bind `SettingsPages.SelectedIndex` to `SettingsNavigation.SelectedIndex`. Keep header drag, close, and footer actions outside the page scroll viewers.

- [ ] **Step 4: Move existing controls into seven pages**

Move, without changing bindings:

- General: language, startup, weather API.
- Appearance: color scheme, display title, opacity, width, height, font scale.
- Modules: `ModuleSettings` list.
- Desktop experience: clipboard-history enable/filter/limit.
- Data and backup: clear history, backup, restore, reset layout, reset defaults.
- Shortcuts: `ShortcutMaxCount` chips.
- About: current version, update status, update check.

- [ ] **Step 5: Update size and placement code**

Replace owner-ratio sizing with the fixed preferred size clamped to `SystemParameters.WorkArea`. Keep owner-centered placement when it fits and clamp `Left` and `Top` so the entire window remains on screen. Update drag and scroll cleanup to operate on the selected page scroll viewer rather than one global scroll viewer.

- [ ] **Step 6: Apply transient backdrop and preserve fallback**

In `SourceInitialized`, call:

```csharp
BackdropMaterialService.Apply(this, BackdropKind.TransientWindow);
```

- [ ] **Step 7: Run focused tests and build**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WpfInteractionRegressionTests|FullyQualifiedName~ModuleResourceScopeTests"
dotnet build UniDesk.sln -c Release --no-restore
```

Expected: focused tests pass and build exits `0` without XAML errors.

- [ ] **Step 8: Commit the Settings center**

```powershell
git add UniDesk/SettingsWindow.xaml UniDesk/SettingsWindow.xaml.cs UniDesk.Tests/WpfInteractionRegressionTests.cs
git commit -m "feat: rebuild settings as glass center"
```

### Task 5: Localize and verify all visual states

**Files:**
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Modify: `docs/DESIGN.md`
- Modify: `docs/release-unidesk.md`

**Interfaces:**
- Produces the seven navigation keys in every supported language.
- Documents visual-only v2.0.0 changes without claiming new product functionality.

- [ ] **Step 1: Run localization structural tests and confirm RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter FullyQualifiedName~WpfInteractionRegressionTests
```

Expected: failure until all four dictionaries contain every navigation key.

- [ ] **Step 2: Add all localized navigation labels**

Add semantically equivalent labels for General, Appearance, Modules, Desktop experience, Data and backup, Shortcuts, and About in all four dictionaries.

- [ ] **Step 3: Update design and release documentation**

Document the shared glass resources, background-only opacity semantics, Settings page structure, Windows 11 DWM behavior, and Windows 10 fallback.

- [ ] **Step 4: Run localization tests and confirm GREEN**

Run the same focused command. Expected: all selected tests pass.

- [ ] **Step 5: Commit localization and documentation**

```powershell
git add UniDesk/Resources/Strings.*.xaml docs/DESIGN.md docs/release-unidesk.md
git commit -m "docs: localize and document Glass 2.0 interface"
```

### Task 6: Full verification and visual smoke test

**Files:**
- Modify only files required to correct defects found during verification.

**Interfaces:**
- Produces a verified build ready for a new user test package, but does not publish or replace the installer without separate approval.

- [ ] **Step 1: Restore and build**

```powershell
dotnet restore UniDesk.sln
dotnet build UniDesk.sln -c Release --no-restore
```

Expected: zero errors and zero warnings.

- [ ] **Step 2: Run the complete test suite**

```powershell
dotnet test UniDesk.sln -c Release --no-build
```

Expected: all tests pass with zero skipped failures.

- [ ] **Step 3: Perform local visual smoke testing**

Launch the Release build and verify:

- Main-window text and icons remain opaque while changing background opacity.
- Main-window module scrolling, dragging, collapse, lock, and Settings opening still work.
- Settings opens at the independent medium width and remains fully visible in the work area.
- Every navigation page opens and every existing control remains interactive.
- Save and Cancel preserve existing behavior.
- Bright and dark theme tints remain readable over a bright wallpaper.
- Windows 11 backdrop failure or disablement leaves the WPF fallback readable.

- [ ] **Step 4: Inspect the final diff**

```powershell
git diff d8ee99b --check
git status --short
git diff --stat d8ee99b..HEAD
```

Expected: no whitespace errors and only interface-design files, tests, and documentation are changed.

- [ ] **Step 5: Commit verification fixes if needed**

Use one narrow commit describing the exact verified defect. Do not amend unrelated commits.
