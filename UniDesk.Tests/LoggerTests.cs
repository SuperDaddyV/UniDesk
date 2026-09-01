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

    [Fact]
    public void LogError_ShouldRecordStableTypeWithoutExceptionPayloadOrStackTrace()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk-logs-{Guid.NewGuid():N}");
        const string secretPath = @"C:\Users\Alice\Documents\private-note.txt";
        const string apiKey = "secret-weather-key";

        try
        {
            using (Logger.UseLogDirectory(temporaryDirectory))
            {
                try
                {
                    throw new InvalidOperationException(
                        $"Cannot open {secretPath}; apiKey={apiKey}");
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "LoggerTests.SensitiveException");
                }
            }

            var log = File.ReadAllText(Assert.Single(Directory.GetFiles(temporaryDirectory, "*.log")));
            Assert.Contains(nameof(InvalidOperationException), log, StringComparison.Ordinal);
            Assert.DoesNotContain(secretPath, log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(apiKey, log, StringComparison.Ordinal);
            Assert.DoesNotContain(nameof(LogError_ShouldRecordStableTypeWithoutExceptionPayloadOrStackTrace), log, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(temporaryDirectory))
            {
                Directory.Delete(temporaryDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public void LogMessages_ShouldRedactPathsCredentialsAndCoordinates()
    {
        var temporaryDirectory = Path.Combine(
            Path.GetTempPath(),
            $"UniDesk-logs-{Guid.NewGuid():N}");

        try
        {
            using (Logger.UseLogDirectory(temporaryDirectory))
            {
                Logger.LogWarning(
                    "Path=C:\\Users\\Alice\\secret.txt\n" +
                    "WeatherApiKey=abc123\n" +
                    "Authorization: Bearer auth-secret\n" +
                    "location=39.904200,116.407400\n" +
                    "https://example.test/data?token=xyz",
                    "LoggerTests.SensitiveMessage");
            }

            var log = File.ReadAllText(Assert.Single(Directory.GetFiles(temporaryDirectory, "*.log")));
            Assert.Contains("[REDACTED]", log, StringComparison.Ordinal);
            Assert.DoesNotContain(@"C:\Users\Alice", log, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("abc123", log, StringComparison.Ordinal);
            Assert.DoesNotContain("auth-secret", log, StringComparison.Ordinal);
            Assert.DoesNotContain("39.904200", log, StringComparison.Ordinal);
            Assert.DoesNotContain("116.407400", log, StringComparison.Ordinal);
            Assert.DoesNotContain("token=xyz", log, StringComparison.OrdinalIgnoreCase);
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
