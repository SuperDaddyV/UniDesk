using System.Reflection;

namespace UniDesk.Helpers;

public static class AppVersionProvider
{
    public static string CurrentVersion => GetCurrentVersion();

    public static string CurrentVersionWithPrefix => "v" + CurrentVersion;

    private static string GetCurrentVersion()
    {
        var assembly = typeof(AppVersionProvider).Assembly;
        var informationalVersion = assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            return TrimMetadata(informationalVersion);
        }

        return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string TrimMetadata(string version)
    {
        var normalized = version.Trim();
        var plusIndex = normalized.IndexOf('+');
        if (plusIndex >= 0)
        {
            normalized = normalized[..plusIndex];
        }

        return normalized.TrimStart('v', 'V');
    }
}
