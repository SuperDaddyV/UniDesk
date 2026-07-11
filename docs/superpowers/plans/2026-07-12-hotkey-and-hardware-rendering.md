# Configurable Hotkey and Hardware Rendering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add an optional, conflict-safe configurable global hotkey and eliminate hardware network-text ghosting while retaining the completed Glass ComboBox fix.

**Architecture:** Keep the existing `Settings.Hotkey` value and split gesture parsing from Win32 registration. `HotkeyService` owns one active registration and performs replace-with-rollback through a testable platform adapter; `MainWindowViewModel` coordinates the show/hide callback for both startup and Settings. The rendering fix stays local to the hardware module by stabilizing dynamic text layout and selecting grayscale rendering.

**Tech Stack:** .NET 9, WPF XAML, CommunityToolkit.Mvvm, Win32 `RegisterHotKey`, xUnit, Inno Setup 6.

## Global Constraints

- An empty `Hotkey` setting means the global hotkey is disabled.
- No database schema, database version, backup format, or NuGet dependency changes.
- Supported gestures require at least one of `Ctrl`, `Alt`, `Shift`, or `Win` plus `A-Z`, `0-9`, `F1-F12`, or `Space`.
- `Esc` cancels capture; `Backspace` and `Delete` clear the pending gesture.
- Registration failure must keep Settings open, must not persist the candidate, and must restore the previous active hotkey.
- The hardware fix must not change sampling interval, sensor selection, or value formatting.
- Preserve the current uncommitted Glass ComboBox changes in `UniDesk/Resources/Themes/Shared.xaml`, `UniDesk.Tests/WpfInteractionRegressionTests.cs`, and `docs/release-unidesk.md`.
- Do not publish to GitHub; rebuild `C:\Users\Administrator\Documents\UniDesk\installer\UniDesk_Setup_2.0.0.exe` only after full verification.

---

### Task 1: Hotkey gesture model and parser

**Files:**
- Create: `UniDesk/Models/HotkeyGesture.cs`
- Create: `UniDesk/Helpers/HotkeyGestureParser.cs`
- Create: `UniDesk.Tests/HotkeyGestureParserTests.cs`

**Interfaces:**
- Produces: `HotkeyGesture(string DisplayText, uint Modifiers, uint VirtualKey)`.
- Produces: `HotkeyGestureParser.TryParse(string?, out HotkeyGesture)` and `HotkeyGestureParser.TryCreate(Key, ModifierKeys, out HotkeyGesture)`.
- Consumes: WPF `Key` and `ModifierKeys` only for capture conversion.

- [ ] **Step 1: Write parser tests first**

```csharp
[Theory]
[InlineData("ctrl+alt+space", "Ctrl+Alt+Space", 0x0003u, 0x20u)]
[InlineData("Win+Shift+F12", "Shift+Win+F12", 0x000Cu, 0x7Bu)]
public void TryParse_ValidGesture_Normalizes(string input, string display, uint modifiers, uint key)
{
    Assert.True(HotkeyGestureParser.TryParse(input, out var gesture));
    Assert.Equal(new HotkeyGesture(display, modifiers, key), gesture);
}

[Theory]
[InlineData("")]
[InlineData("Space")]
[InlineData("Ctrl+Alt")]
[InlineData("Ctrl+Mouse1")]
public void TryParse_InvalidGesture_ReturnsFalse(string input) =>
    Assert.False(HotkeyGestureParser.TryParse(input, out _));
```

- [ ] **Step 2: Run the focused tests and verify RED**

Run:

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~HotkeyGestureParserTests"
```

Expected: compilation fails because `HotkeyGesture` and `HotkeyGestureParser` do not exist.

- [ ] **Step 3: Implement canonical parsing and WPF capture conversion**

Use modifier order `Ctrl`, `Alt`, `Shift`, `Win`. Reject missing modifiers, multiple primary keys, and unsupported keys. For `TryCreate`, translate `Key.System` in the page before calling the parser, then build the canonical text from the active modifiers and primary key.

```csharp
public readonly record struct HotkeyGesture(string DisplayText, uint Modifiers, uint VirtualKey);

