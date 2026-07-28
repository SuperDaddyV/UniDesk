namespace UniDesk.HardwareRepair;

internal sealed class HardwareMaintenanceRunner
{
    internal const string ServiceName = "UniDeskHardwareService";
    internal const string PawnIoServiceName = "PawnIO";
    private const string ServiceDisplayName = "UniDesk Hardware Monitoring Service";
    private const string ServiceDescription =
        "为 UniDesk 提供只读的本机硬件传感器快照；主程序保持普通权限。";
    private readonly IProcessRunner _processRunner;
    private readonly HardwarePackageVerifier _packageVerifier;
    private readonly HardwareRepairLogger _logger;
    private readonly string _pawnIoInstallerPath;
    private readonly string _serviceBinaryPath;
    private readonly string _scPath;

    public HardwareMaintenanceRunner()
        : this(CreateDefaultPaths(), new SystemProcessRunner(), new HardwareRepairLogger())
    {
    }

    internal HardwareMaintenanceRunner(
        HardwareMaintenancePaths paths,
        IProcessRunner processRunner,
        HardwareRepairLogger logger)
    {
        _processRunner = processRunner;
        _logger = logger;
        _packageVerifier = new HardwarePackageVerifier(processRunner, logger);
        _pawnIoInstallerPath = paths.PawnIoInstallerPath;
        _serviceBinaryPath = paths.ServiceBinaryPath;
        _scPath = paths.ServiceControlPath;
    }

    public HardwareRepairExitCode InstallOrRepair()
    {
        _logger.Log("Install-or-repair started.");
        var pawnIoStatus = RunSc(["query", PawnIoServiceName]);
        if (pawnIoStatus.ExitCode == 1060)
        {
            var verification = _packageVerifier.VerifyPawnIo(_pawnIoInstallerPath);
            var verificationExitCode = verification switch
            {
                HardwarePackageVerificationResult.Valid => HardwareRepairExitCode.Success,
                HardwarePackageVerificationResult.Missing => HardwareRepairExitCode.PawnIoInstallerMissing,
                HardwarePackageVerificationResult.HashMismatch => HardwareRepairExitCode.PawnIoHashMismatch,
                _ => HardwareRepairExitCode.PawnIoSignatureInvalid
            };
            if (verificationExitCode != HardwareRepairExitCode.Success)
            {
                return Complete(verificationExitCode);
            }

            var pawnIoInstall = Run(
                _pawnIoInstallerPath,
                ["-install", "-silent"],
                TimeSpan.FromMinutes(2));
            if (pawnIoInstall.ExitCode != 0)
            {
                return Complete(HardwareRepairExitCode.PawnIoInstallFailed);
            }
        }
        else if (pawnIoStatus.ExitCode != 0)
        {
            return Complete(HardwareRepairExitCode.PawnIoStatusCheckFailed);
        }
        else
        {
            _logger.Log("PawnIO is already installed; the installer will not be run again.");
        }

        var pawnIoStart = RunSc(["start", PawnIoServiceName]);
        if (pawnIoStart.ExitCode is not (0 or 1056))
        {
            return Complete(HardwareRepairExitCode.PawnIoStartFailed);
        }

        if (!File.Exists(_serviceBinaryPath))
        {
            return Complete(HardwareRepairExitCode.ServiceBinaryMissing);
        }

        var quotedServiceBinaryPath = $"\"{_serviceBinaryPath}\"";
        var create = RunSc([
            "create", ServiceName,
            "binPath=", quotedServiceBinaryPath,
            "start=", "auto",
            "obj=", "LocalSystem",
            "DisplayName=", ServiceDisplayName
        ]);
        if (create.ExitCode is not (0 or 1073))
        {
            return Complete(HardwareRepairExitCode.ServiceCreateFailed);
        }

        var configure = RunSc([
            "config", ServiceName,
            "binPath=", quotedServiceBinaryPath,
            "start=", "auto",
            "obj=", "LocalSystem",
            "DisplayName=", ServiceDisplayName
        ]);
        if (configure.ExitCode != 0)
        {
            return Complete(HardwareRepairExitCode.ServiceConfigureFailed);
        }

        var description = RunSc(["description", ServiceName, ServiceDescription]);
        if (description.ExitCode != 0)
        {
            return Complete(HardwareRepairExitCode.ServiceDescriptionFailed);
        }

        var recovery = RunSc([
            "failure", ServiceName,
            "reset=", "86400",
            "actions=", "restart/5000/restart/15000"
        ]);
        if (recovery.ExitCode != 0)
        {
            return Complete(HardwareRepairExitCode.ServiceRecoveryFailed);
        }

        var start = RunSc(["start", ServiceName]);
        if (start.ExitCode is not (0 or 1056))
        {
            return Complete(HardwareRepairExitCode.ServiceStartFailed);
        }

        for (var attempt = 0; attempt < 20; attempt++)
        {
            var health = Run(_serviceBinaryPath, ["--health-check"], TimeSpan.FromSeconds(5));
            if (health.ExitCode == 0)
            {
                return Complete(HardwareRepairExitCode.Success);
            }

            Thread.Sleep(500);
        }

        return Complete(HardwareRepairExitCode.HealthCheckFailed);
    }

    public HardwareRepairExitCode RemoveService()
    {
        _logger.Log("Remove-service started.");
        var stop = RunSc(["stop", ServiceName]);
        if (stop.ExitCode is not (0 or 1060 or 1062))
        {
            _logger.Log($"Service stop returned {stop.ExitCode}; delete will still be attempted.");
        }

        var delete = RunSc(["delete", ServiceName]);
        return Complete(delete.ExitCode is 0 or 1060
            ? HardwareRepairExitCode.Success
            : HardwareRepairExitCode.ServiceRemoveFailed);
    }

    private ProcessExecutionResult RunSc(IReadOnlyList<string> arguments) =>
        Run(_scPath, arguments, TimeSpan.FromSeconds(30));

    private ProcessExecutionResult Run(
        string fileName,
        IReadOnlyList<string> arguments,
        TimeSpan timeout)
    {
        var result = _processRunner.Run(fileName, arguments, timeout);
        _logger.Log($"{Path.GetFileName(fileName)} step returned {result.ExitCode}; timedOut={result.TimedOut}.");
        return result;
    }

    private HardwareRepairExitCode Complete(HardwareRepairExitCode result)
    {
        _logger.Log($"Hardware maintenance completed with {(int)result} ({result}).");
        return result;
    }

    private static HardwareMaintenancePaths CreateDefaultPaths()
    {
        var helperDirectory = AppContext.BaseDirectory.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        var appDirectory = Directory.GetParent(helperDirectory)?.FullName
            ?? throw new InvalidOperationException("Unable to resolve the UniDesk installation directory.");
        return new HardwareMaintenancePaths(
            Path.Combine(appDirectory, "Hardware", "PawnIO_setup.exe"),
            Path.Combine(appDirectory, "HardwareService", "UniDesk.HardwareService.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "sc.exe"));
    }
}

internal sealed record HardwareMaintenancePaths(
    string PawnIoInstallerPath,
    string ServiceBinaryPath,
    string ServiceControlPath);
