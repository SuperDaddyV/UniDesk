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
        Assert.Contains("_settingsService.SetValue(\"Hotkey\", hotkeyToPersist)", viewModel, StringComparison.Ordinal);
        Assert.Contains("ApplyGlobalHotkey(originalHotkey)", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void UnchangedHotkey_ShouldNotBlockSavingUnrelatedSettings()
    {
        var viewModel = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");

        Assert.Contains("var hotkeySettingChanged", viewModel, StringComparison.Ordinal);
        Assert.Contains("if (hotkeySettingChanged)", viewModel, StringComparison.Ordinal);
        Assert.Contains("var hotkeyToPersist", viewModel, StringComparison.Ordinal);
        Assert.Contains("_settingsService.SetValue(\"Hotkey\", hotkeyToPersist)", viewModel, StringComparison.Ordinal);
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

    [Fact]
    public void GeneralSettings_ShouldExposeCityAndExplicitAutoLocationConsent()
    {
        var xaml = ReadProjectFile("UniDesk", "Controls", "Settings", "GeneralSettingsPage.xaml");
        var viewModel = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");

        Assert.Contains("Text=\"{Binding City, UpdateSourceTrigger=PropertyChanged}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Text=\"{DynamicResource Settings.CityHint}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("IsChecked=\"{Binding AutoLocation}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("Command=\"{Binding OpenLocationSettingsCommand}\"", xaml, StringComparison.Ordinal);
        Assert.Contains("private string _city = string.Empty", viewModel, StringComparison.Ordinal);
        Assert.Contains("private bool _autoLocation", viewModel, StringComparison.Ordinal);
        Assert.Contains("_settingsService.SetValue(\"AutoLocation\"", viewModel, StringComparison.Ordinal);
        Assert.Contains("ms-settings:privacy-location", viewModel, StringComparison.Ordinal);
        Assert.Contains("StartupEnabled = true;", viewModel, StringComparison.Ordinal);
        Assert.Contains("AutoLocation = true;", viewModel, StringComparison.Ordinal);
    }

    [Fact]
    public void WeatherCredentialChanges_ShouldValidateBeforePersistence()
    {
        var viewModel = ReadProjectFile("UniDesk", "ViewModels", "SettingsViewModel.cs");
        var validation = viewModel.IndexOf("ValidateApiKeyAsync(apiKeyToValidate, apiHostToValidate)", StringComparison.Ordinal);
        var persistence = viewModel.IndexOf("_settingsService.SetValue(\"WeatherApiKey\", apiKeyToValidate)", StringComparison.Ordinal);

        Assert.True(validation >= 0);
        Assert.True(persistence > validation);
    }

    [Fact]
    public void WeatherLocationSettings_ShouldBeLocalizedInEveryLanguage()
    {
        var keys = new[]
        {
            "Settings.WeatherLocation",
            "Settings.City",
            "Settings.CityHint",
            "Settings.AutoLocation",
            "Settings.AutoLocationPrivacyHint",
            "Settings.OpenLocationSettings",
            "Settings.OpenLocationSettingsFailed",
            "Settings.WeatherCredentialsRequired",
            "Settings.WeatherApiHostInvalid"
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
