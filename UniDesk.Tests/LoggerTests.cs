using UniDesk.Helpers;

namespace UniDesk.Tests;

public class LoggerTests
{
    [Fact]
    public void TestProcess_ShouldUseIsolatedLogDirectory()
    {
        var message = $"process-isolated test message {Guid.NewGuid():N}";

        Logger.LogInfo(message, "LoggerTests");

        var logFile = Assert.Single(Directory.GetFiles(TestAssemblyBootstrap.LogDirectory, "*.log"));
        Assert.Contains(message, File.ReadAllText(logFile));
        var defaultLogFile = Path.Combine(
            DirectoryHelper.LogsDirectory,
            $"{DateTime.Now:yyyy-MM-dd}.log");
        if (File.Exists(defaultLogFile))
        {
            Assert.DoesNotContain(message, File.ReadAllText(defaultLogFile));
        }
    }

    [Fact]
    public void UseLogDirectory_ShouldIsolateOutputFromProductionDirectory()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk-logs-{Guid.NewGuid():N}");
        var message = $"isolated test message {Guid.NewGuid():N}";

        try
        {
            using (Logger.UseLogDirectory(temporaryDirectory))
            {
                Logger.LogInfo(message, "LoggerTests");
            }

            var logFile = Assert.Single(Directory.GetFiles(temporaryDirectory, "*.log"));
            Assert.Contains(message, File.ReadAllText(logFile));
            var defaultLogFile = Path.Combine(
                DirectoryHelper.LogsDirectory,
                $"{DateTime.Now:yyyy-MM-dd}.log");
            if (File.Exists(defaultLogFile))
            {
                Assert.DoesNotContain(message, File.ReadAllText(defaultLogFile));
            }
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }
}

internal sealed class TestLogDirectoryScope : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"UniDesk-test-logs-{Guid.NewGuid():N}");
    private readonly IDisposable _scope;

    public TestLogDirectoryScope()
    {
        _scope = Logger.UseLogDirectory(_directory);
    }

    public void Dispose()
    {
        _scope.Dispose();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
