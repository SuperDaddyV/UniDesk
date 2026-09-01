using Microsoft.Data.Sqlite;
using System.IO;

namespace UniDesk.Helpers;

public static class DirectoryHelper
{
    private static readonly string LocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    private static readonly string AppDataPath = System.IO.Path.Combine(LocalAppData, "UniDesk");
    private static readonly string LegacyAppDataPath = System.IO.Path.Combine(LocalAppData, "LumiDesk");

    public static string AppData => AppDataPath;
    public static string DataDirectory => AppDataPath;
    public static string DatabaseFile => System.IO.Path.Combine(AppDataPath, "UniDesk.db");
    public static string IconsDirectory => System.IO.Path.Combine(AppDataPath, "icons");
    public static string LogsDirectory => System.IO.Path.Combine(AppDataPath, "logs");
    public static string CacheDirectory => System.IO.Path.Combine(AppDataPath, "cache");

    public static void EnsureDirectoriesExist()
    {
        MigrateLegacyDataIfNeeded();

        if (!System.IO.Directory.Exists(AppDataPath))
            System.IO.Directory.CreateDirectory(AppDataPath);

        if (!System.IO.Directory.Exists(IconsDirectory))
            System.IO.Directory.CreateDirectory(IconsDirectory);

        if (!System.IO.Directory.Exists(LogsDirectory))
            System.IO.Directory.CreateDirectory(LogsDirectory);

        if (!System.IO.Directory.Exists(CacheDirectory))
            System.IO.Directory.CreateDirectory(CacheDirectory);
    }

    private static void MigrateLegacyDataIfNeeded()
    {
        MigrateLegacyDataIfNeeded(
            LegacyAppDataPath,
            AppDataPath,
            DatabaseFile,
            System.IO.Path.Combine(System.IO.Path.GetTempPath(), "UniDesk-migration-error.log"));
    }

    internal static void MigrateLegacyDataIfNeeded(
        string legacyAppDataPath,
        string appDataPath,
        string databaseFile,
        string fallbackLog)
    {
        if (System.IO.File.Exists(databaseFile) ||
            !System.IO.Directory.Exists(legacyAppDataPath))
        {
            return;
        }

        string? temporaryDatabase = null;
        try
        {
            var legacyDatabase = System.IO.Path.Combine(legacyAppDataPath, "LumiDesk.db");
            var hasLegacyDatabase = System.IO.File.Exists(legacyDatabase);
            if (!System.IO.Directory.Exists(appDataPath))
            {
                System.IO.Directory.CreateDirectory(appDataPath);
            }

            if (hasLegacyDatabase)
            {
                temporaryDatabase = System.IO.Path.Combine(
                    appDataPath,
                    $".UniDesk.db.{Guid.NewGuid():N}.tmp");
                BackupLegacyDatabase(legacyDatabase, temporaryDatabase);
                ValidateLegacyDatabase(temporaryDatabase);
            }

            CopyDirectoryWithoutOverwrite(legacyAppDataPath, appDataPath);

            if (temporaryDatabase != null)
            {
                if (System.IO.File.Exists(databaseFile))
                {
                    throw new IOException("目标数据库在迁移过程中出现，已停止发布旧数据。");
                }

                System.IO.File.Move(temporaryDatabase, databaseFile, overwrite: false);
                temporaryDatabase = null;
            }
        }
        catch (Exception ex)
        {
            try
            {
                System.IO.File.AppendAllText(
                    fallbackLog,
                    $"[{DateTime.UtcNow:O}] {ex.GetType().Name} (0x{ex.HResult:X8}){Environment.NewLine}");
            }
            catch
            {
            }

            throw new System.IO.IOException(
                $"无法迁移旧版 LumiDesk 用户数据，已停止启动以避免创建不完整的数据副本。诊断日志：{fallbackLog}",
                ex);
        }
        finally
        {
            if (temporaryDatabase != null)
            {
                try
                {
                    if (System.IO.File.Exists(temporaryDatabase))
                    {
                        System.IO.File.Delete(temporaryDatabase);
                    }

                    DeleteTemporarySidecar(temporaryDatabase + "-wal");
                    DeleteTemporarySidecar(temporaryDatabase + "-shm");
                }
                catch
                {
                }
            }
        }
    }

