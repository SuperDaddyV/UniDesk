using System.Security.AccessControl;
using System.Reflection;
using UniDesk.HardwareRepair;

namespace UniDesk.Tests;

public class HardwareRepairTests
{
    [Fact]
    public void HardwareRepairLogger_ShouldRedactSensitivePayloadsBeforeWriting()
    {
        var sanitizer = typeof(HardwareRepairLogger).GetMethod(
            "SanitizeMessage",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(sanitizer);

        var sanitized = Assert.IsType<string>(sanitizer.Invoke(
            null,
            ["Path=C:\\Users\\Alice\\secret.txt\n" +
             "WeatherApiKey=abc123\n" +
             "Authorization: Bearer auth-secret\n" +
             "location=39.904200,116.407400\n" +
             "https://example.test/data?token=xyz"]));

        Assert.Contains("[REDACTED]", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain(@"C:\Users\Alice", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("abc123", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("auth-secret", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("39.904200", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("116.407400", sanitized, StringComparison.Ordinal);
        Assert.DoesNotContain("token=xyz", sanitized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain('\n', sanitized);
    }

    [Fact]
    public void HardwareRepairSources_ShouldNotPersistExceptionMessages()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));

        foreach (var sourceName in new[]
                 {
                     "Program.cs",
                     "StartupCleanupRunner.cs",
                     "ServiceOwnershipVerifier.cs",
                     "ServicePayloadSecurityVerifier.cs"
                 })
        {
            var source = File.ReadAllText(Path.Combine(
                projectRoot,
                "UniDesk.HardwareRepair",
                sourceName));
            Assert.DoesNotContain("ex.Message", source, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void HardwareRepairLogger_ShouldUseProtectedHandleAndRejectReparseOrHardlinkTargets()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var loggerSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "UniDesk.HardwareRepair",
            "HardwareRepairLogger.cs"));

        Assert.Contains("FILE_FLAG_OPEN_REPARSE_POINT", loggerSource, StringComparison.Ordinal);
        Assert.Contains("FILE_FLAG_BACKUP_SEMANTICS", loggerSource, StringComparison.Ordinal);
        Assert.Contains("GetFileInformationByHandle", loggerSource, StringComparison.Ordinal);
        Assert.Contains("NumberOfLinks", loggerSource, StringComparison.Ordinal);
        Assert.Contains("SetAccessRuleProtection", loggerSource, StringComparison.Ordinal);
        Assert.Contains("FileShare.Read", loggerSource, StringComparison.Ordinal);
        Assert.DoesNotContain("FileShare.Delete", loggerSource, StringComparison.Ordinal);
    }

    [Fact]
    public void HardwareRepairLogger_ShouldFailClosedForAnUntrustedTemporaryDirectory()
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk_hardware_logger_untrusted_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var logPath = Path.Combine(directory, "hardware-repair.log");

        try
        {
            new HardwareRepairLogger(logPath).Log("must not be written");

            Assert.False(File.Exists(logPath));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void ServicePayloadVerifier_ShouldVerifyOnlyTheDirectProgramFilesBoundary()
    {
        var protectedRoot = ServicePayloadSecurityVerifier.GetProtectedInstallationRoot(
            @"C:\Program Files\Common Files\UniDesk");

        Assert.Equal(@"C:\Program Files\Common Files", protectedRoot);
    }

    [Fact]
    public void ServicePayloadVerifier_ShouldVerifyCommonFilesAndProgramFilesBoundaries()
    {
        var boundaries = ServicePayloadSecurityVerifier.GetProtectedInstallationBoundaries(
            @"C:\Program Files\Common Files\UniDesk");

        Assert.Equal(
            [@"C:\Program Files\Common Files", @"C:\Program Files"],
            boundaries);
    }

    [Theory]
    [InlineData(FileSystemRights.Read)]
    [InlineData(FileSystemRights.ReadAndExecute)]
    [InlineData(FileSystemRights.Synchronize)]
    public void ServicePayloadVerifier_ReadOnlyRights_ShouldNotBeTreatedAsWritable(
        FileSystemRights rights)
    {
        Assert.False(ServicePayloadSecurityVerifier.HasDangerousWriteRights(rights));
    }

    [Theory]
    [InlineData(FileSystemRights.WriteData)]
    [InlineData(FileSystemRights.AppendData)]
    [InlineData(FileSystemRights.WriteExtendedAttributes)]
    [InlineData(FileSystemRights.WriteAttributes)]
    [InlineData(FileSystemRights.Delete)]
    [InlineData(FileSystemRights.DeleteSubdirectoriesAndFiles)]
    [InlineData(FileSystemRights.ChangePermissions)]
    [InlineData(FileSystemRights.TakeOwnership)]
    public void ServicePayloadVerifier_WriteRights_ShouldBeRejected(FileSystemRights rights)
    {
        Assert.True(ServicePayloadSecurityVerifier.HasDangerousWriteRights(rights));
    }

    [Fact]
    public void InstallOrRepair_WhenDriverRemainsUnavailable_ShouldReturnCompatibilityMode()
    {
        var processRunner = new RecordingProcessRunner(
            healthCheckExitCodes: Enumerable.Repeat(22, 20).ToArray());
        var runner = CreateRunner(processRunner, delay: _ => { });

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.HardwareCompatibilityMode, result);
    }

    [Fact]
    public void InstallOrRepair_WhenHealthCheckFailsForAnotherReason_ShouldReturnFailure()
    {
        var processRunner = new RecordingProcessRunner(
            healthCheckExitCodes: Enumerable.Repeat(21, 20).ToArray());
        var runner = CreateRunner(processRunner, delay: _ => { });

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.HealthCheckFailed, result);
    }

    [Fact]
    public void InstallOrRepair_ShouldRegisterQuotedServiceBinaryPath()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(processRunner);

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        var expectedBinaryPath = $"\"{typeof(HardwareRepairExitCode).Assembly.Location}\"";
        foreach (var operation in new[] { "create", "config" })
        {
            var serviceCall = Assert.Single(processRunner.Calls, call =>
                call.Arguments.Count > 0 && call.Arguments[0] == operation);
            var binPathIndex = Array.IndexOf(serviceCall.Arguments.ToArray(), "binPath=");
            Assert.True(binPathIndex >= 0);
            Assert.Equal(expectedBinaryPath, serviceCall.Arguments[binPathIndex + 1]);
        }
    }

    [Fact]
    public void InstallOrRepair_WhenServiceCreateReturns1639_ShouldReturnStableFailureCode()
    {
        var processRunner = new RecordingProcessRunner(serviceCreateExitCode: 1639);
        var runner = CreateRunner(processRunner);

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.ServiceCreateFailed, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            call.Arguments.Count > 0 && call.Arguments[0] == "config");
    }

    [Fact]
    public void InstallOrRepair_WhenPawnIoAlreadyExists_ShouldNotRunInstallerAgain()
    {
        var processRunner = new RecordingProcessRunner(pawnIoQueryExitCode: 0);
        var runner = CreateRunner(processRunner);

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals(
                "PawnIO_setup.exe",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["start", "PawnIO"]));
    }

