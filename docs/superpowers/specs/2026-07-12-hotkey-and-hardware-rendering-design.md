# UniDesk Configurable Hotkey and Hardware Rendering Design

**Date:** 2026-07-12
**Status:** Approved for implementation
**Scope:** Add an optional configurable global hotkey and remove hardware-monitor text ghosting. Preserve the completed Glass ComboBox fix. No database schema change.

## Goals

- Let users enable, change, reset, or completely disable the global show/hide hotkey from Settings.
- Reject invalid or occupied hotkeys without losing the currently working registration.
- Stop the rapidly changing network-rate text from leaving stale glyphs on the layered glass window.
- Keep the existing v2.0.0 data format, module behavior, and visual hierarchy.

## Settings Experience

The existing **Settings → Shortcuts** page receives a second section below the shortcut-count controls:

- An `Enable global hotkey` checkbox.
- A read-only capture field showing the normalized shortcut, for example `Ctrl+Alt+Space`.
- A `Record` action that focuses the capture field and waits for one keyboard gesture.
- A `Restore default` action that restores `Ctrl+Alt+Space` in the pending settings state.
- A short status line for recording instructions, invalid formats, and registration conflicts.

When the feature is disabled, the capture and reset controls are disabled and the persisted `Hotkey` setting is an empty string. UniDesk skips registration at startup and does not display a registration warning.

The recorder accepts one or more modifiers (`Ctrl`, `Alt`, `Shift`, or `Win`) plus one primary key from `A-Z`, `0-9`, `F1-F12`, or `Space`. A modifier is mandatory so ordinary typing cannot be captured globally. `Esc` cancels recording. `Backspace` or `Delete` clears the pending gesture without changing the enable checkbox.

All new labels and messages are localized in Simplified Chinese, English, Japanese, and Spanish.

## Hotkey Model and Registration

The existing `Settings.Hotkey` value remains the source of truth; an empty value means disabled. No table, column, or database-version change is required.

A small hotkey-gesture parser owns normalization and validation. It converts user input into a canonical string and the Win32 modifier/key values used by `RegisterHotKey`. Parsing is separated from registration so it can be unit-tested without Windows hooks.

The low-level hotkey service continues to own the window hook and native registration. Its registration result becomes explicit: success, invalid gesture, or native failure with the Win32 error code. Native calls are wrapped behind a narrow platform adapter so replacement and rollback behavior can be tested deterministically.

`MainWindowViewModel` remains the single coordinator for the active show/hide hotkey because it already owns both `IHotkeyService` and `IWindowService`. Startup and Settings both call the same coordinator method instead of maintaining separate callback implementations.

## Save and Rollback Flow

Hotkey changes are applied only when the user presses Save:

1. Normalize and validate the pending gesture, or accept the disabled state.
2. If unchanged, continue with the normal Settings save.
3. Unregister the old active gesture.
4. If disabled, keep no registration and continue.
5. Otherwise register the candidate gesture.
6. If registration fails, restore the old registration, keep Settings open, do not persist the candidate, and show the localized conflict/error message.
7. If registration succeeds, persist the canonical gesture with the other settings.
8. If later settings persistence fails, restore the previous hotkey registration during the existing save rollback.

Error code `1409` is presented as “already in use” rather than as a generic numeric failure. Other native errors retain their code for diagnosis. Startup reports a failed enabled hotkey once; disabled mode stays silent.

## Hardware-Monitor Rendering Fix

The metrics pipeline remains unchanged: sampling stays serial and continues at the existing interval. The defect is in rendering, not data collection.

The receive and send values currently auto-size inside centered horizontal stacks. Their width changes on nearly every sample, which moves the text on a translucent layered window and can leave stale ClearType glyphs at the previous position.

The network row will therefore:

- Give each changing value a fixed-width text slot so its layout position does not move between samples.
- Align the value consistently within that slot.
- Use grayscale text rendering for the rapidly changing network row to avoid ClearType color/alpha artifacts on a translucent surface.
- Keep the existing labels, sampling interval, formatting, and card dimensions.

The change is local to `HardwareMonitorModuleView`; no global font-rendering setting is changed.

## Verification

- Parser tests cover normalization, supported keys, missing modifiers, modifier-only input, cancellation, and clearing.
- Hotkey-service tests cover successful replacement, disabled mode, conflict rollback, and restoration after persistence failure.
- Settings tests cover loading, saving, resetting, disabling, and conflict behavior.
- Structural WPF tests verify the new Shortcuts-page controls and localized resources.
- Hardware rendering regression tests verify fixed-width network value slots and grayscale rendering.
- A visual smoke test observes at least five consecutive network updates without stale text.
- The already-added Glass ComboBox regression test remains green, and a visual smoke check confirms all eight theme choices remain readable.
- Full Release build and test suites must pass with zero warnings and errors before the v2.0.0 installer is rebuilt.

## Out of Scope

- Multiple simultaneous global hotkeys.
- Application-command palettes or per-module hotkeys.
- Automatic selection of a replacement hotkey after a conflict.
- Changes to system-metrics sampling or sensor selection.
- Database schema or backup-format changes.
