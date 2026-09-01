using System.Xml.Linq;
using Microsoft.Win32;
using System.Security;
using System.Xml;

namespace UniDesk.HardwareRepair;

internal sealed record StartupRunEntry(string HiveName, string ValueName, string Command);

internal sealed record StartupTaskEntry(string Name, string Command);

internal interface IStartupEntryStore
{
    IReadOnlyList<StartupRunEntry> GetLoadedUserRunEntries();

    IReadOnlyList<StartupTaskEntry> GetCandidateTasks();

    void DeleteRunEntry(StartupRunEntry entry);

    void DeleteTask(StartupTaskEntry entry);
}

internal static class StartupEntryOwnership
{
    internal static bool IsOwnedCommand(string entryName, string command, string applicationDirectory)
    {
        var normalizedEntryName = entryName.TrimStart('\\');
        var expectedFileName = normalizedEntryName switch
        {
            "UniDesk" => "UniDesk.exe",
            "LumiDesk" => "LumiDesk.exe",
            "VsirDesk" => "VsirDesk.exe",
            _ => null
        };
        if (expectedFileName == null ||
            !WindowsCommandPathParser.TryGetExecutablePath(command, out var executablePath))
        {
            return false;
        }

        try
        {
            var fullExecutablePath = Path.GetFullPath(
                Environment.ExpandEnvironmentVariables(executablePath));
            return Path.GetFileName(fullExecutablePath).Equals(
                       expectedFileName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Path.GetDirectoryName(fullExecutablePath),
                       Path.GetFullPath(applicationDirectory),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }
}

internal sealed class StartupCleanupRunner
{
    internal const string CurrentApplicationMarkerName = ".unidesk-application-path";
    internal const string LegacyMigrationMarkerName = ".unidesk-legacy-startup-path";
    private readonly string _applicationDirectory;
    private readonly string _markerDirectory;
    private readonly IStartupEntryStore _entryStore;
    private readonly HardwareRepairLogger _logger;

    internal StartupCleanupRunner()
        : this(
            ResolveApplicationDirectoryFromMarker(ResolveComponentDirectory()),
            ResolveComponentDirectory(),
            new WindowsStartupEntryStore(new SystemProcessRunner()),
            new HardwareRepairLogger())
    {
    }

    internal StartupCleanupRunner(
        string applicationDirectory,
        IStartupEntryStore entryStore,
        HardwareRepairLogger logger)
        : this(applicationDirectory, applicationDirectory, entryStore, logger)
    {
    }

    internal StartupCleanupRunner(
        string applicationDirectory,
        string markerDirectory,
        IStartupEntryStore entryStore,
        HardwareRepairLogger logger)
    {
        _applicationDirectory = applicationDirectory;
        _markerDirectory = markerDirectory;
        _entryStore = entryStore;
        _logger = logger;
    }

    internal HardwareRepairExitCode Cleanup()
    {
        _logger.Log(
            "Startup cleanup is limited to currently loaded HKEY_USERS hives and strictly owned scheduled tasks.");
        try
        {
            if (!TryGetOwnedApplicationDirectories(out var ownedApplicationDirectories))
            {
                return HardwareRepairExitCode.StartupCleanupFailed;
            }

            foreach (var entry in _entryStore.GetLoadedUserRunEntries())
            {
                if (ownedApplicationDirectories.Any(directory =>
                        StartupEntryOwnership.IsOwnedCommand(
                            entry.ValueName,
                            entry.Command,
                            directory)))
                {
                    _entryStore.DeleteRunEntry(entry);
                }
            }

            foreach (var entry in _entryStore.GetCandidateTasks())
            {
                if (ownedApplicationDirectories.Any(directory =>
                        StartupEntryOwnership.IsOwnedCommand(
                            entry.Name,
                            entry.Command,
                            directory)))
                {
                    _entryStore.DeleteTask(entry);
                }
            }

            return HardwareRepairExitCode.Success;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or
                                   SecurityException or XmlException)
        {
            _logger.Log($"Startup cleanup failed: {ex.GetType().Name} (0x{ex.HResult:X8}).");
            return HardwareRepairExitCode.StartupCleanupFailed;
        }
    }

    private bool TryGetOwnedApplicationDirectories(out IReadOnlyList<string> directories)
    {
        var result = new List<string> { Path.GetFullPath(_applicationDirectory) };
        var markerPath = Path.Combine(_markerDirectory, LegacyMigrationMarkerName);
        if (!File.Exists(markerPath))
        {
            directories = result;
            return true;
        }

        var legacyPath = File.ReadAllText(markerPath).Trim();
        if (!TryNormalizeLegacyDirectory(legacyPath, result[0], out var normalizedLegacyPath))
        {
            if (string.Equals(
                    normalizedLegacyPath,
                    result[0],
                    StringComparison.OrdinalIgnoreCase))
            {
                directories = result;
                return true;
            }

            _logger.Log("Legacy startup migration marker contains an unsafe directory; cleanup stopped.");
            directories = [];
            return false;
        }

        result.Add(normalizedLegacyPath);
        _logger.Log("Startup cleanup includes the validated legacy installation directory marker.");
        directories = result;
        return true;
    }

    private static bool TryNormalizeLegacyDirectory(
        string path,
        string currentApplicationDirectory,
        out string normalizedPath)
    {
        normalizedPath = string.Empty;
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
        {
            return false;
        }

        var candidatePath = Path.GetFullPath(path).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        normalizedPath = candidatePath;
        if (string.Equals(candidatePath, currentApplicationDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var broadDirectories = new[]
        {
            Path.GetPathRoot(candidatePath),
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
        };
        return !broadDirectories
            .Where(directory => !string.IsNullOrWhiteSpace(directory))
            .Any(directory => string.Equals(
                candidatePath,
                Path.GetFullPath(directory!).TrimEnd(
                    Path.DirectorySeparatorChar,
                    Path.AltDirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase));
    }

    internal static string ResolveApplicationDirectoryFromMarker(string componentDirectory)
    {
        var markerPath = Path.Combine(componentDirectory, CurrentApplicationMarkerName);
        var applicationDirectory = File.ReadAllText(markerPath).Trim();
        if (string.IsNullOrWhiteSpace(applicationDirectory) ||
            !Path.IsPathFullyQualified(applicationDirectory))
        {
            throw new InvalidOperationException("The protected application path marker is invalid.");
        }

        return Path.GetFullPath(applicationDirectory).TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
    }

    private static string ResolveComponentDirectory()
    {
        var helperDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return Directory.GetParent(helperDirectory)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve the UniDesk protected component directory.");
    }
}

internal sealed class WindowsStartupEntryStore(IProcessRunner processRunner) : IStartupEntryStore
{
    private const string RunSubKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private static readonly string[] EntryNames = ["UniDesk", "LumiDesk", "VsirDesk"];
    private readonly string _schtasksPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "schtasks.exe");

    public IReadOnlyList<StartupRunEntry> GetLoadedUserRunEntries()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var entries = new List<StartupRunEntry>();
        foreach (var hiveName in Registry.Users.GetSubKeyNames()
                     .Where(name => name.StartsWith("S-1-", StringComparison.OrdinalIgnoreCase) &&
                                    !name.EndsWith("_Classes", StringComparison.OrdinalIgnoreCase)))
        {
            using var runKey = Registry.Users.OpenSubKey($@"{hiveName}\{RunSubKey}");
            if (runKey == null)
            {
                continue;
            }

            foreach (var valueName in EntryNames)
            {
                if (runKey.GetValue(
                        valueName,
                        null,
                        RegistryValueOptions.DoNotExpandEnvironmentNames) is string command)
                {
                    entries.Add(new(hiveName, valueName, command));
                }
            }
        }

        return entries;
    }

    public IReadOnlyList<StartupTaskEntry> GetCandidateTasks()
    {
        if (!OperatingSystem.IsWindows())
        {
            return [];
        }

        var entries = new List<StartupTaskEntry>();
        foreach (var name in EntryNames)
        {
            var taskName = $@"\{name}";
            var result = processRunner.Run(
                _schtasksPath,
                ["/Query", "/TN", taskName, "/XML"],
                TimeSpan.FromSeconds(30));
            if (result.ExitCode != 0)
            {
                continue;
            }

            var document = XDocument.Parse(result.StandardOutput);
            var command = document.Descendants()
                .FirstOrDefault(element => element.Name.LocalName == "Command")
                ?.Value;
            if (!string.IsNullOrWhiteSpace(command))
            {
                entries.Add(new(taskName, command));
            }
        }

        return entries;
    }

    public void DeleteRunEntry(StartupRunEntry entry)
    {
        using var runKey = Registry.Users.OpenSubKey($@"{entry.HiveName}\{RunSubKey}", writable: true)
            ?? throw new InvalidOperationException($"Loaded user Run key disappeared: {entry.HiveName}");
        runKey.DeleteValue(entry.ValueName, throwOnMissingValue: false);
    }

    public void DeleteTask(StartupTaskEntry entry)
    {
        var result = processRunner.Run(
            _schtasksPath,
            ["/Delete", "/TN", entry.Name, "/F"],
            TimeSpan.FromSeconds(30));
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"Scheduled task deletion failed for {entry.Name} with exit code {result.ExitCode}.");
        }
    }
}
