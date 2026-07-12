namespace UniDesk.Tests;

public class SettingsHotkeyIntegrationTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void SettingsViewModel_ShouldLoadApplyPersistAndRollbackGlobalHotkey()
    {
        var viewModel = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");

        Assert.Contains("public const string DefaultHotkey = \"Ctrl+Alt+Space\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("private bool _globalHotkeyEnabled", viewModel, StringComparison.Ordinal);
        Assert.Contains("private string _hotkey = DefaultHotkey", viewModel, StringComparison.Ordinal);
        Assert.Contains("GetValue(\"Hotkey\", DefaultHotkey)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ApplyGlobalHotkey(requestedHotkey)", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (!hotkeyResult.Success)", viewModel, StringComparison.Ordinal);
        Assert.Contains("_settingsService.SetValue(\"Hotkey\", hotkeyResult.NormalizedHotkey)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ApplyGlobalHotkey(originalHotkey)", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void ShortcutsPage_ShouldCaptureClearCancelAndRestoreHotkey()
    {
        var xaml = ReadProjectFile("UniDesk", "Controls", "Settings", "ShortcutsSettingsPage.xaml");
        var code = ReadProjectFile("UniDesk", "Controls", "Settings", "ShortcutsSettingsPage.xaml.cs");

        Assert.Contains("IsChecked=\"{Binding GlobalHotkeyEnabled}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("x:Name=\"HotkeyCaptureBox\"", xaml, StringComparison.Ordinal);
        Assert.Contains("PreviewKeyDown=\"HotkeyCaptureBox_OnPreviewKeyDown\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding RestoreDefaultHotkeyCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("HotkeyGestureParser.TryCreate", code, StringComparison.Ordinal);
        Assert.Contains("Key.Escape", code, StringComparison.Ordinal);
        Assert.Contains("Key.Back", code, StringComparison.Ordinal);
        Assert.Contains("Key.Delete", code, StringComparison.Ordinal);
    }

    [Fact]
    public void GlobalHotkeySettings_ShouldBeLocalizedInEveryLanguage()
    {
        var keys = new[]
        {
            "Settings.GlobalHotkey",
            "Settings.EnableGlobalHotkey",
            "Settings.HotkeyValue",
            "Settings.RecordHotkey",
            "Settings.RestoreDefaultHotkey",
            "Settings.HotkeyRecording",
            "Settings.HotkeyRecordingHint",
            "Hotkey.AlreadyInUse",
            "Hotkey.Disabled",
            "Hotkey.InvalidCapture"
        };

        foreach (var languageFile in new[]
                 {
                     "Strings.zh-CN.xaml",
                     "Strings.en-US.xaml",
                     "Strings.ja-JP.xaml",
                     "Strings.es-ES.xaml"
                 })
        {
            var resources = ReadProjectFile("UniDesk", "Resources", languageFile);
            foreach (var key in keys)
            {
                Assert.Contains($"x:Key=\"{key}\"", resources, StringComparison.Ordinal);
            }
        }
    }

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));
}
