using System.Diagnostics;
using System.IO;
using UniDesk.Models;

namespace UniDesk.Helpers;

/// <summary>
/// 快捷方式启动逻辑（支持 Shell 协议、UWP 应用与备用路径）。
/// </summary>
public static class ShortcutLaunchHelper
{
    public static bool TryLaunch(ShortcutItem shortcut)
    {
        if (TryLaunchCore(shortcut))
        {
            return true;
        }

        return TryLaunchFallback(shortcut);
    }

    private static bool TryLaunchCore(ShortcutItem shortcut)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(shortcut.LaunchArguments))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = shortcut.Path,
                    Arguments = shortcut.LaunchArguments,
                    UseShellExecute = true
                });
                return true;
            }

            var path = shortcut.Path;

            if (path.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = path,
                    UseShellExecute = true
                });
                return true;
            }

            if (IsUriScheme(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path,
                    UseShellExecute = true
                });
                return true;
            }

            if (shortcut.Type == ShortcutType.Folder && Directory.Exists(path))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{path}\"",
                    UseShellExecute = true
                });
                return true;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ShortcutLaunchHelper.TryLaunchCore");
            return false;
        }
    }

    private static bool TryLaunchFallback(ShortcutItem shortcut)
    {
        if (!shortcut.Name.Contains("日历", StringComparison.Ordinal))
        {
            return false;
        }

        string[] calendarTargets =
        [
            @"shell:AppsFolder\Microsoft.Windows.Calendar_8wekyb3d8bbwe!App",
            @"shell:AppsFolder\microsoft.windowscommunicationsapps_8wekyb3d8bbwe!microsoft.windowslive.calendar",
            "ms-calendar:",
            "outlookcal:"
        ];

        foreach (var target in calendarTargets)
        {
            if (TryStartTarget(target))
            {
                return true;
            }
        }

        return false;
    }

    private static bool TryStartTarget(string target)
    {
        try
        {
            if (target.StartsWith("shell:", StringComparison.OrdinalIgnoreCase))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = target,
                    UseShellExecute = true
                });
            }
            else
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = target,
                    UseShellExecute = true
                });
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private static bool IsUriScheme(string path)
    {
        var colonIndex = path.IndexOf(':');
        return colonIndex > 0 &&
               colonIndex < 30 &&
               !Path.IsPathRooted(path) &&
               !path.StartsWith("\\\\", StringComparison.Ordinal);
    }
}
