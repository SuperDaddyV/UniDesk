namespace UniDesk.HardwareRepair;

internal sealed class HardwareMaintenanceRunner
{
    internal const string ServiceName = "UniDeskHardwareService";
    internal const string PawnIoServiceName = "PawnIO";
    private const int DriverUnavailableHealthCheckExitCode = 22;
    private const string ServiceDisplayName = "UniDesk Hardware Monitoring Service";
    private const int ServiceStopWaitAttempts = 20;
    private const string ServiceDescription =
        "为 UniDesk 提供只读的本机硬件传感器快照；主程序保持普通权限。";
    private readonly IProcessRunner _processRunner;
    private readonly HardwarePackageVerifier _packageVerifier;
    private readonly HardwareRepairLogger _logger;
    private readonly IServicePayloadSecurityVerifier _payloadSecurityVerifier;
    private readonly IServiceOwnershipVerifier _serviceOwnershipVerifier;
    private readonly string _pawnIoInstallerPath;
    private readonly string _serviceBinaryPath;
    private readonly string _scPath;
    private readonly string _taskKillPath;
    private readonly string _powerShellPath;
    private readonly Action<TimeSpan> _delay;

    public HardwareMaintenanceRunner()
        : this(
            CreateDefaultPaths(),
            new SystemProcessRunner(),
            new HardwareRepairLogger(),
            new ServicePayloadSecurityVerifier(),
            new ServiceOwnershipVerifier())
    {
    }

    internal HardwareMaintenanceRunner(
        HardwareMaintenancePaths paths,
        IProcessRunner processRunner,
        HardwareRepairLogger logger,
        IServicePayloadSecurityVerifier payloadSecurityVerifier,
        IServiceOwnershipVerifier serviceOwnershipVerifier,
        Action<TimeSpan>? delay = null)
    {
        _processRunner = processRunner;
        _logger = logger;
        _payloadSecurityVerifier = payloadSecurityVerifier;
        _serviceOwnershipVerifier = serviceOwnershipVerifier;
        _packageVerifier = new HardwarePackageVerifier(processRunner, logger);
        _pawnIoInstallerPath = paths.PawnIoInstallerPath;
        _serviceBinaryPath = paths.ServiceBinaryPath;
        _scPath = paths.ServiceControlPath;
        var systemDirectory = Path.GetDirectoryName(_scPath);
        _taskKillPath = string.IsNullOrEmpty(systemDirectory)
            ? "taskkill.exe"
            : Path.Combine(systemDirectory, "taskkill.exe");
        _powerShellPath = string.IsNullOrEmpty(systemDirectory)
            ? "powershell.exe"
            : Path.Combine(systemDirectory, "WindowsPowerShell", "v1.0", "powershell.exe");
        _delay = delay ?? Thread.Sleep;
    }

