using UniDesk.HardwareRepair;

namespace UniDesk.Tests;

public class HardwareRepairTests
{
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

    private static HardwareMaintenanceRunner CreateRunner(IProcessRunner processRunner)
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
                $"UniDesk_hardware_repair_test_{Guid.NewGuid():N}.log")));
    }

    private sealed class RecordingProcessRunner(
        int serviceCreateExitCode = 0,
        int pawnIoQueryExitCode = 1060) : IProcessRunner
    {
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
                ["query", "PawnIO"] => pawnIoQueryExitCode,
                ["create", ..] => serviceCreateExitCode,
                _ => 0
            };
            return new ProcessExecutionResult(exitCode, string.Empty, string.Empty);
        }
    }

    private sealed record ProcessCall(string FileName, IReadOnlyList<string> Arguments);
}
