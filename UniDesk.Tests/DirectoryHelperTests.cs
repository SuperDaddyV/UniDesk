using UniDesk.Helpers;
using Microsoft.Data.Sqlite;

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

    [Fact]
    public void MigrateLegacyDataIfNeeded_WhenLegacyDatabaseIsNotSqlite_ShouldNotPublishIt()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(legacy);
        File.WriteAllText(Path.Combine(legacy, "LumiDesk.db"), "not a sqlite database");

        try
        {
            Assert.Throws<IOException>(() =>
                DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log));

            Assert.False(File.Exists(database));
            Assert.False(Directory.Exists(target) &&
                         Directory.EnumerateFiles(target, ".UniDesk.db.*.tmp").Any());
            var diagnostic = File.ReadAllText(log);
            Assert.DoesNotContain("not a sqlite database", diagnostic, StringComparison.Ordinal);
            Assert.DoesNotContain("StackTrace", diagnostic, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_WhenLegacyDatabaseIsValid_ShouldPublishValidatedCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(legacy);
        var legacyDatabase = Path.Combine(legacy, "LumiDesk.db");

        try
        {
            using (var connection = new SqliteConnection($"Data Source={legacyDatabase}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
                    CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT, Color TEXT NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
                    CREATE TABLE Todos (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, IsCompleted INTEGER NOT NULL, DueDate TEXT, CreatedAt TEXT NOT NULL, CompletedAt TEXT, Priority INTEGER NOT NULL DEFAULT 1);
                    CREATE TABLE Shortcuts (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Path TEXT NOT NULL, Type TEXT NOT NULL, IconPath TEXT, SortOrder INTEGER NOT NULL DEFAULT 0, CreatedAt TEXT NOT NULL, LaunchArguments TEXT);
                    CREATE TABLE QuickNotes (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT NOT NULL, IsPinned INTEGER NOT NULL, SortOrder INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
                    CREATE TABLE ClipboardHistory (Id INTEGER PRIMARY KEY, Content TEXT NOT NULL, ContentHash TEXT NOT NULL, CreatedAt TEXT NOT NULL, LastUsedAt TEXT NOT NULL, UseCount INTEGER NOT NULL);
                    CREATE TABLE TextSnippets (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT NOT NULL, Category TEXT NOT NULL, IsPinned INTEGER NOT NULL, SortOrder INTEGER NOT NULL, UseCount INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, LastUsedAt TEXT);
                    INSERT INTO Settings (Key, Value) VALUES ('DatabaseVersion', '1.5');
                    """;
                command.ExecuteNonQuery();
            }

            DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log);

            Assert.True(File.Exists(database));
            Assert.False(File.Exists(log));
            using var migrated = new SqliteConnection($"Data Source={database};Mode=ReadOnly");
            migrated.Open();
            using var check = migrated.CreateCommand();
            check.CommandText = "PRAGMA quick_check";
            Assert.Equal("ok", check.ExecuteScalar()?.ToString(), ignoreCase: true);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_Version15WithoutQuickText_ShouldRejectBeforePublish()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(legacy);
        var legacyDatabase = Path.Combine(legacy, "LumiDesk.db");

        try
        {
            CreateVersion15CoreDatabase(legacyDatabase, includeLaunchArguments: true, includeQuickText: false);

            Assert.Throws<IOException>(() =>
                DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log));

            Assert.False(File.Exists(database));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_Version15WithoutLaunchArguments_ShouldRejectBeforePublish()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(legacy);
        var legacyDatabase = Path.Combine(legacy, "LumiDesk.db");

        try
        {
            CreateVersion15CoreDatabase(legacyDatabase, includeLaunchArguments: false, includeQuickText: true);

            Assert.Throws<IOException>(() =>
                DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log));

            Assert.False(File.Exists(database));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public void MigrateLegacyDataIfNeeded_WithUnexpectedTrigger_ShouldRejectBeforePublish()
    {
        var root = Path.Combine(Path.GetTempPath(), $"UniDeskMigrationTest-{Guid.NewGuid():N}");
        var legacy = Path.Combine(root, "legacy");
        var target = Path.Combine(root, "target");
        var database = Path.Combine(target, "UniDesk.db");
        var log = Path.Combine(root, "migration.log");
        Directory.CreateDirectory(legacy);
        var legacyDatabase = Path.Combine(legacy, "LumiDesk.db");

        try
        {
            CreateVersion15CoreDatabase(legacyDatabase, includeLaunchArguments: true, includeQuickText: true);
            using (var connection = new SqliteConnection($"Data Source={legacyDatabase}"))
            {
                connection.Open();
                using var command = connection.CreateCommand();
                command.CommandText = """
                    CREATE TRIGGER unexpected_restore_trigger
                    AFTER INSERT ON Todos
                    BEGIN
                        DELETE FROM Notes;
                    END;
                    """;
                command.ExecuteNonQuery();
            }

            Assert.Throws<IOException>(() =>
                DirectoryHelper.MigrateLegacyDataIfNeeded(legacy, target, database, log));

            Assert.False(File.Exists(database));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            Directory.Delete(root, recursive: true);
        }
    }

    private static void CreateVersion15CoreDatabase(
        string databaseFile,
        bool includeLaunchArguments,
        bool includeQuickText)
    {
        using var connection = new SqliteConnection($"Data Source={databaseFile}");
        connection.Open();
        using var command = connection.CreateCommand();
        var launchArguments = includeLaunchArguments ? ", LaunchArguments TEXT" : string.Empty;
        var quickText = includeQuickText
            ? """
              CREATE TABLE ClipboardHistory (Id INTEGER PRIMARY KEY, Content TEXT NOT NULL, ContentHash TEXT NOT NULL, CreatedAt TEXT NOT NULL, LastUsedAt TEXT NOT NULL, UseCount INTEGER NOT NULL);
              CREATE TABLE TextSnippets (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT NOT NULL, Category TEXT NOT NULL, IsPinned INTEGER NOT NULL, SortOrder INTEGER NOT NULL, UseCount INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, LastUsedAt TEXT);
              """
            : string.Empty;
        command.CommandText = $"""
            CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
            CREATE TABLE Notes (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT, Color TEXT NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            CREATE TABLE Todos (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, IsCompleted INTEGER NOT NULL, DueDate TEXT, CreatedAt TEXT NOT NULL, CompletedAt TEXT, Priority INTEGER NOT NULL DEFAULT 1);
            CREATE TABLE Shortcuts (Id INTEGER PRIMARY KEY, Name TEXT NOT NULL, Path TEXT NOT NULL, Type TEXT NOT NULL, IconPath TEXT, SortOrder INTEGER NOT NULL, CreatedAt TEXT NOT NULL{launchArguments});
            CREATE TABLE QuickNotes (Id INTEGER PRIMARY KEY, Title TEXT NOT NULL, Content TEXT NOT NULL, IsPinned INTEGER NOT NULL, SortOrder INTEGER NOT NULL, CreatedAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL);
            {quickText}
            INSERT INTO Settings (Key, Value) VALUES ('DatabaseVersion', '1.5');
            """;
        command.ExecuteNonQuery();
    }
}