    [Fact]
    public void InstallOrRepair_WhenExistingPawnIoRemainsUnavailable_ShouldRunOneVerifiedRepair()
    {
        var processRunner = new RecordingProcessRunner(
            pawnIoQueryExitCode: 0,
            healthCheckExitCodes: Enumerable.Repeat(22, 20).Append(0).ToArray());
        var runner = CreateRunner(processRunner, delay: _ => { });

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.Single(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals(
                "PawnIO_setup.exe",
                StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase) &&
            call.Arguments.Any(argument => argument.Contains(
                "Get-AuthenticodeSignature",
                StringComparison.Ordinal)));
    }

    [Fact]
    public void PawnIoSignatureVerification_ShouldIsolateWindowsPowerShellModulePath()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var processRunner = new RecordingProcessRunner();
        var verifier = new HardwarePackageVerifier(
            processRunner,
            new HardwareRepairLogger(Path.Combine(
                Path.GetTempPath(),
                $"UniDesk_hardware_verifier_test_{Guid.NewGuid():N}.log")));

        var result = verifier.VerifyPawnIo(Path.Combine(
            projectRoot,
            "installer-assets",
            "PawnIO_setup.exe"));

        Assert.Equal(HardwarePackageVerificationResult.Valid, result);
        var signatureCall = Assert.Single(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(signatureCall.Arguments, argument => argument.Contains(
            "$env:PSModulePath=[IO.Path]::Combine($PSHOME,'Modules')",
            StringComparison.Ordinal));
    }

