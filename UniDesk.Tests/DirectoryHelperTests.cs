using UniDesk.Helpers;

namespace UniDesk.Tests;

public class DirectoryHelperTests
{
    [Fact]
    public void MigrateLegacyDataIfNeeded_WhenTargetDatabaseExists_ShouldIgnoreLegacyDirectory()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(Path.Combine(legacy, "nested"));
        Directory.CreateDirectory(target);
        File.WriteAllText(database, "existing database");
        File.WriteAllText(Path.Combine(target, "nested"), "blocks legacy directory copy");

        try
        {
            DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log);

            Assert.Equal("existing database", File.ReadAllText(database));
            Assert.False(File.Exists(log));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_WhenCopyFails_ShouldRecordAndPropagate()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(Path.Combine(legacy, "nested"));
        Directory.CreateDirectory(target);
        File.WriteAllText(Path.Combine(target, "nested"), "blocks directory creation");

        try
        {
            var exception = Assert.Throws<IOException>(() =>
                DirectoryHelper.MigrateLegacyDataIfNeeded(
                    legacy,
                    target,
                    Path.Combine(target, "UniDesk.db"),
                    log));

            Assert.Contains("停止启动", exception.Message, StringComparison.Ordinal);
            Assert.Contains(log, exception.Message, StringComparison.Ordinal);
            Assert.True(File.Exists(log));
            Assert.Contains("IOException", File.ReadAllText(log), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
