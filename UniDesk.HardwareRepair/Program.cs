using System.Security.Principal;

namespace UniDesk.HardwareRepair;

internal static class Program
{
    private static int Main(string[] args)
    {
        var logger = new HardwareRepairLogger();
        try
        {
            if (!IsElevated())
            {
                logger.Log("Hardware maintenance rejected because the process is not elevated.");
                return (int)HardwareRepairExitCode.NotElevated;
            }

            if (args.Length != 1)
            {
                return (int)HardwareRepairExitCode.InvalidArguments;
            }

            var runner = new HardwareMaintenanceRunner();
            return args[0] switch
            {
                "--install-or-repair" => (int)runner.InstallOrRepair(),
                "--remove-service" => (int)runner.RemoveService(),
                "--cleanup-startup" => (int)new StartupCleanupRunner().Cleanup(),
                _ => (int)HardwareRepairExitCode.InvalidArguments
            };
        }
        catch (Exception ex)
        {
            logger.Log($"Unexpected {ex.GetType().Name}: {ex.Message}");
            return (int)HardwareRepairExitCode.UnexpectedError;
        }
    }

    private static bool IsElevated()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
    }
}