public static bool TryParse(string? value, out HotkeyGesture gesture);
public static bool TryCreate(Key key, ModifierKeys modifiers, out HotkeyGesture gesture);
```

- [ ] **Step 4: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all `HotkeyGestureParserTests` pass.

- [ ] **Step 5: Commit the parser**

```powershell
git add UniDesk/Models/HotkeyGesture.cs UniDesk/Helpers/HotkeyGestureParser.cs UniDesk.Tests/HotkeyGestureParserTests.cs
git commit -m "feat: add global hotkey gesture parser"
```

### Task 2: Testable hotkey replacement with rollback

**Files:**
- Create: `UniDesk/Models/HotkeyRegistrationResult.cs`
- Create: `UniDesk/Services/IHotkeyPlatform.cs`
- Create: `UniDesk/Services/Win32HotkeyPlatform.cs`
- Modify: `UniDesk/Services/IHotkeyService.cs`
- Modify: `UniDesk/Services/HotkeyService.cs`
- Modify: `UniDesk/App.xaml.cs`
- Create: `UniDesk.Tests/HotkeyServiceTests.cs`

**Interfaces:**
- Consumes: `HotkeyGestureParser.TryParse` from Task 1.
- Produces: `IHotkeyService.ReplaceHotkey(string?, Action): HotkeyRegistrationResult`.
- Produces: `IHotkeyService.ActiveHotkey: string`.
- Produces: `IHotkeyPlatform.Register(..., out int errorCode)` and `Unregister(...)`.

- [ ] **Step 1: Write replacement and rollback tests first**

```csharp
[Fact]
public void ReplaceHotkey_Conflict_RestoresPreviousRegistration()
{
    var platform = new FakeHotkeyPlatform();
    var service = CreateInitializedService(platform);
    Assert.True(service.ReplaceHotkey("Ctrl+Alt+Space", () => { }).Success);
    platform.FailNextRegistrationWith(1409);

    var result = service.ReplaceHotkey("Ctrl+Shift+K", () => { });

    Assert.False(result.Success);
    Assert.Equal(1409, result.ErrorCode);
    Assert.True(result.PreviousHotkeyRestored);
    Assert.Equal("Ctrl+Alt+Space", service.ActiveHotkey);
}

[Fact]
public void ReplaceHotkey_Empty_DisablesRegistration()
{
    var service = CreateInitializedService(new FakeHotkeyPlatform());
    service.ReplaceHotkey("Ctrl+Alt+Space", () => { });

    var result = service.ReplaceHotkey(string.Empty, () => { });

    Assert.True(result.Success);
    Assert.Equal(string.Empty, service.ActiveHotkey);
}
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~HotkeyServiceTests"
```

Expected: compilation fails because the new result and platform APIs do not exist.

- [ ] **Step 3: Implement the native adapter and typed result**

```csharp
public enum HotkeyRegistrationFailure { None, InvalidGesture, NativeFailure }

public readonly record struct HotkeyRegistrationResult(
    bool Success,
    string NormalizedHotkey,
    HotkeyRegistrationFailure Failure,
    int ErrorCode,
    bool PreviousHotkeyRestored);