    [Fact]
    public void InstallOrRepair_WhenPayloadAclIsUnsafe_ShouldRefuseServiceRegistration()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            new StubPayloadSecurityVerifier(false, "ordinary user can replace service payload"));

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.ServicePayloadSecurityInvalid, result);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public void InstallOrRepair_WhenExistingServiceIsForeign_ShouldRefuseToStopOrReconfigureIt()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Foreign));

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.ServiceOwnershipInvalid, result);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public void InstallOrRepair_WhenExistingServiceIsOwned_ShouldConfigureWithoutCreatingIt()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Owned));

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            call.Arguments.Count > 0 && call.Arguments[0] == "create");
        Assert.Contains(processRunner.Calls, call =>
            call.Arguments.Count > 0 && call.Arguments[0] == "config");
    }

    [Fact]
    public void InstallOrRepair_WhenCreateRacesWithForeignService_ShouldNotReconfigureIt()
    {
        var processRunner = new RecordingProcessRunner(serviceCreateExitCode: 1073);
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(
                ServiceOwnershipStatus.Missing,
                ServiceOwnershipStatus.Foreign));

        var result = runner.InstallOrRepair();

        Assert.Equal(HardwareRepairExitCode.ServiceOwnershipInvalid, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            call.Arguments.Count > 0 && call.Arguments[0] == "config");
    }

    [Fact]
    public void RemoveService_WhenServiceIsForeign_ShouldNeverStopOrDelete()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Foreign));

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.ServiceOwnershipInvalid, result);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public void RemoveService_WhenServiceOwnershipCannotBeRead_ShouldNeverStopOrDelete()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Unavailable));

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.ServiceOwnershipInvalid, result);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public void RemoveService_WhenServiceIsMissing_ShouldSucceedWithoutScCalls()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Missing));

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.Empty(processRunner.Calls);
    }

    [Fact]
    public void RemoveService_WhenOwned_ShouldDisableStopConfirmAndDelete()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Missing));

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.Success, result);
        Assert.Contains(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["config", "UniDeskHardwareService", "start=", "disabled"]));
        Assert.Contains(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals("taskkill.exe", StringComparison.OrdinalIgnoreCase) &&
            call.Arguments.SequenceEqual(["/F", "/FI", "SERVICES eq UniDeskHardwareService"]));
        Assert.Contains(processRunner.Calls, call =>
            Path.GetFileName(call.FileName).Equals("powershell.exe", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["delete", "UniDeskHardwareService"]));
    }

    [Fact]
    public void RemoveService_WhenOwnershipChangesAfterStop_ShouldFailWithoutDeleting()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Foreign),
            delay: _ => { });

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.ServiceOwnershipInvalid, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["delete", "UniDeskHardwareService"]));
    }

    [Fact]
    public void RemoveService_WhenDeleteIsAcceptedButServiceRemainsOwned_ShouldFail()
    {
        var processRunner = new RecordingProcessRunner();
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Owned,
                ServiceOwnershipStatus.Owned),
            delay: _ => { });

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.ServiceRemoveFailed, result);
        Assert.Contains(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["delete", "UniDeskHardwareService"]));
    }

    [Fact]
    public void RemoveService_WhenStopIsAcceptedButServiceNeverStops_ShouldFailWithoutDeleting()
    {
        var processRunner = new RecordingProcessRunner(
            serviceStoppedQueryExitCodes: Enumerable.Repeat(1, 20).ToArray());
        var runner = CreateRunner(
            processRunner,
            serviceOwnershipVerifier: new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Owned),
            delay: _ => { });

        var result = runner.RemoveService();

        Assert.Equal(HardwareRepairExitCode.ServiceRemoveFailed, result);
        Assert.DoesNotContain(processRunner.Calls, call =>
            call.Arguments.SequenceEqual(["delete", "UniDeskHardwareService"]));
    }

    private static HardwareMaintenanceRunner CreateRunner(
        IProcessRunner processRunner,
        IServicePayloadSecurityVerifier? payloadSecurityVerifier = null,
        IServiceOwnershipVerifier? serviceOwnershipVerifier = null,
        Action<TimeSpan>? delay = null)
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        return new HardwareMaintenanceRunner(
            new HardwareMaintenancePaths(
                Path.Combine(projectRoot, "installer-assets", "PawnIO_setup.exe"),
                typeof(HardwareRepairExitCode).Assembly.Location,
                "sc.exe"),
            processRunner,
            new HardwareRepairLogger(Path.Combine(
                Path.GetTempPath(),
                $"UniDesk_hardware_repair_test_{Guid.NewGuid():N}.log")),
            payloadSecurityVerifier ?? new StubPayloadSecurityVerifier(true, "secure"),
            serviceOwnershipVerifier ?? new StubServiceOwnershipVerifier(ServiceOwnershipStatus.Missing),
            delay);
    }

    private sealed class StubServiceOwnershipVerifier(params ServiceOwnershipStatus[] statuses)
        : IServiceOwnershipVerifier
    {
        private int _index;

        public ServiceOwnershipVerificationResult Verify(string serviceName, string expectedBinaryPath)
        {
            var index = Math.Min(_index++, statuses.Length - 1);
            return new(statuses[index], statuses[index].ToString());
        }
    }

    private sealed class StubPayloadSecurityVerifier(bool isSecure, string reason)
        : IServicePayloadSecurityVerifier
    {
        public ServicePayloadSecurityVerificationResult Verify(string serviceBinaryPath) =>
            new(isSecure, reason);
    }

    private sealed class RecordingProcessRunner(
        int serviceCreateExitCode = 0,
        int pawnIoQueryExitCode = 1060,
        IReadOnlyList<int>? serviceStoppedQueryExitCodes = null,
        IReadOnlyList<int>? healthCheckExitCodes = null) : IProcessRunner
    {
        private int _serviceStoppedQueryIndex;
        private int _healthCheckIndex;

        public List<ProcessCall> Calls { get; } = [];

        public ProcessExecutionResult Run(
            string fileName,
            IReadOnlyList<string> arguments,
            TimeSpan timeout)
        {
            var capturedArguments = arguments.ToArray();
            Calls.Add(new ProcessCall(fileName, capturedArguments));
            var exitCode = capturedArguments switch
            {
                ["--health-check"] => GetHealthCheckExitCode(),
                _ when Path.GetFileName(fileName).Equals(
                    "powershell.exe",
                    StringComparison.OrdinalIgnoreCase) => GetServiceStoppedQueryExitCode(),
                ["query", "PawnIO"] => pawnIoQueryExitCode,
                ["create", ..] => serviceCreateExitCode,
                _ => 0
            };
            return new ProcessExecutionResult(exitCode, string.Empty, string.Empty);
        }

        private int GetServiceStoppedQueryExitCode()
        {
            if (serviceStoppedQueryExitCodes == null || serviceStoppedQueryExitCodes.Count == 0)
            {
                return 0;
            }

            var index = Math.Min(_serviceStoppedQueryIndex++, serviceStoppedQueryExitCodes.Count - 1);
            return serviceStoppedQueryExitCodes[index];
        }

        private int GetHealthCheckExitCode()
        {
            if (healthCheckExitCodes == null || healthCheckExitCodes.Count == 0)
            {
                return 0;
            }

            var index = Math.Min(_healthCheckIndex++, healthCheckExitCodes.Count - 1);
            return healthCheckExitCodes[index];
        }
    }

    private sealed record ProcessCall(string FileName, IReadOnlyList<string> Arguments);
}
