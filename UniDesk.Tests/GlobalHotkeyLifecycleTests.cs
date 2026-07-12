namespace UniDesk.Tests;

public class GlobalHotkeyLifecycleTests
{
    private static readonly string ProjectRoot =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));

    [Fact]
    public void StartupAndSettings_ShouldUseMainWindowViewModelCoordinator()
    {
        var appCode = ReadProjectFile("UniDesk", "App.xaml.cs");
        var viewModelCode = ReadProjectFile("UniDesk", "ViewModels", "MainWindowViewModel.cs");

        Assert.Contains(
            "public HotkeyRegistrationResult ApplyGlobalHotkey(string? hotkey)",
            viewModelCode,
            StringComparison.Ordinal);
        Assert.Contains("_hotkeyService.ReplaceHotkey", viewModelCode, StringComparison.Ordinal);
        Assert.Contains("ApplyGlobalHotkey(hotkey)", appCode, StringComparison.Ordinal);
        Assert.DoesNotContain("private void RegisterGlobalHotkey", appCode, StringComparison.Ordinal);
    }

    private static string ReadProjectFile(params string[] segments) =>
        File.ReadAllText(Path.Combine([ProjectRoot, .. segments]));
}
