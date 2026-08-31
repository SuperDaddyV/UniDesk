using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

public class StartupServiceTests
{
    [Fact]
    public void SyncWithSetting_WhenDisabled_DoesNotThrow()
    {
        var service = new StartupService(new NoOpNotificationService());
        var exception = Record.Exception(() => service.SyncWithSetting(false));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(@"\UniDesk", @"C:\Program Files\UniDesk\UniDesk.exe", true)]
    [InlineData("LumiDesk", @"C:\Program Files\UniDesk\LumiDesk.exe", true)]
    [InlineData("VsirDesk", @"C:\Program Files\UniDesk\VsirDesk.exe", true)]
    [InlineData("LumiDesk", @"C:\Temp\LumiDesk.exe", false)]
    [InlineData("UniDesk", @"C:\Program Files\UniDesk\not-unidesk.exe", false)]
    [InlineData("UniDesk", @"C:\Program Files\UniDesk\UniDesk.exe.evil", false)]
    [InlineData("UniDesk", @"C:UniDesk.exe", false)]
    [InlineData("UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"evil", false)]
    [InlineData("UniDesk", "powershell.exe", false)]
    public void IsOwnedScheduledTaskAction_ShouldRequireSupportedExecutableInCurrentInstallDirectory(
        string taskName,
        string actionPath,
        bool expected)
    {
        var actual = StartupService.IsOwnedScheduledTaskAction(
            taskName,
            actionPath,
            @"C:\Program Files\UniDesk\UniDesk.exe");

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\" --minimized", true)]
    [InlineData("LumiDesk", "C:\\Program Files\\UniDesk\\LumiDesk.exe --legacy", true)]
    [InlineData("VsirDesk", "\"C:\\Temp\\VsirDesk.exe\"", false)]
    [InlineData("UniDesk", "powershell.exe -File C:\\Program Files\\UniDesk\\UniDesk.exe", false)]
    [InlineData("UniDesk", "C:\\Program Files\\UniDesk\\UniDesk.exe.evil", false)]
    [InlineData("UniDesk", "C:\\Program Files\\UniDesk\\UniDesk.exe/arg", false)]
    [InlineData("UniDesk", "C:UniDesk.exe", false)]
    [InlineData("UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"evil", false)]
    [InlineData("Other", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"", false)]
    public void IsOwnedRunKeyValue_ShouldRequireSupportedExecutableInCurrentInstallDirectory(
        string valueName,
        string command,
        bool expected)
    {
        var actual = StartupService.IsOwnedRunKeyValue(
            valueName,
            command,
            @"C:\Program Files\UniDesk\UniDesk.exe");

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CanReplaceMissingRunKeyValue_ShouldOnlyAcceptDefinitelyMissingOwnedExecutable()
    {
        var testDirectory = Path.Combine(
            Path.GetTempPath(),
            $"unidesk-startup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);
        var existingExecutable = Path.Combine(testDirectory, "UniDesk.exe");
        File.WriteAllText(existingExecutable, string.Empty);
        var missingExecutable = Path.Combine(testDirectory, "removed", "UniDesk.exe");

        try
        {
            Assert.True(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                $"\"{missingExecutable}\""));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                $"\"{existingExecutable}\""));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                $"\"{Path.Combine(testDirectory, "removed", "Other.exe")}\""));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                "powershell.exe -File UniDesk.exe"));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                "C:UniDesk.exe"));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "UniDesk",
                @"\\server\share\UniDesk.exe"));
            Assert.False(StartupService.CanReplaceMissingRunKeyValue(
                "Other",
                $"\"{missingExecutable}\""));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public void CanWriteRunKeyValue_ShouldRejectExistingBlankOrNonStringValues()
    {
        const string currentExecutable = @"C:\Program Files\UniDesk\UniDesk.exe";

        Assert.True(StartupService.CanWriteRunKeyValue(
            valueExists: false,
            existingValue: null,
            currentExecutable));
        Assert.True(StartupService.CanWriteRunKeyValue(
            valueExists: true,
            existingValue: $"\"{currentExecutable}\"",
            currentExecutable));
        Assert.False(StartupService.CanWriteRunKeyValue(
            valueExists: true,
            existingValue: null,
            currentExecutable));
        Assert.False(StartupService.CanWriteRunKeyValue(
            valueExists: true,
            existingValue: "   ",
            currentExecutable));
        Assert.False(StartupService.CanWriteRunKeyValue(
            valueExists: true,
            existingValue: new byte[] { 1, 2, 3 },
            currentExecutable));
    }

    [Fact]
    public void HasSafeExistingAncestorChain_ShouldRejectExistingReparseAncestor()
    {
        var root = Path.GetPathRoot(Path.GetFullPath(Path.GetTempPath()))!;
        var reparseAncestor = Path.Combine(root, "legacy-junction");
        var executable = Path.Combine(reparseAncestor, "removed", "UniDesk.exe");

        var actual = StartupService.HasSafeExistingAncestorChain(
            executable,
            path => string.Equals(path, reparseAncestor, StringComparison.OrdinalIgnoreCase)
                ? FileAttributes.Directory | FileAttributes.ReparsePoint
                : throw new DirectoryNotFoundException());

        Assert.False(actual);
    }

    [Fact]
    public void RunKeyValueMatchesSnapshot_ShouldRejectAnyObservedChange()
    {
        Assert.True(StartupService.RunKeyValueMatchesSnapshot(
            valueExists: false,
            existingValue: null,
            verifiedValueExists: false,
            verifiedValue: null));
        Assert.True(StartupService.RunKeyValueMatchesSnapshot(
            valueExists: true,
            existingValue: "same",
            verifiedValueExists: true,
            verifiedValue: "same"));
        Assert.False(StartupService.RunKeyValueMatchesSnapshot(
            valueExists: false,
            existingValue: null,
            verifiedValueExists: true,
            verifiedValue: "new"));
        Assert.False(StartupService.RunKeyValueMatchesSnapshot(
            valueExists: true,
            existingValue: "before",
            verifiedValueExists: true,
            verifiedValue: "after"));
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
    }
}
