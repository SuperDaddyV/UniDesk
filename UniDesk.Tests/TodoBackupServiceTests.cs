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

        var result = await backupService.ImportFromFileAsync(_backupFile);

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

        var result = await backupService.ImportFromFileAsync(_backupFile);

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
    public async Task ExportToFileAsync_Default_ShouldExcludeWeatherKeyAndClipboardHistory()
    {
        var (_, _, _, quickTextService, _, settingsService, backupService) = await InitAsync();
        await settingsService.SetSettingAsync("WeatherApiKey", "weather-secret");
        await quickTextService.RecordClipboardTextAsync("portable clipboard");

        await backupService.ExportToFileAsync(_backupFile);

        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(_backupFile));
        var root = document.RootElement;
        Assert.Equal(5, root.GetProperty("version").GetInt32());
        Assert.False(root.GetProperty("settings").TryGetProperty("WeatherApiKey", out _));
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

        await Assert.ThrowsAsync<InvalidDataException>(() => backupService.ImportFromFileAsync(_backupFile));

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

        await Assert.ThrowsAsync<InvalidDataException>(() => backupService.ImportFromFileAsync(_backupFile));

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

        await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(
            () => backupService.ImportFromFileAsync(_backupFile));

        var todos = await todoService.GetAllTodosAsync();
        Assert.Single(todos);
        Assert.Equal("保留的待办", todos[0].Title);
        Assert.Equal("320", await settingsService.GetSettingAsync("PanelWidth"));
        Cleanup();
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