    public HardwareRepairExitCode InstallOrRepair()
    {
        _logger.Log("Install-or-repair started.");
        if (!File.Exists(_serviceBinaryPath))
        {
            return Complete(HardwareRepairExitCode.ServiceBinaryMissing);
        }

        var payloadSecurity = _payloadSecurityVerifier.Verify(_serviceBinaryPath);
        if (!payloadSecurity.IsSecure)
        {
            _logger.Log($"Service payload security verification failed: {payloadSecurity.Reason}");
            return Complete(HardwareRepairExitCode.ServicePayloadSecurityInvalid);
        }

        var serviceOwnership = _serviceOwnershipVerifier.Verify(ServiceName, _serviceBinaryPath);
        if (serviceOwnership.Status is ServiceOwnershipStatus.Foreign or ServiceOwnershipStatus.Unavailable)
        {
            _logger.Log($"Service ownership verification failed: {serviceOwnership.Reason}");
            return Complete(HardwareRepairExitCode.ServiceOwnershipInvalid);
        }

        var pawnIoStatus = RunSc(["query", PawnIoServiceName]);
        var pawnIoWasAlreadyRegistered = pawnIoStatus.ExitCode == 0;
        if (pawnIoStatus.ExitCode == 1060)
        {
            var installFailure = InstallVerifiedPawnIo();
            if (installFailure.HasValue)
            {
                return Complete(installFailure.Value);
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

        var quotedServiceBinaryPath = $"\"{_serviceBinaryPath}\"";
        if (serviceOwnership.Status == ServiceOwnershipStatus.Missing)
        {
            var create = RunSc([
                "create", ServiceName,
                "binPath=", quotedServiceBinaryPath,
                "start=", "auto",
                "obj=", "LocalSystem",
                "DisplayName=", ServiceDisplayName
            ]);
            if (create.ExitCode == 1073)
            {
                serviceOwnership = _serviceOwnershipVerifier.Verify(ServiceName, _serviceBinaryPath);
                if (serviceOwnership.Status != ServiceOwnershipStatus.Owned)
                {
                    _logger.Log($"Service appeared during creation but is not owned: {serviceOwnership.Reason}");
                    return Complete(HardwareRepairExitCode.ServiceOwnershipInvalid);
                }
            }
            else if (create.ExitCode != 0)
            {
                return Complete(HardwareRepairExitCode.ServiceCreateFailed);
            }
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

        var lastHealthCheckExitCode = WaitForHealthyService();
        if (lastHealthCheckExitCode == 0)
        {
            return Complete(HardwareRepairExitCode.Success);
        }

        if (lastHealthCheckExitCode == DriverUnavailableHealthCheckExitCode &&
            pawnIoWasAlreadyRegistered)
        {
            _logger.Log("PawnIO is registered but unavailable; one verified repair will be attempted.");
            var repairFailure = InstallVerifiedPawnIo();
            if (repairFailure.HasValue)
            {
                return Complete(repairFailure.Value);
            }

            var pawnIoRestart = RunSc(["start", PawnIoServiceName]);
            if (pawnIoRestart.ExitCode is not (0 or 1056))
            {
                return Complete(HardwareRepairExitCode.PawnIoStartFailed);
            }

            var serviceStop = RunSc(["stop", ServiceName]);
            if (serviceStop.ExitCode is not (0 or 1060 or 1062) || !WaitForServiceStopped())
            {
                _logger.Log("The hardware service could not be restarted after PawnIO repair.");
                return Complete(HardwareRepairExitCode.HealthCheckFailed);
            }

            var serviceRestart = RunSc(["start", ServiceName]);
            if (serviceRestart.ExitCode is not (0 or 1056))
            {
                return Complete(HardwareRepairExitCode.ServiceStartFailed);
            }

            lastHealthCheckExitCode = WaitForHealthyService();
            if (lastHealthCheckExitCode == 0)
            {
                return Complete(HardwareRepairExitCode.Success);
            }
        }

        if (lastHealthCheckExitCode == DriverUnavailableHealthCheckExitCode)
        {
            _logger.Log("The low-level driver remained unavailable; UniDesk will use compatible user-mode hardware sources.");
            return Complete(HardwareRepairExitCode.HardwareCompatibilityMode);
        }

        return Complete(HardwareRepairExitCode.HealthCheckFailed);
    }

    private HardwareRepairExitCode? InstallVerifiedPawnIo()
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
            return verificationExitCode;
        }

        var pawnIoInstall = Run(
            _pawnIoInstallerPath,
            ["-install", "-silent"],
            TimeSpan.FromMinutes(2));
        return pawnIoInstall.ExitCode == 0
            ? null
            : HardwareRepairExitCode.PawnIoInstallFailed;
    }

    private int WaitForHealthyService()
    {
        var lastHealthCheckExitCode = -1;
        for (var attempt = 0; attempt < 20; attempt++)
        {
            var health = Run(_serviceBinaryPath, ["--health-check"], TimeSpan.FromSeconds(5));
            lastHealthCheckExitCode = health.ExitCode;
            if (health.ExitCode == 0)
            {
                break;
            }

            _delay(TimeSpan.FromMilliseconds(500));
        }

        return lastHealthCheckExitCode;
    }

    public HardwareRepairExitCode RemoveService()
    {
        _logger.Log("Remove-service started.");
        var ownership = _serviceOwnershipVerifier.Verify(ServiceName, _serviceBinaryPath);
        if (ownership.Status == ServiceOwnershipStatus.Missing)
        {
            return Complete(HardwareRepairExitCode.Success);
        }
        if (ownership.Status != ServiceOwnershipStatus.Owned)
        {
            _logger.Log($"Service removal rejected: {ownership.Reason}");
            return Complete(HardwareRepairExitCode.ServiceOwnershipInvalid);
        }

        var disable = RunSc(["config", ServiceName, "start=", "disabled"]);
        if (disable.ExitCode != 0)
        {
            _logger.Log($"Service disable returned {disable.ExitCode}; removal will continue fail-closed.");
        }

        var stop = RunSc(["stop", ServiceName]);
        _logger.Log($"Service stop request returned {stop.ExitCode}; actual stopped state will be verified.");

        ownership = _serviceOwnershipVerifier.Verify(ServiceName, _serviceBinaryPath);
        if (ownership.Status == ServiceOwnershipStatus.Missing)
        {
            return Complete(HardwareRepairExitCode.Success);
        }
        if (ownership.Status != ServiceOwnershipStatus.Owned)
        {
            _logger.Log($"Service ownership changed before process termination: {ownership.Reason}");
            return Complete(HardwareRepairExitCode.ServiceOwnershipInvalid);
        }

        Run(
            _taskKillPath,
            ["/F", "/FI", $"SERVICES eq {ServiceName}"],
            TimeSpan.FromSeconds(30));
        if (!WaitForServiceStopped())
        {
            _logger.Log("Service process could not be confirmed stopped; delete was not attempted.");
            return Complete(HardwareRepairExitCode.ServiceRemoveFailed);
        }

        var delete = RunSc(["delete", ServiceName]);
        return Complete(delete.ExitCode is 0 or 1060
            ? HardwareRepairExitCode.Success
            : HardwareRepairExitCode.ServiceRemoveFailed);
    }

    private bool WaitForServiceStopped()
    {
        const string command =
            "$env:PSModulePath=[IO.Path]::Combine($PSHOME,'Modules'); " +
            "$s = Get-Service -Name 'UniDeskHardwareService' -ErrorAction SilentlyContinue; " +
            "if (($null -eq $s) -or ($s.Status -eq 'Stopped')) { exit 0 } else { exit 1 }";
        for (var attempt = 0; attempt < ServiceStopWaitAttempts; attempt++)
        {
            var status = Run(
                _powerShellPath,
                ["-NoLogo", "-NoProfile", "-NonInteractive", "-Command", command],
                TimeSpan.FromSeconds(30));
            if (status.ExitCode == 0)
            {
                return true;
            }

            if (attempt + 1 < ServiceStopWaitAttempts)
            {
                _delay(TimeSpan.FromMilliseconds(500));
            }
        }

        return false;
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
