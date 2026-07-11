# UniDesk Glass 2.0 Interface Design

**Date:** 2026-07-11  
**Status:** Approved for implementation  
**Scope:** Visual redesign only. No new end-user features, data schema changes, or workflow changes.

## Goal

Give UniDesk v2.0.0 a visibly coherent identity by refining the main desktop sidebar and rebuilding Settings as a medium-width glass settings center. Preserve all existing commands, persistence semantics, module behavior, and supported Windows versions.

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

## System Backdrop and Fallback

On Windows 11 build 22621 or later, UniDesk attempts to apply the public DWM system backdrop attribute. The main window requests the long-lived main-window material; Settings requests the transient-window material. The call is best-effort and never blocks window creation.

Windows 10, older Windows 11 builds, disabled composition, remote sessions, and failed DWM calls use the existing WPF translucent theme brushes as the fallback. No Windows App SDK or other NuGet dependency is added.

The fallback glass layer is always present at a low tint so content remains readable if the system material is unavailable. The system backdrop helper owns only capability detection and DWM calls; it does not own theme selection or settings persistence.

## Main Window

The main window keeps the current module controls and data bindings. The visual changes are limited to:

- Separate background opacity from content opacity.
- Use a dedicated glass background layer under the title bar and module content.
- Normalize title-bar height, icon-button hit areas, card radii, inner padding, and section spacing.
- Keep the time/weather module as the leading visual card and keep all modules in one vertical column.
- Preserve current panel width, height, scrolling, drag behavior, top-most behavior, lock behavior, and collapse behavior.

No module is added, removed, renamed, or functionally changed.

## Settings Center

Settings uses a two-column layout:

- Left navigation: `160px` wide, with icon and localized title.
- Right content: one selected settings page with its own vertical scroll viewer.
- Footer: fixed reset/cancel/save actions that remain visible while content scrolls.
- Header: unified title treatment and close action, with the existing drag behavior.

The navigation pages are:

1. General: language, startup, and weather API settings.
2. Appearance: theme, display title, background opacity, panel width, panel height, and font scale.
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

- DWM backdrop failures are swallowed only after returning the fallback result; window creation continues.
- Existing save, cancel, API editing, backup, restore, and update error behavior is unchanged.
- Navigation items expose readable text and keyboard focus.
- Text contrast is provided by opaque text brushes and a tinted content layer, not by reducing text opacity.
- The settings window remains usable at `680px × 560px` and at Windows display scaling up to at least `150%`.

## Verification

Implementation is accepted only when:

- Structural regression tests prove that Settings uses the shared glass resources, seven navigation pages, fixed dimensions, and a fixed footer.
- Structural regression tests prove that the main content container no longer binds `Opacity` and that only the background layer binds `WindowOpacity`.
- Backdrop capability tests cover Windows 10, Windows 11 before build 22621, and Windows 11 build 22621 or later.
- All localized dictionaries contain the new navigation keys.
- `dotnet build UniDesk.sln -c Release --no-restore` succeeds with zero errors and warnings.
- `dotnet test UniDesk.sln -c Release --no-build` passes in full.
- A local visual smoke test checks the main window and every Settings page at normal scale and verifies readable bright-wallpaper contrast.

## Out of Scope

- Global search, command palette, reminders, notifications, or other new product features.
- Database schema or data migration.
- Changes to backup semantics.
- WinUI migration, Windows App SDK, new UI framework, or new NuGet packages.
- GitHub publication or installer replacement before separate user approval.
