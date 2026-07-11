# UniDesk Follow-System Theme Design

**Date:** 2026-07-11  
**Status:** Approved for implementation

## Goal

Allow UniDesk to follow the Windows application light/dark preference and switch between user-selected light and dark UniDesk color schemes without restarting.

## Behavior

- Appearance settings add a `Follow system` switch plus light-scheme and dark-scheme selectors.
- Defaults are `Taro` for light and `DarkGrey` for dark.
- When following is enabled, Windows `AppsUseLightTheme` selects the effective scheme.
- When following is disabled, the user's existing manual `ColorScheme` remains effective.
- Changes are applied on the WPF dispatcher and do not flash to an intermediate palette.

## Architecture

- `ISystemThemeService` exposes `IsLightTheme`, `ThemeChanged`, `Initialize()`, and `Dispose()`.
- `SystemThemeService` reads `HKCU\\Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize\\AppsUseLightTheme`, subscribes to `SystemEvents.UserPreferenceChanged`, marshals updates to the dispatcher, and unsubscribes on disposal.
- Pure registry-value interpretation is isolated for unit testing. Missing or malformed values default to light.
- `App` owns service lifetime. `SettingsViewModel` previews the effective scheme and persists `FollowSystemTheme`, `ColorSchemeLight`, and `ColorSchemeDark` through the existing settings service.
- `AppColorSchemeCatalog` remains the only component that mutates theme resources.

## Compatibility and Error Handling

Registry read failures retain the last known theme or default to light. Event-handler exceptions do not terminate the app. No static event subscription survives application exit.

## Verification

- Unit tests cover registry-value interpretation and effective-scheme selection.
- View-model tests cover preview, save, cancel, and manual-theme restoration.
- Structural tests require the new Appearance controls and all four localization dictionaries.
- Manual smoke testing changes Windows app theme and verifies UniDesk updates without restart.

## Out of Scope

Following wallpaper accent color, scheduled themes, per-module themes, Windows App SDK, and new dependencies.