public interface IHotkeyPlatform
{
    bool Register(IntPtr windowHandle, int id, uint modifiers, uint virtualKey, out int errorCode);
    bool Unregister(IntPtr windowHandle, int id);
}
```

Move only the P/Invoke calls into `Win32HotkeyPlatform`; keep `WM_HOTKEY` hook handling in `HotkeyService`.

- [ ] **Step 4: Replace the multi-entry registration path with one active registration**

`ReplaceHotkey` must normalize first, return a no-op success for the already-active value, unregister the previous ID, register the candidate, and re-register the previous gesture/callback if the candidate fails. Disabled input unregisters and succeeds without a native registration.

- [ ] **Step 5: Register the platform adapter in dependency injection**

```csharp
services.AddSingleton<IHotkeyPlatform, Win32HotkeyPlatform>();
services.AddSingleton<IHotkeyService, HotkeyService>();
```

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all hotkey-service tests pass, including rollback and disabled mode.

- [ ] **Step 7: Commit the registration layer**

```powershell
git add UniDesk/Models/HotkeyRegistrationResult.cs UniDesk/Services/IHotkeyPlatform.cs UniDesk/Services/Win32HotkeyPlatform.cs UniDesk/Services/IHotkeyService.cs UniDesk/Services/HotkeyService.cs UniDesk/App.xaml.cs UniDesk.Tests/HotkeyServiceTests.cs
git commit -m "refactor: make global hotkey replacement recoverable"
```

### Task 3: One hotkey coordinator for startup and Settings

**Files:**
- Modify: `UniDesk/ViewModels/MainWindowViewModel.cs`
- Modify: `UniDesk/App.xaml.cs`
- Modify: `UniDesk.Tests/MainWindowViewModelTests.cs`

**Interfaces:**
- Consumes: `IHotkeyService.ReplaceHotkey` from Task 2.
- Produces: `MainWindowViewModel.ApplyGlobalHotkey(string?): HotkeyRegistrationResult`.

- [ ] **Step 1: Write coordinator tests first**

Add a fake `IHotkeyService` and assert that `ApplyGlobalHotkey("")` disables registration and that a candidate is registered with a callback that toggles `IWindowService`.

```csharp
var result = viewModel.ApplyGlobalHotkey("Ctrl+Shift+K");
Assert.True(result.Success);
fakeHotkeyService.InvokeCallback();
Assert.Equal(1, fakeWindowService.ToggleCount);
```

- [ ] **Step 2: Run focused tests and verify RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~MainWindowViewModelTests"
```

Expected: compilation fails because `ApplyGlobalHotkey` does not exist and the fake interface is stale.

- [ ] **Step 3: Implement the coordinator method**

```csharp
public HotkeyRegistrationResult ApplyGlobalHotkey(string? hotkey) =>
    _hotkeyService.ReplaceHotkey(
        hotkey,
        () => Application.Current.Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            () => _windowService.ToggleWindow()));
```

- [ ] **Step 4: Route startup through the coordinator**

After `IHotkeyService.Initialize(_mainWindow)`, call `MainWindowViewModel.ApplyGlobalHotkey` with the stored value. Remove `App.RegisterGlobalHotkey`. If the stored value is empty, do nothing further. If startup registration fails, show one localized warning; use the occupied message for error `1409`.

- [ ] **Step 5: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all main-window view-model tests pass.

- [ ] **Step 6: Commit the coordinator**

```powershell
git add UniDesk/ViewModels/MainWindowViewModel.cs UniDesk/App.xaml.cs UniDesk.Tests/MainWindowViewModelTests.cs
git commit -m "feat: coordinate global hotkey lifecycle"
```

### Task 4: Settings capture UI, persistence, and localization

