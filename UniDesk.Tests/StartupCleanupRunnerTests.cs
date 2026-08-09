using UniDesk.HardwareRepair;

namespace UniDesk.Tests;

public class StartupCleanupRunnerTests
{
    [Theory]
    [InlineData("UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\" --minimized", true)]
    [InlineData("LumiDesk", "C:\\Program Files\\UniDesk\\LumiDesk.exe", true)]
    [InlineData("VsirDesk", "C:\\Temp\\VsirDesk.exe", false)]
    [InlineData("UniDesk", "C:\\Program Files\\UniDesk\\UniDesk.exe.evil", false)]
    [InlineData("UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"evil", false)]
    [InlineData("Other", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"", false)]
    public void IsOwnedStartupCommand_ShouldRequireExactExecutableInCurrentInstallDirectory(
        string entryName,
        string command,
        bool expected)
    {
        Assert.Equal(
            expected,
            StartupEntryOwnership.IsOwnedCommand(
                entryName,
                command,
                @"C:\Program Files\UniDesk"));
    }

    [Fact]
    public void Cleanup_ShouldDeleteOnlyStrictlyOwnedLoadedHiveValuesAndTasks()
    {
        var store = new RecordingStartupEntryStore(
            [
                new("S-1-5-21-1", "UniDesk", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\""),
                new("S-1-5-21-2", "UniDesk", "\"C:\\Other\\UniDesk.exe\""),
                new("S-1-5-21-3", "Unrelated", "\"C:\\Program Files\\UniDesk\\UniDesk.exe\"")
            ],
            [
                new("UniDesk", @"C:\Program Files\UniDesk\UniDesk.exe"),
                new("LumiDesk", @"C:\Other\LumiDesk.exe")
            ]);
        var runner = new StartupCleanupRunner(
            @"C:\Program Files\UniDesk",
            store,
            new HardwareRepairLogger(Path.Combine(
                Path.GetTempPath(),
                $"UniDesk_startup_cleanup_test_{Guid.NewGuid():N}.log")));

        var result = runner.Cleanup();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.Equal([("S-1-5-21-1", "UniDesk")], store.DeletedRunEntries);
        Assert.Equal(["UniDesk"], store.DeletedTasks);
    }

    [Fact]
    public void Cleanup_WithProtectedLegacyMarker_ShouldAlsoDeleteStrictlyOwnedLegacyEntries()
    {
        var applicationDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk_startup_migration_test_{Guid.NewGuid():N}",
            "UniDesk");
        Directory.CreateDirectory(applicationDirectory);
        var legacyDirectory = @"D:\Program Files\UniDesk";
        File.WriteAllText(
            Path.Combine(applicationDirectory, StartupCleanupRunner.LegacyMigrationMarkerName),
            legacyDirectory);
        var store = new RecordingStartupEntryStore(
            [
                new("S-1-5-21-1", "UniDesk", "\"D:\\Program Files\\UniDesk\\UniDesk.exe\""),
                new("S-1-5-21-2", "UniDesk", "\"D:\\Program Files\\Other\\UniDesk.exe\"")
            ],
            [new("VsirDesk", @"D:\Program Files\UniDesk\VsirDesk.exe")]);
        var runner = new StartupCleanupRunner(
            applicationDirectory,
            store,
            new HardwareRepairLogger(Path.Combine(
                Path.GetTempPath(),
                $"UniDesk_startup_cleanup_test_{Guid.NewGuid():N}.log")));

        var result = runner.Cleanup();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.Equal([("S-1-5-21-1", "UniDesk")], store.DeletedRunEntries);
        Assert.Equal(["VsirDesk"], store.DeletedTasks);
    }

    [Fact]
    public void CleanupImplementation_ShouldLimitRegistryEnumerationToLoadedUserHives()
    {
        var implementation = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "StartupCleanupRunner.cs"));
        var program = File.ReadAllText(Path.Combine(
            ProjectRoot,
            "UniDesk.HardwareRepair",
            "Program.cs"));

        Assert.Contains("Registry.Users.GetSubKeyNames()", implementation, StringComparison.Ordinal);
        Assert.Contains("RegistryValueOptions.DoNotExpandEnvironmentNames", implementation, StringComparison.Ordinal);
        Assert.Contains("StartupEntryOwnership.IsOwnedCommand", implementation, StringComparison.Ordinal);
        Assert.Contains("currently loaded HKEY_USERS hives", implementation, StringComparison.Ordinal);
        Assert.Contains("--cleanup-startup", program, StringComparison.Ordinal);
    }

    private static readonly string ProjectRoot = Path.GetFullPath(Path.Combine(
        AppContext.BaseDirectory,
        "..",
        "..",
        "..",
        ".."));

    private sealed class RecordingStartupEntryStore(
        IReadOnlyList<StartupRunEntry> runEntries,
        IReadOnlyList<StartupTaskEntry> taskEntries) : IStartupEntryStore
    {
        public List<(string HiveName, string ValueName)> DeletedRunEntries { get; } = [];
        public List<string> DeletedTasks { get; } = [];

        public IReadOnlyList<StartupRunEntry> GetLoadedUserRunEntries() => runEntries;

        public IReadOnlyList<StartupTaskEntry> GetCandidateTasks() => taskEntries;

        public void DeleteRunEntry(StartupRunEntry entry) =>
            DeletedRunEntries.Add((entry.HiveName, entry.ValueName));

        public void DeleteTask(StartupTaskEntry entry) => DeletedTasks.Add(entry.Name);
    }
}
