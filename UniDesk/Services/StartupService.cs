using System.IO;
using System.Diagnostics;
using Microsoft.Win32;

namespace UniDesk.Services;

public class StartupService : IStartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RegistryValueName = "UniDesk";

    private static readonly string[] RegistryValueNames = new[]
    {
        "UniDesk",
        "LumiDesk",
        "VsirDesk"
    };

    private static readonly string[] ScheduledTaskNames = new[]
    {
        @"\UniDesk",
        "UniDesk",
        @"\LumiDesk",
        "LumiDesk",
        @"\VsirDesk",
        "VsirDesk"
    };

    private readonly INotificationService _notificationService;
    private readonly ILocalizationService? _localizationService;

    public StartupService(
        INotificationService notificationService,
        ILocalizationService? localizationService = null)
    {
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public bool IsEnabled => HasCurrentStartupEntry() || HasLegacyStartupEntry();

    public bool Enable()
    {
        try
        {
            var exePath = GetExecutablePath();
            if (string.IsNullOrEmpty(exePath))
            {
                _notificationService.ShowErrorMessage(L("Settings.StartupPathFailed", "无法获取程序路径，无法设置开机自启。"));
                return false;
            }

            if (!SetRunKeyValue(exePath))
            {
                _notificationService.ShowErrorMessage(L("Settings.StartupWriteFailed", "无法写入开机自启设置，开机自启设置失败。"));
                return false;
            }

            DeleteLegacyStartupEntries();
            return true;
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(Format(
                "Settings.StartupEnableFailedFormat",
                $"设置开机自启失败：{ex.Message}",
                ex.Message));
            return false;
        }
    }

    public bool Disable()
    {
        try
        {
            var removed = false;
            foreach (var valueName in RegistryValueNames)
            {
                removed |= DeleteRunKeyValue(valueName);
            }

            foreach (var taskName in ScheduledTaskNames)
            {
                removed |= DeleteScheduledTask(taskName);
            }

            return removed || !IsEnabled;
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(Format(
                "Settings.StartupDisableFailedFormat",
                $"取消开机自启失败：{ex.Message}",
                ex.Message));
            return false;
        }
    }

    public void SyncWithSetting(bool shouldEnable)
    {
        if (shouldEnable)
        {
            if (!HasCurrentStartupEntry() || HasLegacyStartupEntry())
            {
                Enable();
            }
        }
        else if (IsEnabled)
        {
            Disable();
        }
    }

    private static bool HasCurrentStartupEntry()
    {
        return IsRegisteredInRunKey(RegistryValueName)
               || IsOwnedScheduledTask(@"\UniDesk")
               || IsOwnedScheduledTask("UniDesk");
    }

    private static bool HasLegacyStartupEntry()
    {
        return IsRegisteredInRunKey("LumiDesk")
               || IsRegisteredInRunKey("VsirDesk")
               || IsOwnedScheduledTask(@"\LumiDesk")
               || IsOwnedScheduledTask("LumiDesk")
               || IsOwnedScheduledTask(@"\VsirDesk")
               || IsOwnedScheduledTask("VsirDesk");
    }

    private static bool SetRunKeyValue(string exePath)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
            if (key == null)
            {
                return false;
            }

            ReadRunKeyValue(key, out var valueExists, out var existingValue);
            if (!CanWriteRunKeyValue(valueExists, existingValue, exePath))
            {
                return false;
            }

            ReadRunKeyValue(key, out var verifiedValueExists, out var verifiedValue);
            if (!RunKeyValueMatchesSnapshot(
                    valueExists,
                    existingValue,
                    verifiedValueExists,
                    verifiedValue) ||
                !CanWriteRunKeyValue(verifiedValueExists, verifiedValue, exePath))
            {
                return false;
            }

            key.SetValue(RegistryValueName, $"\"{exePath}\"", RegistryValueKind.String);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void DeleteLegacyStartupEntries()
    {
        DeleteRunKeyValue("LumiDesk");
        DeleteRunKeyValue("VsirDesk");

        foreach (var taskName in ScheduledTaskNames)
        {
            DeleteScheduledTask(taskName);
        }
    }

    private static bool IsRegisteredInRunKey(string valueName)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            var executablePath = GetExecutablePath();
            return key?.GetValue(valueName) is string value &&
                   !string.IsNullOrWhiteSpace(executablePath) &&
                   IsOwnedRunKeyValue(valueName, value, executablePath);
        }
        catch
        {
            return false;
        }
    }

    private static bool IsRegisteredInTaskScheduler(string taskName)
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        var result = RunSchtasks($"/Query /TN \"{taskName}\"");
        return result.ExitCode == 0;
    }

    private static bool DeleteScheduledTask(string taskName)
    {
        if (!OperatingSystem.IsWindows() || !IsOwnedScheduledTask(taskName))
        {
            return false;
        }

        var result = RunSchtasks($"/Delete /TN \"{taskName}\" /F");
        if (result.ExitCode == 0 || !IsRegisteredInTaskScheduler(taskName))
        {
            return true;
        }

        if (DeleteScheduledTaskWithPowerShell(taskName) || !IsRegisteredInTaskScheduler(taskName))
        {
            return true;
        }

        return DeleteScheduledTaskElevated(taskName) || !IsRegisteredInTaskScheduler(taskName);
    }

    private static bool IsOwnedScheduledTask(string taskName)
    {
        if (!OperatingSystem.IsWindows() || !IsRegisteredInTaskScheduler(taskName))
        {
            return false;
        }

        var executablePath = GetExecutablePath();
        var actionPath = GetScheduledTaskActionPath(taskName);
        return !string.IsNullOrWhiteSpace(executablePath) &&
               !string.IsNullOrWhiteSpace(actionPath) &&
               IsOwnedScheduledTaskAction(taskName, actionPath, executablePath);
    }

    internal static bool IsOwnedScheduledTaskAction(
        string taskName,
        string actionPath,
        string currentExecutablePath)
    {
        if (string.IsNullOrWhiteSpace(taskName) ||
            string.IsNullOrWhiteSpace(actionPath) ||
            string.IsNullOrWhiteSpace(currentExecutablePath))
        {
            return false;
        }

        var taskLeafName = GetScheduledTaskLeafName(taskName);
        return IsOwnedStartupExecutable(taskLeafName, actionPath, currentExecutablePath);
    }

    internal static bool IsOwnedRunKeyValue(
        string valueName,
        string command,
        string currentExecutablePath)
    {
        return TryGetStartupExecutablePath(command, out var executablePath) &&
               IsOwnedStartupExecutable(valueName, executablePath, currentExecutablePath);
    }

    internal static bool CanWriteRunKeyValue(
        bool valueExists,
        object? existingValue,
        string currentExecutablePath)
    {
        if (!valueExists)
        {
            return true;
        }

        return existingValue is string existingCommand &&
               !string.IsNullOrWhiteSpace(existingCommand) &&
               (IsOwnedRunKeyValue(
                    RegistryValueName,
                    existingCommand,
                    currentExecutablePath) ||
                CanReplaceMissingRunKeyValue(RegistryValueName, existingCommand));
    }

    internal static bool CanReplaceMissingRunKeyValue(string valueName, string command)
    {
        if (!TryGetStartupExecutablePath(command, out var executablePath) ||
            !string.Equals(
                Path.GetFileName(executablePath),
                GetExpectedStartupExecutableName(valueName),
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var root = Path.GetPathRoot(executablePath);
        if (string.IsNullOrWhiteSpace(root) ||
            root.StartsWith(@"\\", StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            var drive = new DriveInfo(root);
            if (!drive.IsReady || drive.DriveType != DriveType.Fixed ||
                (File.GetAttributes(root) & FileAttributes.Directory) == 0)
            {
                return false;
            }
        }
        catch
        {
            return false;
        }

        if (!HasSafeExistingAncestorChain(executablePath))
        {
            return false;
        }

        try
        {
            _ = File.GetAttributes(executablePath);
            return false;
        }
        catch (FileNotFoundException)
        {
            return true;
        }
        catch (DirectoryNotFoundException)
        {
            return true;
        }
        catch
        {
            return false;
        }
    }

    internal static bool RunKeyValueMatchesSnapshot(
        bool valueExists,
        object? existingValue,
        bool verifiedValueExists,
        object? verifiedValue) =>
        valueExists == verifiedValueExists &&
        (!valueExists || Equals(existingValue, verifiedValue));

    private static bool HasSafeExistingAncestorChain(string executablePath) =>
        HasSafeExistingAncestorChain(executablePath, File.GetAttributes);

    internal static bool HasSafeExistingAncestorChain(
        string executablePath,
        Func<string, FileAttributes> getAttributes)
    {
        try
        {
            var currentPath = Path.GetDirectoryName(executablePath);
            while (!string.IsNullOrWhiteSpace(currentPath))
            {
                try
                {
                    var attributes = getAttributes(currentPath);
                    if ((attributes & FileAttributes.Directory) == 0 ||
                        (attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        return false;
                    }
                }
                catch (FileNotFoundException)
                {
                }
                catch (DirectoryNotFoundException)
                {
                }
                catch
                {
                    return false;
                }

                currentPath = Path.GetDirectoryName(currentPath);
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void ReadRunKeyValue(
        RegistryKey key,
        out bool valueExists,
        out object? value)
    {
        valueExists = Array.Exists(
            key.GetValueNames(),
            name => string.Equals(name, RegistryValueName, StringComparison.OrdinalIgnoreCase));
        value = valueExists
            ? key.GetValue(
                RegistryValueName,
                defaultValue: null,
                RegistryValueOptions.DoNotExpandEnvironmentNames)
            : null;
    }

    private static bool TryGetStartupExecutablePath(string command, out string executablePath)
    {
        executablePath = string.Empty;
        if (string.IsNullOrWhiteSpace(command))
        {
            return false;
        }

        var trimmedCommand = command.Trim();
        string parsedExecutablePath;
        if (trimmedCommand.StartsWith('"'))
        {
            var closingQuote = trimmedCommand.IndexOf('"', 1);
            if (closingQuote <= 1)
            {
                return false;
            }
            if (closingQuote + 1 < trimmedCommand.Length &&
                !char.IsWhiteSpace(trimmedCommand[closingQuote + 1]))
            {
                return false;
            }
            parsedExecutablePath = trimmedCommand[1..closingQuote];
        }
        else
        {
            var executableEnd = trimmedCommand.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
            if (executableEnd < 0)
            {
                return false;
            }
            var executableBoundary = executableEnd + 4;
            if (executableBoundary < trimmedCommand.Length &&
                !char.IsWhiteSpace(trimmedCommand[executableBoundary]))
            {
                return false;
            }
            parsedExecutablePath = trimmedCommand[..executableBoundary];
        }

        try
        {
            var expandedPath = Environment.ExpandEnvironmentVariables(parsedExecutablePath.Trim());
            if (!Path.IsPathFullyQualified(expandedPath))
            {
                return false;
            }

            executablePath = Path.GetFullPath(expandedPath);
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsOwnedStartupExecutable(
        string entryName,
        string actionPath,
        string currentExecutablePath)
    {
        var expectedExecutableName = GetExpectedStartupExecutableName(entryName);
        if (expectedExecutableName == null)
        {
            return false;
        }

        try
        {
            var normalizedActionPath = Environment.ExpandEnvironmentVariables(
                actionPath.Trim().Trim('"'));
            if (!Path.IsPathFullyQualified(normalizedActionPath))
            {
                return false;
            }

            var actionFullPath = Path.GetFullPath(normalizedActionPath);
            var currentFullPath = Path.GetFullPath(currentExecutablePath);
            return Path.GetFileName(actionFullPath).Equals(
                       expectedExecutableName,
                       StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(
                       Path.GetDirectoryName(actionFullPath),
                       Path.GetDirectoryName(currentFullPath),
                       StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static string? GetExpectedStartupExecutableName(string entryName) => entryName switch
    {
        "UniDesk" => "UniDesk.exe",
        "LumiDesk" => "LumiDesk.exe",
        "VsirDesk" => "VsirDesk.exe",
        _ => null
    };

    private static string? GetScheduledTaskActionPath(string taskName)
    {
        var taskLeafName = EscapePowerShellSingleQuoted(GetScheduledTaskLeafName(taskName));
        var taskPath = EscapePowerShellSingleQuoted(GetScheduledTaskPath(taskName));
        var command =
            $"$task = Get-ScheduledTask -TaskPath '{taskPath}' -TaskName '{taskLeafName}' -ErrorAction Stop; " +
            "$actions = @($task.Actions); " +
            "if ($actions.Count -ne 1 -or [string]::IsNullOrWhiteSpace($actions[0].Execute)) { exit 3 }; " +
            "[Console]::Out.Write($actions[0].Execute)";
        var result = RunPowerShell(command);
        return result.ExitCode == 0 ? result.Output.Trim() : null;
    }

    private static bool DeleteRunKeyValue(string valueName)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(valueName) is not string value)
            {
                return false;
            }

            var executablePath = GetExecutablePath();
            if (string.IsNullOrWhiteSpace(executablePath) ||
                !IsOwnedRunKeyValue(valueName, value, executablePath))
            {
                return false;
            }

            key.DeleteValue(valueName, throwOnMissingValue: false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool DeleteScheduledTaskWithPowerShell(string taskName)
    {
        var taskLeafName = EscapePowerShellSingleQuoted(GetScheduledTaskLeafName(taskName));
        var taskPath = EscapePowerShellSingleQuoted(GetScheduledTaskPath(taskName));
        var command = $"Unregister-ScheduledTask -TaskPath '{taskPath}' -TaskName '{taskLeafName}' -Confirm:$false -ErrorAction Stop";
        return RunPowerShell(command).ExitCode == 0;
    }

    private static bool DeleteScheduledTaskElevated(string taskName)
    {
        try
        {
            var schtasksPath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
            if (!File.Exists(schtasksPath))
            {
                schtasksPath = "schtasks.exe";
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = schtasksPath,
                Arguments = $"/Delete /TN \"{taskName}\" /F",
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Hidden
            });

            if (process == null)
            {
                return false;
            }

            if (!process.WaitForExit(10000))
            {
                return false;
            }

            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    private static string GetScheduledTaskLeafName(string taskName)
    {
        var normalized = taskName.Replace('/', '\\').TrimEnd('\\');
        var separatorIndex = normalized.LastIndexOf('\\');
        return separatorIndex >= 0 ? normalized[(separatorIndex + 1)..] : normalized;
    }

    private static string GetScheduledTaskPath(string taskName)
    {
        var normalized = taskName.Replace('/', '\\');
        var separatorIndex = normalized.LastIndexOf('\\');
        if (separatorIndex <= 0)
        {
            return @"\";
        }

        return normalized[..(separatorIndex + 1)];
    }

    private static string EscapePowerShellSingleQuoted(string value)
    {
        return value.Replace("'", "''");
    }

    private static (int ExitCode, string Output) RunSchtasks(string arguments)
    {
        try
        {
            var schtasksPath = Path.Combine(Environment.SystemDirectory, "schtasks.exe");
            if (!File.Exists(schtasksPath))
            {
                schtasksPath = "schtasks.exe";
            }

            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = schtasksPath,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                return (-1, string.Empty);
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return (-1, output);
            }

            return (process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private static (int ExitCode, string Output) RunPowerShell(string command)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            });

            if (process == null)
            {
                return (-1, string.Empty);
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return (-1, output);
            }

            return (process.ExitCode, output);
        }
        catch (Exception ex)
        {
            return (-1, ex.Message);
        }
    }

    private static string? GetExecutablePath()
    {
        var path = Environment.ProcessPath;
        if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
        {
            return path;
        }

        path = System.Reflection.Assembly.GetExecutingAssembly().Location;
        if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            var exe = Path.ChangeExtension(path, ".exe");
            if (File.Exists(exe))
            {
                return exe;
            }
        }

        return File.Exists(path) ? path : null;
    }

    private string L(string key, string fallback) =>
        _localizationService?.GetString(key) ?? fallback;

    private string Format(string key, string fallback, params object?[] args) =>
        _localizationService?.Format(key, args) ?? fallback;
}