**Files:**
- Modify: `UniDesk/ViewModels/SettingsViewModel.cs`
- Modify: `UniDesk/Controls/Settings/ShortcutsSettingsPage.xaml`
- Modify: `UniDesk/Controls/Settings/ShortcutsSettingsPage.xaml.cs`
- Modify: `UniDesk/Resources/Strings.zh-CN.xaml`
- Modify: `UniDesk/Resources/Strings.en-US.xaml`
- Modify: `UniDesk/Resources/Strings.ja-JP.xaml`
- Modify: `UniDesk/Resources/Strings.es-ES.xaml`
- Modify: `UniDesk.Tests/SettingsViewModelTests.cs`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`

**Interfaces:**
- Consumes: `MainWindowViewModel.ApplyGlobalHotkey` from Task 3.
- Consumes: `HotkeyGestureParser.TryCreate` from Task 1.
- Produces Settings properties: `GlobalHotkeyEnabled`, `Hotkey`, and `HotkeyStatusText`.
- Produces command: `RestoreDefaultHotkeyCommand`.

- [ ] **Step 1: Write Settings behavior tests first**

Cover load, disabled load, reset, successful save, conflict, and persistence-failure rollback.

```csharp
[Fact]
public async Task Save_HotkeyConflict_DoesNotPersistAndKeepsWindowOpen()
{
    var fixture = CreateFixture(hotkeyResult: Conflict(1409));
    fixture.ViewModel.GlobalHotkeyEnabled = true;
    fixture.ViewModel.Hotkey = "Ctrl+Shift+K";

    await fixture.ViewModel.SaveCommand.ExecuteAsync(null);

    Assert.False(fixture.ViewModel.LastSaveSucceeded);
    Assert.Null(fixture.RequestedCloseValue);
    Assert.Equal("Ctrl+Alt+Space", fixture.Settings.GetValue("Hotkey", ""));
}
```

- [ ] **Step 2: Write structural XAML/localization tests first**

Assert the Shortcuts page contains `GlobalHotkeyEnabled`, `HotkeyCaptureBox`, recording handlers, and restore command. Assert all four language files contain `Settings.EnableGlobalHotkey`, `Settings.RecordHotkey`, `Settings.RestoreDefaultHotkey`, `Settings.HotkeyRecording`, `Hotkey.AlreadyInUse`, and `Hotkey.Disabled`.

- [ ] **Step 3: Run focused tests and verify RED**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~SettingsViewModelTests|FullyQualifiedName~WpfInteractionRegressionTests"
```

Expected: new Settings properties, controls, and localization keys are missing.

- [ ] **Step 4: Implement pending state and save rollback**

Load an empty stored hotkey as `GlobalHotkeyEnabled=false` and `Hotkey=DefaultHotkey`. Save the enabled candidate only after `ApplyGlobalHotkey` succeeds. On conflict, set the status and return without closing. In the existing catch path, reapply the original hotkey before reverting other settings.

- [ ] **Step 5: Implement the recorder UI**

Add a section with the enable checkbox, read-only `HotkeyCaptureBox`, `Record` and `Restore default` buttons, and a bound status line. In code-behind, recording uses `PreviewKeyDown`: `Esc` cancels, `Backspace/Delete` clears, and other keys call `HotkeyGestureParser.TryCreate` with `e.SystemKey` when `e.Key == Key.System`.

- [ ] **Step 6: Add all four language resources**

Use concise localized strings and keep the established key names identical across dictionaries. Error `1409` must say the gesture is already in use and ask the user to choose another.

- [ ] **Step 7: Run focused tests and verify GREEN**

Run the command from Step 3. Expected: all Settings and WPF regression tests pass.

- [ ] **Step 8: Commit Settings support**

```powershell
git add UniDesk/ViewModels/SettingsViewModel.cs UniDesk/Controls/Settings/ShortcutsSettingsPage.xaml UniDesk/Controls/Settings/ShortcutsSettingsPage.xaml.cs UniDesk/Resources/Strings.*.xaml UniDesk.Tests/SettingsViewModelTests.cs UniDesk.Tests/WpfInteractionRegressionTests.cs
git commit -m "feat: add configurable global hotkey settings"
```

### Task 5: Hardware rendering and Glass ComboBox regression fixes

**Files:**
- Modify: `UniDesk/Controls/HardwareMonitorModuleView.xaml`
- Modify: `UniDesk/Resources/Themes/Shared.xaml`
- Modify: `UniDesk.Tests/WpfInteractionRegressionTests.cs`
- Modify: `docs/release-unidesk.md`

**Interfaces:**
- Consumes existing `SystemNetworkReceivedText` and `SystemNetworkSentText` bindings unchanged.
- Produces fixed-width named value slots `NetworkReceivedValueText` and `NetworkSentValueText`.

- [ ] **Step 1: Add the hardware rendering regression assertions**

```csharp
Assert.Contains("x:Name=\"NetworkReceivedValueText\"", hardwareXaml);
Assert.Contains("x:Name=\"NetworkSentValueText\"", hardwareXaml);
Assert.Contains("Width=\"78\"", hardwareXaml);
Assert.Contains("TextOptions.TextRenderingMode=\"Grayscale\"", hardwareXaml);
```

