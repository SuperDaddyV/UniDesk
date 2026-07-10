using UniDesk.Helpers;

namespace UniDesk.Tests;

public class LogRetentionServiceTests
{
    [Fact]
    public void DeleteExpiredLogs_ShouldDeleteOnlyExpiredStandardTopLevelLogs()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"unidesk-logs-{Guid.NewGuid():N}");
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nested);
        var expired = Path.Combine(directory, "2026-07-02.log");
        var retained = Path.Combine(directory, "2026-07-04.log");
        var unknown = Path.Combine(directory, "notes.log");
        var nestedExpired = Path.Combine(nested, "2026-07-01.log");
        File.WriteAllText(expired, "old");
        File.WriteAllText(retained, "keep");
        File.WriteAllText(unknown, "keep");
        File.WriteAllText(nestedExpired, "keep");

        try
        {
            var deleted = LogRetentionService.DeleteExpiredLogs(
                directory,
                new DateOnly(2026, 7, 10));

            Assert.Equal(1, deleted);
            Assert.False(File.Exists(expired));
            Assert.True(File.Exists(retained));
            Assert.True(File.Exists(unknown));
            Assert.True(File.Exists(nestedExpired));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
