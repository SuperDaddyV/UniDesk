using UniDesk.Models;
using UniDesk.Services;
using System.Text.Json;
using Xunit;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class TodoBackupServiceTests
{
    private readonly string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_backup.db");
    private readonly string _backupFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_backup.json");

    [Fact]
    public async Task ExportAndImport_ShouldIncludeQuickNotes()
    {
        var (db, todoService, quickNoteService, quickTextService, shortcutService, settingsService, backupService) = await InitAsync();

        settingsService.SetValue("PanelWidth", "480");
        settingsService.SetValue("ModuleSettings", "[{\"moduleId\":\"QuickText\",\"displayName\":\"快捷文本\",\"isEnabled\":true,\"sortOrder\":0}]");
        await settingsService.FlushPendingSavesAsync();
        await shortcutService.CreateShortcutAsync(new ShortcutItem
        {
            Name = "文件夹",
            Path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Type = ShortcutType.Folder,
            SortOrder = 0
        });
        await todoService.CreateTodoAsync(new TodoItem { Title = "待办" });
        await quickNoteService.CreateQuickNoteAsync(new QuickNote
        {
            Title = "便签",
            Content = "便签内容",
            IsPinned = true
        });
        await quickTextService.RecordClipboardTextAsync("剪贴板历史");
        await quickTextService.CreateTextSnippetAsync(new TextSnippet
        {
            Title = "短语",
            Content = "常用短语"
        });

        await backupService.ExportToFileAsync(
            _backupFile,
            new BackupExportOptions(IncludeClipboardHistory: true));
        settingsService.SetValue("PanelWidth", "320");
        settingsService.SetValue("ModuleSettings", "");
        await settingsService.FlushPendingSavesAsync();
        await db.ExecuteNonQueryAsync("DELETE FROM Shortcuts");
        await db.ExecuteNonQueryAsync("DELETE FROM Todos");
        await db.ExecuteNonQueryAsync("DELETE FROM QuickNotes");
        await db.ExecuteNonQueryAsync("DELETE FROM ClipboardHistory");
        await db.ExecuteNonQueryAsync("DELETE FROM TextSnippets");

        var result = await ImportAsync(backupService, _backupFile);

        Assert.True(result.SettingCount > 0);
        Assert.Equal(1, result.ShortcutCount);
        Assert.Equal(1, result.TodoCount);
        Assert.Equal(1, result.QuickNoteCount);
        Assert.Equal(1, result.ClipboardHistoryCount);
        Assert.Equal(1, result.TextSnippetCount);
        Assert.Equal("480", settingsService.GetValue("PanelWidth", ""));
        Assert.Contains("QuickText", settingsService.GetValue("ModuleSettings", ""));
        Assert.Single(await shortcutService.GetAllShortcutsAsync());
        Assert.Single(await todoService.GetAllTodosAsync());
        var notes = await quickNoteService.GetAllQuickNotesAsync();
        Assert.Single(notes);
        Assert.Equal("便签", notes[0].Title);
        Assert.Single(await quickTextService.GetClipboardHistoryAsync());
        Assert.Single(await quickTextService.GetTextSnippetsAsync());

        Cleanup();
    }

    [Fact]
    public async Task ImportFromFileAsync_ShouldAcceptOldTodoOnlyBackup()
    {
        var (_, todoService, quickNoteService, quickTextService, shortcutService, _, backupService) = await InitAsync();
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 1,
              "exportedAt": "2026-06-13T00:00:00Z",
              "todos": [
                {
                  "title": "旧备份待办",
                  "isCompleted": false,
                  "priority": 1,
                  "createdAt": "2026-06-13T00:00:00Z"
                }
              ]
            }
            """);

        var result = await ImportAsync(backupService, _backupFile);

        Assert.Equal(0, result.SettingCount);
        Assert.Equal(0, result.ShortcutCount);
        Assert.Equal(1, result.TodoCount);
        Assert.Equal(0, result.QuickNoteCount);
        Assert.Equal(0, result.ClipboardHistoryCount);
        Assert.Equal(0, result.TextSnippetCount);
        Assert.Empty(await shortcutService.GetAllShortcutsAsync());
        Assert.Single(await todoService.GetAllTodosAsync());
        Assert.Empty(await quickNoteService.GetAllQuickNotesAsync());
        Assert.Empty(await quickTextService.GetClipboardHistoryAsync());
        Assert.Empty(await quickTextService.GetTextSnippetsAsync());

        Cleanup();
    }

    [Fact]
    public async Task ExportToFileAsync_Default_ShouldExcludeWeatherCredentialsAndClipboardHistory()
    {
        var (_, _, _, quickTextService, _, settingsService, backupService) = await InitAsync();
        await settingsService.SetSettingAsync("WeatherApiKey", "weather-secret");
        await settingsService.SetSettingAsync("WeatherApiHost", "abc.def.qweatherapi.com");
        await quickTextService.RecordClipboardTextAsync("portable clipboard");

        await backupService.ExportToFileAsync(_backupFile);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_backupFile));
        var root = document.RootElement;
        Assert.Equal(5, root.GetProperty("version").GetInt32());
        Assert.False(root.GetProperty("settings").TryGetProperty("WeatherApiKey", out _));
        Assert.False(root.GetProperty("settings").TryGetProperty("WeatherApiHost", out _));
        Assert.False(root.TryGetProperty("clipboardHistory", out _));
        Assert.False(root.GetProperty("containsSensitivePlaintext").GetBoolean());
        Assert.DoesNotContain(
            root.GetProperty("includedSections").EnumerateArray().Select(item => item.GetString()),
            section => section == "clipboardHistory");
        Cleanup();
    }

    [Fact]
    public async Task ExportToFileAsync_WithClipboardHistory_ShouldDeclareAndWritePortablePlainText()
    {
        var (_, _, _, quickTextService, _, _, backupService) = await InitAsync();
        await quickTextService.RecordClipboardTextAsync("portable clipboard");

        await backupService.ExportToFileAsync(
            _backupFile,
            new BackupExportOptions(IncludeClipboardHistory: true));

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_backupFile));
        var root = document.RootElement;
        Assert.True(root.GetProperty("containsSensitivePlaintext").GetBoolean());
        Assert.Equal(
            "portable clipboard",
            root.GetProperty("clipboardHistory")[0].GetProperty("content").GetString());
        Assert.Contains(
            root.GetProperty("includedSections").EnumerateArray().Select(item => item.GetString()),
            section => section == "clipboardHistory");
        Cleanup();
    }

    [Fact]
    public async Task ImportFromFileAsync_InvalidTodo_ShouldPreserveExistingData()
    {
        var (_, todoService, _, _, _, _, backupService) = await InitAsync();
        await todoService.CreateTodoAsync(new TodoItem { Title = "保留的待办" });
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 4,
              "exportedAt": "2026-07-10T00:00:00Z",
              "todos": [
                {
                  "title": "",
                  "isCompleted": false,
                  "priority": 1,
                  "createdAt": "2026-07-10T00:00:00Z"
                }
              ]
            }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() => backupService.PrepareImportAsync(_backupFile));

        var todos = await todoService.GetAllTodosAsync();
        Assert.Single(todos);
        Assert.Equal("保留的待办", todos[0].Title);
        Cleanup();
    }

    [Fact]
    public async Task ImportFromFileAsync_FutureVersion_ShouldPreserveExistingData()
    {
        var (_, todoService, _, _, _, _, backupService) = await InitAsync();
        await todoService.CreateTodoAsync(new TodoItem { Title = "保留的待办" });
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 6,
              "exportedAt": "2026-07-10T00:00:00Z",
              "todos": [
                {
                  "title": "未来版本待办",
                  "isCompleted": false,
                  "priority": 1,
                  "createdAt": "2026-07-10T00:00:00Z"
                }
              ]
            }
            """);

        await Assert.ThrowsAsync<InvalidDataException>(() => backupService.PrepareImportAsync(_backupFile));

        var todos = await todoService.GetAllTodosAsync();
        Assert.Single(todos);
        Assert.Equal("保留的待办", todos[0].Title);
        Cleanup();
    }

    [Fact]
    public async Task ImportFromFileAsync_InsertFailure_ShouldRollbackSettingsAndTodos()
    {
        var (db, todoService, _, _, _, settingsService, backupService) = await InitAsync();
        await settingsService.SetSettingAsync("PanelWidth", "320");
        await todoService.CreateTodoAsync(new TodoItem { Title = "保留的待办" });
        await db.ExecuteNonQueryAsync(
            """
            CREATE TRIGGER fail_todo_restore
            BEFORE INSERT ON Todos
            WHEN NEW.Title = '强制失败'
            BEGIN
                SELECT RAISE(ABORT, 'forced restore failure');
            END
            """);
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 4,
              "exportedAt": "2026-07-10T00:00:00Z",
              "settings": {
                "PanelWidth": "480"
              },
              "todos": [
                {
                  "title": "强制失败",
                  "isCompleted": false,
                  "priority": 1,
                  "createdAt": "2026-07-10T00:00:00Z"
                }
              ]
            }
            """);

        var plan = await backupService.PrepareImportAsync(_backupFile);
        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => backupService.ApplyImportAsync(plan));

        var todos = await todoService.GetAllTodosAsync();
        Assert.Single(todos);
        Assert.Equal("保留的待办", todos[0].Title);
        Assert.Equal("320", await settingsService.GetSettingAsync("PanelWidth"));
        Cleanup();
    }

    [Fact]
    public async Task PrepareImportAsync_ShouldNotMutateAndShouldPreviewAllShortcuts()
    {
        var (_, todoService, _, _, _, _, backupService) = await InitAsync();
        await todoService.CreateTodoAsync(new TodoItem { Title = "保留的待办" });
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 5,
              "exportedAt": "2026-07-10T00:00:00Z",
              "containsSensitivePlaintext": true,
              "settings": { "PanelWidth": "480" },
              "shortcuts": [
                {
                  "name": "安全文件夹",
                  "path": "C:\\Users\\Public",
                  "type": 1,
                  "sortOrder": 0
                },
                {
                  "name": "带参数程序",
                  "path": "C:\\Tools\\runner.exe",
                  "launchArguments": "--danger",
                  "type": 0,
                  "sortOrder": 1
                }
              ],
              "todos": [
                {
                  "title": "待导入待办",
                  "isCompleted": false,
                  "priority": 1,
                  "createdAt": "2026-07-10T00:00:00Z"
                }
              ]
            }
            """);

        var plan = await backupService.PrepareImportAsync(_backupFile);

        var existingTodos = await todoService.GetAllTodosAsync();
        Assert.Single(existingTodos);
        Assert.Equal("保留的待办", existingTodos[0].Title);
        Assert.Equal(1, plan.Preview.SettingCount);
        Assert.Equal(2, plan.Preview.ShortcutCount);
        Assert.Equal(1, plan.Preview.TodoCount);
        Assert.True(plan.Preview.ContainsSensitivePlaintext);
        Assert.Collection(
            plan.Preview.Shortcuts,
            shortcut =>
            {
                Assert.Equal("安全文件夹", shortcut.Name);
                Assert.Equal(@"C:\Users\Public", shortcut.Path);
                Assert.False(shortcut.IsRisky);
            },
            shortcut =>
            {
                Assert.Equal("带参数程序", shortcut.Name);
                Assert.Equal(@"C:\Tools\runner.exe", shortcut.Path);
                Assert.Equal("--danger", shortcut.LaunchArguments);
                Assert.True(shortcut.IsRisky);
            });
        Cleanup();
    }

    [Fact]
    public async Task ApplyImportAsync_LegacyWeatherCredentials_ShouldIgnoreThemAndProtectClipboard()
    {
        var (db, _, _, quickTextService, _, settingsService, backupService) = await InitAsync();
        await File.WriteAllTextAsync(
            _backupFile,
            """
            {
              "version": 4,
              "exportedAt": "2026-07-10T00:00:00Z",
              "settings": {
                "WeatherApiKey": "legacy-weather",
                "WeatherApiHost": "attacker.example"
              },
              "clipboardHistory": [
                {
                  "content": "legacy clipboard",
                  "createdAt": "2026-07-10T00:00:00Z",
                  "lastUsedAt": "2026-07-10T00:00:00Z",
                  "useCount": 1
                }
              ]
            }
            """);

        var result = await ImportAsync(backupService, _backupFile);

        Assert.Equal(0, result.SettingCount);
        Assert.Equal(1, result.ClipboardHistoryCount);
        var rawWeatherKey = await db.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'WeatherApiKey'",
            reader => reader.GetString(0));
        var rawWeatherHost = await db.QuerySingleAsync(
            "SELECT Value FROM Settings WHERE Key = 'WeatherApiHost'",
            reader => reader.GetString(0));
        var rawClipboard = await db.QuerySingleAsync(
            "SELECT Content FROM ClipboardHistory LIMIT 1",
            reader => reader.GetString(0));
        Assert.Equal(string.Empty, rawWeatherKey);
        Assert.Equal(string.Empty, rawWeatherHost);
        Assert.StartsWith(DpapiUserDataProtector.Prefix, rawClipboard);
        Assert.DoesNotContain("legacy clipboard", rawClipboard);
        Assert.Equal(string.Empty, await settingsService.GetSettingAsync("WeatherApiKey"));
        Assert.Equal(string.Empty, await settingsService.GetSettingAsync("WeatherApiHost"));
        Assert.Equal("legacy clipboard", Assert.Single(await quickTextService.GetClipboardHistoryAsync()).Content);
        Cleanup();
    }

    private static async Task<TodoBackupImportResult> ImportAsync(
        ITodoBackupService backupService,
        string filePath)
    {
        var plan = await backupService.PrepareImportAsync(filePath);
        return await backupService.ApplyImportAsync(plan);
    }

    private async Task<(DatabaseService Db, TodoService TodoService, QuickNoteService QuickNoteService, QuickTextService QuickTextService, ShortcutService ShortcutService, SettingsService SettingsService, TodoBackupService BackupService)> InitAsync()
    {
        Cleanup();
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        var todoService = new TodoService(db);
        var quickNoteService = new QuickNoteService(db);
        var settingsService = new SettingsService(db);
        var quickTextService = new QuickTextService(db, settingsService);
        var shortcutService = new ShortcutService(db);
        var backupService = new TodoBackupService(
            todoService,
            quickNoteService,
            quickTextService,
            shortcutService,
            settingsService,
            db);
        return (db, todoService, quickNoteService, quickTextService, shortcutService, settingsService, backupService);
    }

    private void Cleanup()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_testDbFile))
            {
                File.Delete(_testDbFile);
            }

            if (File.Exists(_backupFile))
            {
                File.Delete(_backupFile);
            }
        }
        catch
        {
        }
    }
}