Retain the existing `GlassComboBox_ShouldUseReadableSelectablePopupTemplate` regression test.

- [ ] **Step 2: Run the focused WPF test and verify RED for hardware rendering**

```powershell
dotnet test UniDesk.Tests/UniDesk.Tests.csproj -c Release --no-restore --filter "FullyQualifiedName~WpfInteractionRegressionTests"
```

Expected: hardware-specific assertions fail; the Glass ComboBox assertion already passes.

- [ ] **Step 3: Stabilize the network row layout**

Set `TextOptions.TextRenderingMode="Grayscale"` on the network-row border. Give both dynamic value `TextBlock`s `Width="78"`, `TextAlignment="Left"`, and stable margins while preserving their bindings and formatting.

- [ ] **Step 4: Keep and review the Glass ComboBox template**

Verify the popup uses `PrimaryBackgroundBrush`, each item uses `PrimaryTextBrush`, `PART_Popup` binds `IsDropDownOpen`, and no `StaysOpen="False"` remains. Do not change theme selection logic.

- [ ] **Step 5: Update release notes**

Document the readable/selectable follow-system dropdowns, configurable/disableable hotkey with conflict rollback, and hardware ghosting fix in both English and Chinese v2.0.0 sections.

- [ ] **Step 6: Run focused tests and verify GREEN**

Run the command from Step 2. Expected: all WPF regression tests pass.

- [ ] **Step 7: Commit the UI regressions**

```powershell
git add UniDesk/Controls/HardwareMonitorModuleView.xaml UniDesk/Resources/Themes/Shared.xaml UniDesk.Tests/WpfInteractionRegressionTests.cs docs/release-unidesk.md
git commit -m "fix: stabilize glass control rendering"
```

### Task 6: Full verification, visual smoke test, and v2.0.0 package

**Files:**
- Verify: `UniDesk.sln`
- Verify: `UniDesk.iss`
- Output: `C:\Users\Administrator\Documents\UniDesk\installer\UniDesk_Setup_2.0.0.exe`

**Interfaces:**
- Consumes all previous task outputs.
- Produces the final self-contained unsigned v2.0.0 installer and SHA256 hash.

- [ ] **Step 1: Run a fresh Release build**

```powershell
dotnet build UniDesk.sln -c Release --no-restore
```

Expected: zero warnings and zero errors.

- [ ] **Step 2: Run the complete test suite**

```powershell
dotnet test UniDesk.sln -c Release --no-build
```

Expected: all tests pass with zero failures and zero skips.

- [ ] **Step 3: Run visual smoke checks**

Use an isolated diagnostic instance without committing the diagnostic mutex override. Confirm:

1. Follow-system dropdown shows all eight readable options and selection changes.
2. Enable/record/reset/disable hotkey controls work.
3. A known conflicting hotkey keeps Settings open and preserves the old active registration.
4. At least five consecutive two-second network updates leave no stale glyphs.

- [ ] **Step 4: Publish to a new self-contained directory**

```powershell
dotnet restore UniDesk/UniDesk.csproj -r win-x64 --ignore-failed-sources
dotnet publish UniDesk/UniDesk.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false --no-restore -o publish/win-x64-v2.0.0-hotkeyfix
```

- [ ] **Step 5: Compile and copy the installer**

Temporarily point `MyAppSourceDir` in `UniDesk.iss` to the new publish directory, compile with Inno Setup, restore the script, and copy the result to the exact destination path. Do not commit the temporary source-directory edit.

- [ ] **Step 6: Verify package integrity**

Check ProductVersion `2.0.0`, confirm no PDB files, compute SHA256, verify the source/destination hashes match, and run Windows Defender custom scan. Record that Authenticode status remains `NotSigned`.

- [ ] **Step 7: Verify repository cleanliness**

```powershell
git diff --check
git status --short
git log -8 --oneline
```

Expected: no uncommitted tracked changes remain and the implementation commits are present. Do not push or publish to GitHub.
