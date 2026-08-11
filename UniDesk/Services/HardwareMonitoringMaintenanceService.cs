using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using UniDesk.Hardware.Contracts;

namespace UniDesk.Services;

public enum HardwareRepairLaunchStatus
{
    Succeeded,
    Cancelled,
    HelperMissing,
    Failed
}

public sealed record HardwareRepairLaunchResult(
    HardwareRepairLaunchStatus Status,
    int? ExitCode = null,
    string? Error = null);

public interface IHardwareMonitoringMaintenanceService
{
    Task<HardwareServiceDiagnosticStatus> GetStatusAsync(
        CancellationToken cancellationToken = default);
    Task<HardwareRepairLaunchResult> RepairAsync(
        CancellationToken cancellationToken = default);
}

public sealed class HardwareMonitoringMaintenanceService : IHardwareMonitoringMaintenanceService
{
    public const string ProtectedComponentDirectoryName = "UniDesk";
    public const string RepairHelperRelativePath =
        @"HardwareRepair\UniDesk.HardwareRepair.exe";

    private readonly IHardwareMetricsDiagnosticsSource _diagnosticsSource;
    private readonly string _repairHelperPath;

    public HardwareMonitoringMaintenanceService(IHardwareMetricsDiagnosticsSource diagnosticsSource)
        : this(
            diagnosticsSource,
            GetDefaultRepairHelperPath(Environment.GetFolderPath(
                Environment.SpecialFolder.CommonProgramFiles)))
    {
    }

    internal static string GetDefaultRepairHelperPath(string commonProgramFilesPath) =>
        Path.Combine(
            commonProgramFilesPath,
            ProtectedComponentDirectoryName,
            RepairHelperRelativePath);

    public HardwareMonitoringMaintenanceService(
        IHardwareMetricsDiagnosticsSource diagnosticsSource,
        string repairHelperPath)
    {
        _diagnosticsSource = diagnosticsSource;
        _repairHelperPath = repairHelperPath;
    }

    public async Task<HardwareServiceDiagnosticStatus> GetStatusAsync(
        CancellationToken cancellationToken = default)
    {
        var diagnostics = await Task.Run(
            _diagnosticsSource.CaptureDiagnostics,
            cancellationToken).ConfigureAwait(false);
        return diagnostics.HardwareServiceStatus ?? new HardwareServiceDiagnosticStatus(
            HardwareServiceAvailability.ServiceUnavailable,
            new PawnIoStatus(false, null),
            null,
            "Hardware service status is unavailable.");
    }

    public async Task<HardwareRepairLaunchResult> RepairAsync(
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_repairHelperPath))
        {
            return new HardwareRepairLaunchResult(HardwareRepairLaunchStatus.HelperMissing);
        }

        try
        {
            using var process = Process.Start(new ProcessStartInfo(_repairHelperPath)
            {
                UseShellExecute = true,
                Arguments = "--install-or-repair"
            });
            if (process == null)
            {
                return new HardwareRepairLaunchResult(
                    HardwareRepairLaunchStatus.Failed,
                    Error: "The repair helper could not be started.");
            }

            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            return process.ExitCode == 0
                ? new HardwareRepairLaunchResult(HardwareRepairLaunchStatus.Succeeded, 0)
                : new HardwareRepairLaunchResult(
                    HardwareRepairLaunchStatus.Failed,
                    process.ExitCode,
                    $"Repair helper exited with code {process.ExitCode}.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return new HardwareRepairLaunchResult(HardwareRepairLaunchStatus.Cancelled);
        }
        catch (Exception ex)
        {
            return new HardwareRepairLaunchResult(
                HardwareRepairLaunchStatus.Failed,
                Error: $"{ex.GetType().Name}: {ex.Message}");
        }
    }
}
