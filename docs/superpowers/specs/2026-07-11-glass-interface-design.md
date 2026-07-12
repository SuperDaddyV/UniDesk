# UniDesk Glass 2.0 Interface Design

**Date:** 2026-07-11  
**Status:** Implemented; compatibility amendment recorded on 2026-07-12
**Scope:** Shared visual redesign integrated with the separately specified global search and system-theme features. No database schema changes.

## Goal

Give UniDesk v2.0.0 a visibly coherent identity by refining the main desktop sidebar and rebuilding Settings as a medium-width glass settings center. Reserve integrated surfaces for the approved global search and system-theme features while preserving existing module behavior and supported Windows versions.

## Product Direction

UniDesk remains a lightweight vertical desktop sidebar. The main window keeps its current `320px` to `520px` width range and single-column module order. The redesign must not turn it into a wide dashboard or introduce new navigation into the main window.

Settings becomes an independent `720px × 620px` center instead of inheriting the narrow main-panel width. Its minimum usable size is `680px × 560px`; placement is clamped to the current monitor work area.

## Visual System

The application uses one shared glass system across the main window and Settings:

- Window radius: `16px`.
- Section/card radius: `12px`.
- Input/button radius: `8px`.
- Outer spacing: `16px`; card spacing: `12px`; compact control spacing: `8px`.
- Borders use a one-pixel translucent highlight; only top-level windows receive a soft shadow.
- Primary text and icons remain fully opaque. The existing `WindowOpacity` value affects the glass background layer only.
- Theme tint, primary text, secondary text, divider, highlight, accent, card, input, hover, and selection brushes come from the application theme dictionaries. Settings must not define a second hard-coded light palette.
- Hover, pressed, selected, disabled, and keyboard-focus states must remain distinguishable on bright and dark wallpapers.

## Transparent Backdrop

The main window and Settings are WPF layered windows (`AllowsTransparency=True`). Their glass surface is rendered exclusively with the existing translucent WPF theme brushes so the saved opacity value reveals the real desktop consistently on supported Windows versions. No Windows App SDK or other NuGet dependency is added.

The Windows 11 `DWMWA_SYSTEMBACKDROP_TYPE` material must not be applied to these layered windows. Validation on 2026-07-12 showed that combining the DWM system material with WPF layered transparency creates an opaque rectangular host surface: lowering opacity reveals a white or black system layer instead of the desktop, and the rectangle protrudes beyond the rounded WPF content. Keeping one WPF composition path preserves both true transparency and transparent outer corners.

## Main Window

The main window keeps the current module controls and data bindings. The visual changes are limited to:

- Separate background opacity from content opacity.
- Use a dedicated glass background layer under the title bar and module content.
- Normalize title-bar height, icon-button hit areas, card radii, inner padding, and section spacing.
- Keep the time/weather module as the leading visual card and keep all modules in one vertical column.
- Preserve current panel width, height, scrolling, drag behavior, top-most behavior, lock behavior, and collapse behavior.

No dashboard module is added, removed, renamed, or functionally changed. Global search is a title-bar workspace rather than a dashboard module.

## Settings Center

Settings uses a two-column layout:

- Left navigation: `160px` wide, with icon and localized title.
- Right content: one selected settings page with its own vertical scroll viewer.
- Footer: fixed reset/cancel/save actions that remain visible while content scrolls.
- Header: unified title treatment and close action, with the existing drag behavior.

The left navigation is visually integrated into the window glass rather than rendered as one large opaque rectangular panel. Its background and right divider remain transparent; spacing separates it from the page content. Only the active navigation item uses a rounded highlight pill, while hover and keyboard-focus states use the same lightweight rounded treatment. This preserves the two-column structure without introducing sharp internal corners.

The navigation pages are:

1. General: language, startup, and weather API settings.
2. Appearance: theme, follow-system theme settings, display title, background opacity, panel width, panel height, and font scale.
3. Modules: module visibility and order.
4. Desktop experience: clipboard-history and sensitive-content controls.
5. Data and backup: backup, restore, clear history, reset layout, and reset defaults.
6. Shortcuts: main-panel shortcut count.
7. About: current version, update status, and update check.

Every existing binding and command remains connected to the same `SettingsViewModel` member. Page switching is presentation-only and is not persisted.

## Localization

New navigation and section labels are added to all existing resource dictionaries:

- Simplified Chinese
- English
- Japanese
- Spanish

No user-facing label may be hard-coded in `SettingsWindow.xaml` except product names and protocol terms such as `API Host` and `API Key` that already exist.

## Error Handling and Accessibility

- Window creation does not depend on DWM backdrop availability or calls.
- Existing save, cancel, API editing, backup, restore, and update error behavior is unchanged.
- Navigation items expose readable text and keyboard focus.
- Text contrast is provided by opaque text brushes and a tinted content layer, not by reducing text opacity.
- The settings window remains usable at `680px × 560px` and at Windows display scaling up to at least `150%`.

## Verification

Implementation is accepted only when:

- Structural regression tests prove that Settings uses the shared glass resources, seven navigation pages, fixed dimensions, and a fixed footer.
- Structural regression tests prove that the Settings sidebar has no opaque panel background or vertical divider and retains rounded navigation pills.
- Structural regression tests prove that the main content container no longer binds `Opacity` and that only the background layer binds `WindowOpacity`.
- A regression test proves that both layered glass windows do not request a rectangular DWM backdrop.
- All localized dictionaries contain the new navigation keys.
- `dotnet build UniDesk.sln -c Release --no-restore` succeeds with zero errors and warnings.
- `dotnet test UniDesk.sln -c Release --no-build` passes in full.
- A local visual smoke test checks the main window and every Settings page at normal scale and verifies readable bright-wallpaper contrast.

## Out of Scope

- Command palette, reminders, notifications, or other product features beyond the separately approved global search and system-theme specifications.
- Database schema or data migration.
- Changes to backup semantics.
- WinUI migration, Windows App SDK, new UI framework, or new NuGet packages.
- GitHub publication or installer replacement before separate user approval.