    private static void CopyDirectoryWithoutOverwrite(string sourceDirectory, string targetDirectory)
    {
        foreach (var directory in System.IO.Directory.EnumerateDirectories(sourceDirectory, "*", System.IO.SearchOption.AllDirectories))
        {
            if ((new System.IO.DirectoryInfo(directory).Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("旧版数据目录包含不受支持的重解析点。");
            }

            var relativePath = System.IO.Path.GetRelativePath(sourceDirectory, directory);
            System.IO.Directory.CreateDirectory(System.IO.Path.Combine(targetDirectory, relativePath));
        }

        foreach (var file in System.IO.Directory.EnumerateFiles(sourceDirectory, "*", System.IO.SearchOption.AllDirectories))
        {
            var fileName = System.IO.Path.GetFileName(file);
            if (fileName.StartsWith("LumiDesk.db", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if ((new System.IO.FileInfo(file).Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("旧版数据目录包含不受支持的重解析文件。");
            }

            var relativePath = System.IO.Path.GetRelativePath(sourceDirectory, file);
            var targetFile = System.IO.Path.Combine(targetDirectory, relativePath);
            var targetFileDirectory = System.IO.Path.GetDirectoryName(targetFile);
            if (!string.IsNullOrWhiteSpace(targetFileDirectory))
            {
                System.IO.Directory.CreateDirectory(targetFileDirectory);
            }

            if (!System.IO.File.Exists(targetFile))
            {
                System.IO.File.Copy(file, targetFile, overwrite: false);
            }
        }
    }

    private static void ValidateLegacyDatabase(string databaseFile)
    {
        try
        {
            using var connection = new SqliteConnection($"Data Source={databaseFile};Mode=ReadOnly");
            connection.Open();

            using (var quickCheck = connection.CreateCommand())
            {
                quickCheck.CommandText = "PRAGMA quick_check";
                var result = quickCheck.ExecuteScalar()?.ToString();
                if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("旧版数据库完整性检查未通过。");
                }
            }

            var tables = ReadTableNames(connection);
            var knownTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Settings", "Notes", "Todos", "Shortcuts", "QuickNotes", "ClipboardHistory", "TextSnippets"
            };
            if (tables.Any(table => !knownTables.Contains(table)))
            {
                throw new InvalidDataException("旧版数据库包含无法识别的数据表。");
            }

            ValidateKnownSchemaObjectTypes(connection, knownTables);

            RequireColumns(connection, "Settings", "Key", "Value");
            RequireColumns(connection, "Notes", "Id", "Title", "Content", "Color", "CreatedAt", "UpdatedAt");
            RequireColumns(connection, "Todos", "Id", "Title", "IsCompleted", "DueDate", "CreatedAt", "CompletedAt");
            RequireColumns(connection, "Shortcuts", "Id", "Name", "Path", "Type", "IconPath", "CreatedAt");

            if (!tables.Contains("Settings", StringComparer.OrdinalIgnoreCase) ||
                !tables.Contains("Notes", StringComparer.OrdinalIgnoreCase) ||
                !tables.Contains("Todos", StringComparer.OrdinalIgnoreCase) ||
                !tables.Contains("Shortcuts", StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException("旧版数据库缺少必要的数据表。");
            }

            using var versionCommand = connection.CreateCommand();
            versionCommand.CommandText = "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'";
            var version = versionCommand.ExecuteScalar()?.ToString();
            if (string.IsNullOrWhiteSpace(version) ||
                !Version.TryParse(version, out var parsed) ||
                parsed > new Version(1, 5))
            {
                throw new InvalidDataException("旧版数据库版本缺失、格式无效或超出支持范围。");
            }

            ValidateVersionedSchema(connection, tables, parsed);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static void BackupLegacyDatabase(string sourceFile, string destinationFile)
    {
        try
        {
            using var source = new SqliteConnection($"Data Source={sourceFile};Mode=ReadOnly");
            source.Open();
            using var destination = new SqliteConnection($"Data Source={destinationFile}");
            destination.Open();
            source.BackupDatabase(destination);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
        }
    }

    private static void DeleteTemporarySidecar(string sidecar)
    {
        if (System.IO.File.Exists(sidecar))
        {
            System.IO.File.Delete(sidecar);
        }
    }

    private static List<string> ReadTableNames(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static void ValidateKnownSchemaObjectTypes(
        SqliteConnection connection,
        IReadOnlySet<string> knownTables)
    {
        using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT type, name, sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'";
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var type = reader.GetString(0);
            var name = reader.GetString(1);
            if (string.Equals(type, "table", StringComparison.OrdinalIgnoreCase))
            {
                var sql = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                if (!knownTables.Contains(name) ||
                    sql.Contains("VIRTUAL TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException($"旧版数据库对象 {name} 不是受支持的数据表。");
                }
            }
            else if (!string.Equals(type, "index", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"旧版数据库包含不受支持的 {type} 对象 {name}。");
            }
        }
    }

    private static void RequireColumns(SqliteConnection connection, string table, params string[] requiredColumns)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        using var reader = command.ExecuteReader();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        if (requiredColumns.Any(column => !columns.Contains(column)))
        {
            throw new InvalidDataException($"旧版数据库表 {table} 的结构不完整。");
        }
    }

    private static void ValidateVersionedSchema(
        SqliteConnection connection,
        IReadOnlyCollection<string> tables,
        Version version)
    {
        if (version.Major != 1 || version.Minor < 0 || version.Minor > 5)
        {
            throw new InvalidDataException("旧版数据库版本不是受支持的迁移里程碑。");
        }

        if (version >= new Version(1, 1))
        {
            RequireColumns(connection, "Todos", "Priority");
        }

        if (version >= new Version(1, 2))
        {
            RequireColumns(connection, "Shortcuts", "LaunchArguments");
        }

        if (version >= new Version(1, 4))
        {
            RequireTableWithColumns(
                connection,
                tables,
                "QuickNotes",
                "Id", "Title", "Content", "IsPinned", "SortOrder", "CreatedAt", "UpdatedAt");
        }

        if (version >= new Version(1, 5))
        {
            RequireTableWithColumns(
                connection,
                tables,
                "ClipboardHistory",
                "Id", "Content", "ContentHash", "CreatedAt", "LastUsedAt", "UseCount");
            RequireTableWithColumns(
                connection,
                tables,
                "TextSnippets",
                "Id", "Title", "Content", "Category", "IsPinned", "SortOrder", "UseCount", "CreatedAt", "UpdatedAt", "LastUsedAt");
        }
    }

    private static void RequireTableWithColumns(
        SqliteConnection connection,
        IReadOnlyCollection<string> tables,
        string table,
        params string[] requiredColumns)
    {
        if (!tables.Contains(table, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"旧版数据库缺少版本 {table} 数据表。");
        }

        RequireColumns(connection, table, requiredColumns);
    }
}
