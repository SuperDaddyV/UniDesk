using Microsoft.Data.Sqlite;
using System.Globalization;
using System.IO;
using System.Text.Json;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public class DatabaseService : IDatabaseService
{
    private const string DatabaseVersion = "1.5";
    private static readonly JsonSerializerOptions ModuleSettingsJsonOptions =
        new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;
    private readonly string _initialLanguage;
    private readonly IMonitorWorkAreaProvider _monitorWorkAreas;

    public DatabaseService(
        string? connectionString = null,
        string? initialLanguage = null,
        IMonitorWorkAreaProvider? monitorWorkAreas = null)
    {
        _connectionString = connectionString ?? $"Data Source={DirectoryHelper.DatabaseFile}";
        _initialLanguage = initialLanguage ?? ILocalizationService.DefaultLanguage;
        _monitorWorkAreas = monitorWorkAreas ?? Win32MonitorWorkAreaProvider.Instance;
    }

    public Task InitializeAsync() => Task.Run(InitializeCoreAsync);

    private async Task InitializeCoreAsync()
    {
        try
        {
            DirectoryHelper.EnsureDirectoriesExist();
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await EnableWalAsync(connection);

            var transactionStarted = false;
            try
            {
                await ExecuteConnectionCommandAsync(connection, "BEGIN IMMEDIATE");
                transactionStarted = true;

                var hasUserTables = await HasUserTablesAsync(connection);
                var version = await GetDatabaseVersionAsync(connection);
                if (!hasUserTables)
                {
                    await CreateTablesAsync(connection);
                    await SetDatabaseVersionAsync(connection, DatabaseVersion);
                }
                else
                {
                    var inferredVersion = false;
                    if (string.IsNullOrWhiteSpace(version))
                    {
                        version = await InferDatabaseVersionAsync(connection)
                            ?? throw new InvalidDataException(
                                "现有数据库缺少 DatabaseVersion，且无法识别为完整的已知数据库结构。");
                        inferredVersion = true;
                    }

                    var comparison = CompareDatabaseVersions(version, DatabaseVersion);
                    if (comparison > 0)
                    {
                        throw new InvalidDataException(
                            $"数据库版本 {version} 高于当前支持的版本 {DatabaseVersion}。");
                    }

                    if (comparison < 0)
                    {
                        await MigrateDatabaseAsync(connection, version, DatabaseVersion);
                        await SetDatabaseVersionAsync(connection, DatabaseVersion);
                    }
                    else if (inferredVersion)
                    {
                        await SetDatabaseVersionAsync(connection, DatabaseVersion);
                    }
                }

                await EnsureSchemaUpdatesAsync(connection);
                await ExecuteConnectionCommandAsync(connection, "COMMIT");
                transactionStarted = false;
            }
            catch
            {
                if (transactionStarted)
                {
                    try
                    {
                        await ExecuteConnectionCommandAsync(connection, "ROLLBACK");
                    }
                    catch (Exception rollbackException)
                    {
                        Logger.LogError(rollbackException, "DatabaseService.Initialize.Rollback");
                    }
                }

                throw;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "DatabaseService.Initialize");
            throw;
        }
    }

    private static async Task EnableWalAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA journal_mode=WAL";
        await command.ExecuteScalarAsync();
    }

    private static async Task ExecuteConnectionCommandAsync(SqliteConnection connection, string sql)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> HasUserTablesAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        return Convert.ToInt32(await command.ExecuteScalarAsync()) > 0;
    }

    private static async Task<string?> GetDatabaseVersionAsync(SqliteConnection connection)
    {
        var tableCheck = connection.CreateCommand();
        tableCheck.CommandText =
            "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'Settings'";
        if (Convert.ToInt32(await tableCheck.ExecuteScalarAsync()) == 0)
        {
            return null;
        }

        var command = connection.CreateCommand();
        command.CommandText = "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'";
        var result = await command.ExecuteScalarAsync();
        return result?.ToString();
    }

    private static async Task<string?> InferDatabaseVersionAsync(SqliteConnection connection)
    {
        var knownTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Settings", "Notes", "Todos", "Shortcuts", "QuickNotes", "ClipboardHistory", "TextSnippets"
        };
        var tables = await ReadTableNamesAsync(connection);
        if (tables.Any(table => !knownTables.Contains(table)) ||
            !tables.Contains("Settings", StringComparer.OrdinalIgnoreCase) ||
            !tables.Contains("Notes", StringComparer.OrdinalIgnoreCase) ||
            !tables.Contains("Todos", StringComparer.OrdinalIgnoreCase) ||
            !tables.Contains("Shortcuts", StringComparer.OrdinalIgnoreCase))
        {
            return null;
        }

        if (!await HasSupportedSchemaObjectsAsync(connection, knownTables))
        {
            return null;
        }

        if (!await HasColumnsAsync(connection, "Settings", "Key", "Value") ||
            !await HasColumnsAsync(connection, "Notes", "Id", "Title", "Content", "Color", "CreatedAt", "UpdatedAt") ||
            !await HasColumnsAsync(connection, "Todos", "Id", "Title", "IsCompleted", "DueDate", "CreatedAt", "CompletedAt") ||
            !await HasColumnsAsync(connection, "Shortcuts", "Id", "Name", "Path", "Type", "IconPath", "CreatedAt"))
        {
            return null;
        }

        var hasQuickNotes = tables.Contains("QuickNotes", StringComparer.OrdinalIgnoreCase);
        if (hasQuickNotes &&
            !await HasColumnsAsync(connection, "QuickNotes", "Id", "Title", "Content", "IsPinned", "SortOrder", "CreatedAt", "UpdatedAt"))
        {
            return null;
        }

        var hasClipboardHistory = tables.Contains("ClipboardHistory", StringComparer.OrdinalIgnoreCase);
        var hasTextSnippets = tables.Contains("TextSnippets", StringComparer.OrdinalIgnoreCase);
        if (hasClipboardHistory != hasTextSnippets)
        {
            return null;
        }

        if (hasClipboardHistory &&
            (!await HasColumnsAsync(connection, "ClipboardHistory", "Id", "Content", "ContentHash", "CreatedAt", "LastUsedAt", "UseCount") ||
             !await HasColumnsAsync(connection, "TextSnippets", "Id", "Title", "Content", "Category", "IsPinned", "SortOrder", "UseCount", "CreatedAt", "UpdatedAt", "LastUsedAt")))
        {
            return null;
        }

        if (!await HasColumnsAsync(connection, "Todos", "Priority"))
        {
            return "1.0";
        }

        if (!await HasColumnsAsync(connection, "Shortcuts", "LaunchArguments"))
        {
            return "1.1";
        }

        if (!hasQuickNotes)
        {
            return "1.3";
        }

        return hasClipboardHistory ? DatabaseVersion : "1.4";
    }

    private static async Task<List<string>> ReadTableNamesAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%'";
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<string>();
        while (await reader.ReadAsync())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<bool> HasSupportedSchemaObjectsAsync(
        SqliteConnection connection,
        IReadOnlySet<string> knownTables)
    {
        var command = connection.CreateCommand();
        command.CommandText =
            "SELECT type, name, sql FROM sqlite_master WHERE name NOT LIKE 'sqlite_%'";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var type = reader.GetString(0);
            var name = reader.GetString(1);
            if (string.Equals(type, "table", StringComparison.OrdinalIgnoreCase))
            {
                var sql = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                if (!knownTables.Contains(name) ||
                    sql.Contains("VIRTUAL TABLE", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else if (!string.Equals(type, "index", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    private static async Task<bool> HasColumnsAsync(
        SqliteConnection connection,
        string table,
        params string[] requiredColumns)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info(\"{table}\")";
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(1));
        }

        return requiredColumns.All(columns.Contains);
    }

    private async Task SetDatabaseVersionAsync(SqliteConnection connection, string version)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
            INSERT OR REPLACE INTO Settings (Key, Value) 
            VALUES ('DatabaseVersion', @version)";
        command.Parameters.AddWithValue("@version", version);
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateTablesAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            @"
            CREATE TABLE IF NOT EXISTS Notes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT,
                Color TEXT NOT NULL DEFAULT '#FFFFFF',
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )",
            "CREATE INDEX IF NOT EXISTS idx_notes_updated_at ON Notes(UpdatedAt DESC)",
            @"
            CREATE TABLE IF NOT EXISTS QuickNotes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL DEFAULT '',
                IsPinned INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )",
            "CREATE INDEX IF NOT EXISTS idx_quick_notes_order ON QuickNotes(IsPinned DESC, SortOrder ASC, UpdatedAt DESC)",
            @"
            CREATE TABLE IF NOT EXISTS ClipboardHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL,
                ContentHash TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL,
                LastUsedAt TEXT NOT NULL,
                UseCount INTEGER NOT NULL DEFAULT 1
            )",
            "CREATE INDEX IF NOT EXISTS idx_clipboard_history_last_used ON ClipboardHistory(LastUsedAt DESC)",
            @"
            CREATE TABLE IF NOT EXISTS TextSnippets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT '默认',
                IsPinned INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                UseCount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastUsedAt TEXT
            )",
            "CREATE INDEX IF NOT EXISTS idx_text_snippets_order ON TextSnippets(IsPinned DESC, SortOrder ASC, LastUsedAt DESC)",
            @"
            CREATE TABLE IF NOT EXISTS Todos (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                IsCompleted INTEGER NOT NULL DEFAULT 0,
                DueDate TEXT,
                CreatedAt TEXT NOT NULL,
                CompletedAt TEXT,
                Priority INTEGER NOT NULL DEFAULT 1
            )",
            "CREATE INDEX IF NOT EXISTS idx_todos_due_date ON Todos(DueDate)",
            "CREATE INDEX IF NOT EXISTS idx_todos_created_at ON Todos(CreatedAt)",
            @"
            CREATE TABLE IF NOT EXISTS Shortcuts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Path TEXT NOT NULL,
                Type TEXT NOT NULL DEFAULT 'Application',
                IconPath TEXT,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LaunchArguments TEXT
            )",
            @"
            CREATE TABLE IF NOT EXISTS Settings (
                Key TEXT PRIMARY KEY,
                Value TEXT
            )"
        };

        foreach (var sql in commands)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }

        await EnsureShortcutsTableAsync(connection);
        await InitializeDefaultSettingsAsync(connection);
    }

    private async Task InitializeDefaultSettingsAsync(SqliteConnection connection)
    {
        var recommendedSize = PanelSizePolicy.GetRecommendedSize(
            _monitorWorkAreas.GetForWindow(0).WorkArea);
        var defaultSettings = new Dictionary<string, string>
        {
            { "Theme", "System" },
            { "ColorScheme", "Taro" },
            { "FollowSystemTheme", "True" },
            { "ColorSchemeLight", "Taro" },
            { "ColorSchemeDark", "DarkGrey" },
            { "WindowOpacity", "0.70" },
            { "TopMost", "true" },
            { "Startup", "true" },
            { "AutoLocation", "true" },
            { "City", "" },
            { "PanelWidth", recommendedSize.Width.ToString(CultureInfo.InvariantCulture) },
            { "PanelHeight", recommendedSize.Height.ToString(CultureInfo.InvariantCulture) },
            { "WindowLocked", "false" },
            { "PanelCollapsed", "false" },
            { "WindowLeft", "" },
            { "WindowTop", "" },
            { "WidgetLayout", "" },
            { "Hotkey", "Ctrl+Alt+Space" },
            { ILocalizationService.LanguageSettingKey, _initialLanguage },
            { "WeatherApiKey", "" },
            { "WeatherApiHost", "" },
            { WeatherApiDefaults.DefaultApiKeySettingKey, WeatherApiDefaults.BuiltInApiKeyEncrypted },
            { WeatherApiDefaults.DefaultApiHostSettingKey, WeatherApiDefaults.BuiltInApiHostEncrypted },
            { "ShortcutMaxCount", "9" },
            { QuickTextService.HistoryEnabledSettingKey, "true" },
            { QuickTextService.SensitiveFilterSettingKey, "true" },
            { QuickTextService.HistoryMaxCountSettingKey, QuickTextService.DefaultHistoryLimit.ToString() },
            { DashboardModuleCatalog.SettingsKey, JsonSerializer.Serialize(DashboardModuleCatalog.CreateDefaultModules(), ModuleSettingsJsonOptions) }
        };

        foreach (var setting in defaultSettings)
        {
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR IGNORE INTO Settings (Key, Value) 
                VALUES (@key, @value)";
            command.Parameters.AddWithValue("@key", setting.Key);
            command.Parameters.AddWithValue("@value", setting.Value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private async Task MigrateDatabaseAsync(SqliteConnection connection, string fromVersion, string toVersion)
    {
        if (fromVersion == "1.0" && toVersion == "1.1")
        {
            await TryAddColumnAsync(connection, "Todos", "Priority", "INTEGER NOT NULL DEFAULT 1");
        }

        if (CompareDatabaseVersions(fromVersion, "1.2") < 0 &&
            CompareDatabaseVersions(toVersion, "1.2") >= 0)
        {
            await TryAddColumnAsync(connection, "Shortcuts", "LaunchArguments", "TEXT");
        }

        if (CompareDatabaseVersions(fromVersion, "1.3") < 0 &&
            CompareDatabaseVersions(toVersion, "1.3") >= 0)
        {
            await EnsureEncryptedWeatherDefaultsAsync(connection);
        }

        if (CompareDatabaseVersions(fromVersion, "1.4") < 0 &&
            CompareDatabaseVersions(toVersion, "1.4") >= 0)
        {
            await EnsureQuickNotesTableAsync(connection);
        }

        if (CompareDatabaseVersions(fromVersion, "1.5") < 0 &&
            CompareDatabaseVersions(toVersion, "1.5") >= 0)
        {
            await EnsureQuickTextTablesAsync(connection);
            await EnsureQuickTextSettingsAsync(connection);
        }
    }

    private async Task EnsureSchemaUpdatesAsync(SqliteConnection connection)
    {
        await TryAddColumnAsync(connection, "Todos", "Priority", "INTEGER NOT NULL DEFAULT 1");
        await EnsureShortcutsTableAsync(connection);
        await EnsureQuickNotesTableAsync(connection);
        await EnsureQuickTextTablesAsync(connection);
        await EnsureQuickTextSettingsAsync(connection);
        await EnsureEncryptedWeatherDefaultsAsync(connection);
    }

    private static async Task EnsureShortcutsTableAsync(SqliteConnection connection)
    {
        var create = connection.CreateCommand();
        create.CommandText = @"
            CREATE TABLE IF NOT EXISTS Shortcuts (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Name TEXT NOT NULL,
                Path TEXT NOT NULL,
                Type TEXT NOT NULL DEFAULT 'Application',
                IconPath TEXT,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                LaunchArguments TEXT
            )";
        await create.ExecuteNonQueryAsync();

        await TryAddColumnAsync(connection, "Shortcuts", "SortOrder", "INTEGER NOT NULL DEFAULT 0");
        await TryAddColumnAsync(connection, "Shortcuts", "LaunchArguments", "TEXT");

        var index = connection.CreateCommand();
        index.CommandText = "CREATE INDEX IF NOT EXISTS idx_shortcuts_sort_order ON Shortcuts(SortOrder)";
        await index.ExecuteNonQueryAsync();
    }

    private static async Task EnsureQuickNotesTableAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            @"
            CREATE TABLE IF NOT EXISTS QuickNotes (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL DEFAULT '',
                IsPinned INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            )",
            "CREATE INDEX IF NOT EXISTS idx_quick_notes_order ON QuickNotes(IsPinned DESC, SortOrder ASC, UpdatedAt DESC)"
        };

        foreach (var sql in commands)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureEncryptedWeatherDefaultsAsync(SqliteConnection connection)
    {
        var defaults = new Dictionary<string, string>
        {
            { WeatherApiDefaults.DefaultApiKeySettingKey, WeatherApiDefaults.BuiltInApiKeyEncrypted },
            { WeatherApiDefaults.DefaultApiHostSettingKey, WeatherApiDefaults.BuiltInApiHostEncrypted }
        };

        foreach (var setting in defaults)
        {
            var insertCommand = connection.CreateCommand();
            insertCommand.CommandText = @"
                INSERT OR IGNORE INTO Settings (Key, Value)
                VALUES (@key, @value)";
            insertCommand.Parameters.AddWithValue("@key", setting.Key);
            insertCommand.Parameters.AddWithValue("@value", setting.Value);
            await insertCommand.ExecuteNonQueryAsync();

            var updateCommand = connection.CreateCommand();
            updateCommand.CommandText = @"
                UPDATE Settings
                SET Value = @value
                WHERE Key = @key AND (Value IS NULL OR trim(Value) = '')";
            updateCommand.Parameters.AddWithValue("@key", setting.Key);
            updateCommand.Parameters.AddWithValue("@value", setting.Value);
            await updateCommand.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureQuickTextTablesAsync(SqliteConnection connection)
    {
        var commands = new[]
        {
            @"
            CREATE TABLE IF NOT EXISTS ClipboardHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Content TEXT NOT NULL,
                ContentHash TEXT NOT NULL UNIQUE,
                CreatedAt TEXT NOT NULL,
                LastUsedAt TEXT NOT NULL,
                UseCount INTEGER NOT NULL DEFAULT 1
            )",
            "CREATE INDEX IF NOT EXISTS idx_clipboard_history_last_used ON ClipboardHistory(LastUsedAt DESC)",
            @"
            CREATE TABLE IF NOT EXISTS TextSnippets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                Content TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT '默认',
                IsPinned INTEGER NOT NULL DEFAULT 0,
                SortOrder INTEGER NOT NULL DEFAULT 0,
                UseCount INTEGER NOT NULL DEFAULT 0,
                CreatedAt TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL,
                LastUsedAt TEXT
            )",
            "CREATE INDEX IF NOT EXISTS idx_text_snippets_order ON TextSnippets(IsPinned DESC, SortOrder ASC, LastUsedAt DESC)"
        };

        foreach (var sql in commands)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task EnsureQuickTextSettingsAsync(SqliteConnection connection)
    {
        var defaults = new Dictionary<string, string>
        {
            { QuickTextService.HistoryEnabledSettingKey, "true" },
            { QuickTextService.SensitiveFilterSettingKey, "true" },
            { QuickTextService.HistoryMaxCountSettingKey, QuickTextService.DefaultHistoryLimit.ToString() }
        };

        foreach (var setting in defaults)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR IGNORE INTO Settings (Key, Value) VALUES (@key, @value)";
            command.Parameters.AddWithValue("@key", setting.Key);
            command.Parameters.AddWithValue("@value", setting.Value);
            await command.ExecuteNonQueryAsync();
        }
    }

    private static async Task TryAddColumnAsync(SqliteConnection connection, string table, string column, string definition)
    {
        var check = connection.CreateCommand();
        check.CommandText = $"PRAGMA table_info({table})";
        var hasColumn = false;
        var tableExists = false;
        await using (var reader = await check.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tableExists = true;
                if (string.Equals(reader.GetString(1), column, StringComparison.OrdinalIgnoreCase))
                {
                    hasColumn = true;
                    break;
                }
            }
        }

        if (!tableExists || hasColumn) return;

        var alter = connection.CreateCommand();
        alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
        await alter.ExecuteNonQueryAsync();
    }

    public Task<T> ExecuteInTransactionAsync<T>(Func<IDatabaseSession, Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        return Task.Run(() => ExecuteInTransactionCoreAsync(operation));
    }

    private async Task<T> ExecuteInTransactionCoreAsync<T>(Func<IDatabaseSession, Task<T>> operation)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();
        using var transaction = connection.BeginTransaction();
        var session = new DatabaseSession(connection, transaction);

        try
        {
            var result = await operation(session);
            transaction.Commit();
            return result;
        }
        catch (Exception originalException)
        {
            try
            {
                transaction.Rollback();
            }
            catch (Exception rollbackException)
            {
                Logger.LogError(
                    rollbackException,
                    $"DatabaseService.Transaction.Rollback after {originalException.GetType().Name}");
            }

            throw;
        }
    }

    private static int CompareDatabaseVersions(string left, string right)
    {
        if (!Version.TryParse(left, out var leftVersion))
        {
            throw new InvalidDataException($"数据库版本格式无效：{left}。");
        }

        if (!Version.TryParse(right, out var rightVersion))
        {
            throw new InvalidDataException($"数据库版本格式无效：{right}。");
        }

        return leftVersion.CompareTo(rightVersion);
    }

    public Task<int> ExecuteNonQueryAsync(string sql, params object?[] parameters) =>
        Task.Run(() => ExecuteNonQueryCoreAsync(sql, parameters));

    private async Task<int> ExecuteNonQueryCoreAsync(string sql, object?[] parameters)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        for (int i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        return await command.ExecuteNonQueryAsync();
    }

    public Task<List<T>> QueryAsync<T>(string sql, Func<SqliteDataReader, T> map, params object?[] parameters) =>
        Task.Run(() => QueryCoreAsync(sql, map, parameters));

    private async Task<List<T>> QueryCoreAsync<T>(string sql, Func<SqliteDataReader, T> map, object?[] parameters)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        for (int i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        var results = new List<T>();
        using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            results.Add(map(reader));
        }

        return results;
    }

    public Task<T?> QuerySingleAsync<T>(string sql, Func<SqliteDataReader, T> map, params object?[] parameters) =>
        Task.Run(() => QuerySingleCoreAsync(sql, map, parameters));

    private async Task<T?> QuerySingleCoreAsync<T>(string sql, Func<SqliteDataReader, T> map, object?[] parameters)
    {
        using var connection = new SqliteConnection(_connectionString);
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = sql;

        for (int i = 0; i < parameters.Length; i++)
        {
            command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
        }

        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return map(reader);
        }

        return default;
    }
}
