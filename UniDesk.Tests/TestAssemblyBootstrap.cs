using System.Runtime.CompilerServices;
using UniDesk.Helpers;

namespace UniDesk.Tests;

internal static class TestAssemblyBootstrap
{
    internal static string LogDirectory { get; } = Path.Combine(
        Path.GetTempPath(),
        $"UniDesk-test-process-logs-{Environment.ProcessId}-{Guid.NewGuid():N}");

    [ModuleInitializer]
    internal static void Initialize()
    {
        Logger.UseProcessLogDirectory(LogDirectory);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => TryDeleteLogDirectory();
    }

    private static void TryDeleteLogDirectory()
    {
        try
        {
            if (Directory.Exists(LogDirectory))
            {
                Directory.Delete(LogDirectory, recursive: true);
            }
        }
        catch
        {
        }
    }
}
