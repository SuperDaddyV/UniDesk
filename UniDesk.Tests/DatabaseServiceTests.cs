using Xunit;
using UniDesk.Services;
using UniDesk.Helpers;
using Microsoft.Data.Sqlite;
using UniDesk.Models;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class DatabaseServiceTests
{
    private string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_db.db");

    private DatabaseService GetService()
    {
        return new DatabaseService(
            $"Data Source={_testDbFile}",
            monitorWorkAreas: new FixedMonitorWorkAreaProvider(1366, 768));
    }

    [Fact]
    public void StartupAndDatabaseConstructor_ShouldKeepDirectoryMigrationOffCallerThread()
    {
        var projectRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory,
            "..",
            "..",
            "..",
            ".."));
        var appSource = File.ReadAllText(Path.Combine(projectRoot, "UniDesk", "App.xaml.cs"));
        var databaseSource = File.ReadAllText(Path.Combine(
            projectRoot,
            "UniDesk",
            "Services",
            "DatabaseService.cs"));
        var constructorStart = databaseSource.IndexOf("public DatabaseService(", StringComparison.Ordinal);
        var initializeStart = databaseSource.IndexOf("public Task InitializeAsync", constructorStart, StringComparison.Ordinal);
        var constructorBody = databaseSource[constructorStart..initializeStart];

        Assert.Contains(
            "await Task.Run(DirectoryHelper.EnsureDirectoriesExist)",
            appSource,
            StringComparison.Ordinal);
        Assert.DoesNotContain("DirectoryHelper.EnsureDirectoriesExist", constructorBody, StringComparison.Ordinal);
        Assert.Contains(
            "DirectoryHelper.EnsureDirectoriesExist();",
            databaseSource[initializeStart..],
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateDatabaseFile()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        Assert.True(System.IO.File.Exists(_testDbFile));
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateSettingsTable()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Settings'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateNotesTable()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Notes'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateTodosTable()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Todos'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateQuickNotesTable()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='QuickNotes'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateQuickTextTables()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var clipboardHistory = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='ClipboardHistory'",
            reader => reader.GetInt32(0)
        );
        var snippets = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='TextSnippets'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, clipboardHistory);
        Assert.Equal(1, snippets);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateShortcutsTable()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Shortcuts'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateNotesIndex()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_notes_updated_at'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateTodosIndexes()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var dueDateIndex = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_todos_due_date'",
            reader => reader.GetInt32(0)
        );

        var createdAtIndex = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_todos_created_at'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, dueDateIndex);
        Assert.Equal(1, createdAtIndex);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateShortcutsIndex()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_shortcuts_sort_order'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateQuickNotesIndex()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_quick_notes_order'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, result);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldAddSortOrderToExistingShortcutsTable()
    {
        Cleanup();

        await using (var connection = new SqliteConnection($"Data Source={_testDbFile}"))
        {
            await connection.OpenAsync();

            var createSettings = connection.CreateCommand();
            createSettings.CommandText = "CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT)";
            await createSettings.ExecuteNonQueryAsync();

            var version = connection.CreateCommand();
            version.CommandText = "INSERT INTO Settings (Key, Value) VALUES ('DatabaseVersion', '1.5')";
            await version.ExecuteNonQueryAsync();

            var createShortcuts = connection.CreateCommand();
            createShortcuts.CommandText = @"
                CREATE TABLE Shortcuts (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    Path TEXT NOT NULL,
                    Type TEXT NOT NULL DEFAULT 'Application',
                    IconPath TEXT,
                    CreatedAt TEXT NOT NULL
                )";
            await createShortcuts.ExecuteNonQueryAsync();

            var insertShortcut = connection.CreateCommand();
            insertShortcut.CommandText = "INSERT INTO Shortcuts (Name, Path, Type, CreatedAt) VALUES ('App', 'path', 'Application', @createdAt)";
            insertShortcut.Parameters.AddWithValue("@createdAt", DateTime.UtcNow.ToString("o"));
            await insertShortcut.ExecuteNonQueryAsync();
        }

        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var columns = await databaseService.QueryAsync(
            "PRAGMA table_info(Shortcuts)",
            reader => reader.GetString(1)
        );
        var sortOrder = await databaseService.QuerySingleAsync<int>(
            "SELECT SortOrder FROM Shortcuts WHERE Name = 'App'",
            reader => reader.GetInt32(0)
        );

        Assert.Contains("SortOrder", columns);
        Assert.Contains("LaunchArguments", columns);
        Assert.Equal(0, sortOrder);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldCreateQuickTextIndexes()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var clipboardIndex = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_clipboard_history_last_used'",
            reader => reader.GetInt32(0)
        );
        var snippetIndex = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM sqlite_master WHERE type='index' AND name='idx_text_snippets_order'",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, clipboardIndex);
        Assert.Equal(1, snippetIndex);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeDefaultSettings()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var theme = await databaseService.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'Theme'",
            reader => reader.GetString(0)
        );

        Assert.Equal("System", theme);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPersistMonitorAwarePanelRecommendation()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var values = await databaseService.QueryAsync<string>(
            "SELECT Value FROM Settings WHERE Key IN ('PanelHeight', 'PanelWidth') ORDER BY Key",
            reader => reader.GetString(0));

        Assert.Equal(["560", "340"], values);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPreserveExistingPanelSizes()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            "UPDATE Settings SET Value = CASE Key WHEN 'PanelWidth' THEN '480' ELSE '900' END WHERE Key IN ('PanelWidth', 'PanelHeight')");

        await databaseService.InitializeAsync();

        var values = await databaseService.QueryAsync<string>(
            "SELECT Value FROM Settings WHERE Key IN ('PanelHeight', 'PanelWidth') ORDER BY Key",
            reader => reader.GetString(0));

        Assert.Equal(["900", "480"], values);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldInitializeAllDefaultSettings()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var settings = await databaseService.QueryAsync<string>(
            "SELECT Key FROM Settings ORDER BY Key",
            reader => reader.GetString(0)
        );

        var expectedKeys = new[]
        {
            "AutoLocation",
            "City",
            "ColorScheme",
            "ColorSchemeDark",
            "ColorSchemeLight",
            "ClipboardHistoryEnabled",
            "ClipboardHistoryMaxCount",
            "ClipboardSensitiveFilterEnabled",
            "DatabaseVersion",
            "DefaultWeatherApiHostEnc",
            "DefaultWeatherApiKeyEnc",
            "FollowSystemTheme",
            "Hotkey",
            "Language",
            "ModuleSettings",
            "PanelHeight",
            "PanelCollapsed",
            "PanelWidth",
            "ShortcutMaxCount",
            "Startup",
            "Theme",
            "TopMost",
            "WeatherApiHost",
            "WeatherApiKey",
            "WidgetLayout",
            "WindowLeft",
            "WindowLocked",
            "WindowOpacity",
            "WindowTop"
        };
        Assert.Equal(expectedKeys.Length, settings.Count);
        
        foreach (var key in expectedKeys)
        {
            Assert.Contains(key, settings);
        }
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldEnableStartupAndAutoLocationForNewDatabase()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var startup = await databaseService.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'Startup'",
            reader => reader.GetString(0));
        var autoLocation = await databaseService.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'AutoLocation'",
            reader => reader.GetString(0));

        Assert.Equal("true", startup);
        Assert.Equal("true", autoLocation);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldUseResolvedInitialLanguageForNewDatabase()
    {
        var databaseService = new DatabaseService(
            $"Data Source={_testDbFile}",
            initialLanguage: "ja-JP");
        await databaseService.InitializeAsync();

        var language = await databaseService.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'Language'",
            reader => reader.GetString(0));

        Assert.Equal("ja-JP", language);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPreserveExistingLanguageDuringUpgradeInitialization()
    {
        var firstRun = new DatabaseService(
            $"Data Source={_testDbFile}",
            initialLanguage: "en-US");
        await firstRun.InitializeAsync();
        await firstRun.ExecuteNonQueryAsync(
            "UPDATE Settings SET Value = @p0 WHERE Key = 'Language'",
            "es-ES");

        var laterRun = new DatabaseService(
            $"Data Source={_testDbFile}",
            initialLanguage: "ja-JP");
        await laterRun.InitializeAsync();

        var language = await laterRun.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'Language'",
            reader => reader.GetString(0));

        Assert.Equal("es-ES", language);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldPreserveExistingStartupAndAutoLocationChoices()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            "UPDATE Settings SET Value = @p0 WHERE Key IN ('Startup', 'AutoLocation')",
            "false");

        await databaseService.InitializeAsync();

        var values = await databaseService.QueryAsync<string>(
            "SELECT Value FROM Settings WHERE Key IN ('Startup', 'AutoLocation') ORDER BY Key",
            reader => reader.GetString(0));
        Assert.Equal(["false", "false"], values);

        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldSetDatabaseVersion()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var version = await databaseService.QuerySingleAsync<string>(
            "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'",
            reader => reader.GetString(0)
        );

        Assert.Equal("1.5", version);
        
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ShouldNotRecreateTablesOnSecondCall()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        
        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Test Note", "Test Content", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        await databaseService.InitializeAsync();

        var count = await databaseService.QuerySingleAsync<int>(
            "SELECT COUNT(*) FROM Notes",
            reader => reader.GetInt32(0)
        );

        Assert.Equal(1, count);
        
        Cleanup();
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_ShouldInsertRecord()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var result = await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Test Note", "Test Content", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_ShouldUpdateRecord()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Test Note", "Test Content", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        var result = await databaseService.ExecuteNonQueryAsync(
            "UPDATE Notes SET Title = @p0 WHERE Title = @p1",
            "Updated Note", "Test Note"
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task ExecuteNonQueryAsync_ShouldDeleteRecord()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Test Note", "Test Content", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        var result = await databaseService.ExecuteNonQueryAsync(
            "DELETE FROM Notes WHERE Title = @p0",
            "Test Note"
        );

        Assert.Equal(1, result);
        
        Cleanup();
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnMultipleRecords()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Note 1", "Content 1", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Note 2", "Content 2", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        var notes = await databaseService.QueryAsync<string>(
            "SELECT Title FROM Notes ORDER BY Title",
            reader => reader.GetString(0)
        );

        Assert.Equal(2, notes.Count);
        Assert.Equal("Note 1", notes[0]);
        Assert.Equal("Note 2", notes[1]);
        
        Cleanup();
    }

    [Fact]
    public async Task QueryAsync_ShouldReturnEmptyListWhenNoRecords()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var notes = await databaseService.QueryAsync<string>(
            "SELECT Title FROM Notes",
            reader => reader.GetString(0)
        );

        Assert.Empty(notes);
        
        Cleanup();
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldReturnSingleRecord()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Notes (Title, Content, Color, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "Test Note", "Test Content", "#FFFFFF", DateTime.UtcNow.ToString("o"), DateTime.UtcNow.ToString("o")
        );

        var title = await databaseService.QuerySingleAsync<string>(
            "SELECT Title FROM Notes WHERE Title = @p0",
            reader => reader.GetString(0),
            "Test Note"
        );

        Assert.Equal("Test Note", title);
        
        Cleanup();
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldReturnNullWhenNoRecord()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var title = await databaseService.QuerySingleAsync<string>(
            "SELECT Title FROM Notes WHERE Title = @p0",
            reader => reader.GetString(0),
            "NonExistent"
        );

        Assert.Null(title);
        
        Cleanup();
    }

    [Fact]
    public async Task QuerySingleAsync_ShouldHandleComplexObject()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await databaseService.ExecuteNonQueryAsync(
            "INSERT INTO Todos (Title, IsCompleted, DueDate, CreatedAt) VALUES (@p0, @p1, @p2, @p3)",
            "Test Todo", 1, "2026-12-31", DateTime.UtcNow.ToString("o")
        );

        var todo = await databaseService.QuerySingleAsync<(string Title, bool IsCompleted)>(
            "SELECT Title, IsCompleted FROM Todos WHERE Title = @p0",
            reader => (reader.GetString(0), reader.GetInt32(1) == 1),
            "Test Todo"
        );

        Assert.Equal("Test Todo", todo.Title);
        Assert.True(todo.IsCompleted);
        
        Cleanup();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WhenOperationThrows_ShouldRollback()
    {
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            databaseService.ExecuteInTransactionAsync<int>(async session =>
            {
                await session.ExecuteNonQueryAsync(
                    "INSERT INTO Todos (Title, IsCompleted, CreatedAt) VALUES (@p0, @p1, @p2)",
                    "Should Roll Back",
                    0,
                    DateTime.UtcNow.ToString("o"));
                throw new InvalidOperationException("force rollback");
            }));

        var count = await databaseService.QuerySingleAsync(
            "SELECT COUNT(*) FROM Todos WHERE Title = @p0",
            reader => reader.GetInt32(0),
            "Should Roll Back");

        Assert.Equal(0, count);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_InvalidPath_ShouldThrow()
    {
        Cleanup();
        var missingDirectory = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            $"missing-{Guid.NewGuid():N}",
            "test.db");
        var databaseService = new DatabaseService($"Data Source={missingDirectory}");

        await Assert.ThrowsAsync<SqliteException>(() => databaseService.InitializeAsync());
    }

    [Fact]
    public async Task InitializeAsync_ShouldEnableWalMode()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var mode = await databaseService.QuerySingleAsync(
            "PRAGMA journal_mode",
            reader => reader.GetString(0));

        Assert.Equal("wal", mode, ignoreCase: true);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_FutureDatabaseVersion_ShouldThrowAndPreserveVersion()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            "UPDATE Settings SET Value = @p0 WHERE Key = 'DatabaseVersion'",
            "1.10");

        await Assert.ThrowsAsync<InvalidDataException>(() => databaseService.InitializeAsync());

        var version = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'",
            reader => reader.GetString(0));
        Assert.Equal("1.10", version);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_ExistingSchemaWithoutDatabaseVersion_ShouldFailClosed()
    {
        Cleanup();
        await using (var connection = new SqliteConnection($"Data Source={_testDbFile}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT); CREATE TABLE Notes (Id INTEGER PRIMARY KEY);";
            await command.ExecuteNonQueryAsync();
        }

        var databaseService = GetService();
        var exception = await Assert.ThrowsAsync<InvalidDataException>(
            () => databaseService.InitializeAsync());

        Assert.Contains("DatabaseVersion", exception.Message, StringComparison.Ordinal);
        var versionCount = await databaseService.QuerySingleAsync(
            "SELECT COUNT(*) FROM Settings WHERE Key = 'DatabaseVersion'",
            reader => reader.GetInt32(0));
        Assert.Equal(0, versionCount);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_CompleteKnownSchemaWithoutDatabaseVersion_ShouldInferAndStampCurrentVersion()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            "DELETE FROM Settings WHERE Key = 'DatabaseVersion'");

        await databaseService.InitializeAsync();

        var version = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'",
            reader => reader.GetString(0));
        Assert.Equal("1.5", version);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_MissingDatabaseVersionWithTrigger_ShouldFailClosed()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();
        await databaseService.ExecuteNonQueryAsync(
            """
            DELETE FROM Settings WHERE Key = 'DatabaseVersion';
            CREATE TRIGGER unexpected_restore_trigger
            AFTER INSERT ON Notes
            BEGIN
                SELECT 1;
            END;
            """);

        await Assert.ThrowsAsync<InvalidDataException>(
            () => databaseService.InitializeAsync());

        var versionCount = await databaseService.QuerySingleAsync(
            "SELECT COUNT(*) FROM Settings WHERE Key = 'DatabaseVersion'",
            reader => reader.GetInt32(0));
        Assert.Equal(0, versionCount);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_NewDatabase_ShouldUseDashboardModuleCatalogDefaults()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var json = await databaseService.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'ModuleSettings'",
            reader => reader.GetString(0));
        var modules = System.Text.Json.JsonSerializer.Deserialize<List<ModuleSetting>>(
            json!,
            new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web));

        Assert.NotNull(modules);
        Assert.Equal(
            DashboardModuleCatalog.CreateDefaultModules().Select(module => module.ModuleId),
            modules!.Select(module => module.ModuleId));
        Assert.Equal(
            DashboardModuleCatalog.CreateDefaultModules().Select(module => module.IsEnabled),
            modules.Select(module => module.IsEnabled));
        Cleanup();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ShouldReturnBeforeSynchronousDatabaseCallbackCompletes()
    {
        Cleanup();
        var databaseService = GetService();
        await databaseService.InitializeAsync();

        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackRelease = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocationReturned = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var invocation = Task.Run(async () =>
        {
            var operation = databaseService.ExecuteInTransactionAsync<int>(_ =>
            {
                callbackStarted.SetResult();
                callbackRelease.Task.GetAwaiter().GetResult();
                return Task.FromResult(1);
            });
            invocationReturned.SetResult();
            return await operation;
        });

        await invocationReturned.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await callbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
        callbackRelease.SetResult();
        Assert.Equal(1, await invocation);
        Cleanup();
    }

    [Fact]
    public async Task InitializeAsync_MigrationFailure_ShouldRollbackEarlierMigrationWrites()
    {
        Cleanup();
        await using (var connection = new SqliteConnection($"Data Source={_testDbFile}"))
        {
            await connection.OpenAsync();
            var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE Settings (Key TEXT PRIMARY KEY, Value TEXT);
                INSERT INTO Settings (Key, Value) VALUES ('DatabaseVersion', '1.2');
                CREATE VIEW QuickNotes AS SELECT 1 AS Id;
                """;
            await command.ExecuteNonQueryAsync();
        }

        var databaseService = GetService();
        await Assert.ThrowsAsync<SqliteException>(() => databaseService.InitializeAsync());

        await using (var verifyConnection = new SqliteConnection($"Data Source={_testDbFile}"))
        {
            await verifyConnection.OpenAsync();
            var verifyCommand = verifyConnection.CreateCommand();
            verifyCommand.CommandText =
                "SELECT COUNT(*) FROM Settings WHERE Key IN ('DefaultWeatherApiKeyEnc', 'DefaultWeatherApiHostEnc')";
            var insertedDefaultCount = Convert.ToInt32(await verifyCommand.ExecuteScalarAsync());
            Assert.Equal(0, insertedDefaultCount);

            verifyCommand.CommandText = "SELECT Value FROM Settings WHERE Key = 'DatabaseVersion'";
            Assert.Equal("1.2", await verifyCommand.ExecuteScalarAsync());
        }

        Cleanup();
    }

    private void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (System.IO.File.Exists(_testDbFile))
            {
                System.IO.File.Delete(_testDbFile);
            }
        }
        catch
        {
        }
    }

    private sealed class FixedMonitorWorkAreaProvider(double width, double height) : IMonitorWorkAreaProvider
    {
        private readonly MonitorWorkArea _monitor = new(
            Handle: 1,
            PixelWorkArea: new PixelRect(0, 0, width, height),
            WorkArea: new LogicalRect(0, 0, width, height),
            DpiX: 96,
            DpiY: 96,
            IsPrimary: true);

        public IReadOnlyList<MonitorWorkArea> GetAll() => [_monitor];

        public MonitorWorkArea GetForWindow(nint windowHandle) => _monitor;

        public MonitorWorkArea GetForPixelRect(PixelRect pixelBounds) => _monitor;

        public MonitorWorkArea GetForPixelPoint(PixelPoint pixelPoint) => _monitor;
    }
}
