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
               || IsRegisteredInTaskScheduler(@"\UniDesk")
               || IsRegisteredInTaskScheduler("UniDesk");
    }

    private static bool HasLegacyStartupEntry()
    {
        return IsRegisteredInRunKey("LumiDesk")
               || IsRegisteredInRunKey("VsirDesk")
               || IsRegisteredInTaskScheduler(@"\LumiDesk")
               || IsRegisteredInTaskScheduler("LumiDesk")
               || IsRegisteredInTaskScheduler(@"\VsirDesk")
               || IsRegisteredInTaskScheduler("VsirDesk");
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

            key.SetValue(RegistryValueName, $"\"{exePath}\"");
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
            return key?.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value);
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
        if (!OperatingSystem.IsWindows() || !IsRegisteredInTaskScheduler(taskName))
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

    private static bool DeleteRunKeyValue(string valueName)
    {
        try
        {
            if (!OperatingSystem.IsWindows())
            {
                return false;
            }

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            if (key?.GetValue(valueName) == null)
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
