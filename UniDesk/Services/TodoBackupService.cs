using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public class TodoBackupService : ITodoBackupService
{
    private const int CurrentBackupVersion = 4;
    private readonly ITodoService _todoService;
    private readonly IQuickNoteService _quickNoteService;
    private readonly IQuickTextService _quickTextService;
    private readonly IShortcutService _shortcutService;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseService _databaseService;

    private static readonly HashSet<string> ExcludedSettingKeys = new(StringComparer.Ordinal)
    {
        "DatabaseVersion",
        WeatherApiDefaults.DefaultApiKeySettingKey,
        WeatherApiDefaults.DefaultApiHostSettingKey
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly UTF8Encoding Utf8NoBom = new(encoderShouldEmitUTF8Identifier: false);

    public TodoBackupService(
        ITodoService todoService,
        IQuickNoteService quickNoteService,
        IQuickTextService quickTextService,
        IShortcutService shortcutService,
        ISettingsService settingsService,
        IDatabaseService databaseService)
    {
        _todoService = todoService;
        _quickNoteService = quickNoteService;
        _quickTextService = quickTextService;
        _shortcutService = shortcutService;
        _settingsService = settingsService;
        _databaseService = databaseService;
    }

    public async Task ExportToFileAsync(string filePath)
    {
        var todos = await _todoService.GetAllTodosAsync();
        var quickNotes = await _quickNoteService.GetAllQuickNotesAsync();
        var clipboardHistory = await _quickTextService.GetClipboardHistoryAsync(10_000);
        var textSnippets = await _quickTextService.GetTextSnippetsAsync();
        var shortcuts = await _shortcutService.GetAllShortcutsAsync();
        var settings = await GetSettingsBackupAsync();
        var payload = new TodoBackupFile
        {
            Version = CurrentBackupVersion,
            ExportedAt = DateTime.UtcNow,
            Settings = settings,
            Shortcuts = shortcuts.Select(ShortcutBackupEntry.FromShortcut).ToList(),
            Todos = todos.Select(TodoBackupEntry.FromTodo).ToList(),
            QuickNotes = quickNotes.Select(QuickNoteBackupEntry.FromQuickNote).ToList(),
            ClipboardHistory = clipboardHistory.Select(ClipboardHistoryBackupEntry.FromHistory).ToList(),
            TextSnippets = textSnippets.Select(TextSnippetBackupEntry.FromSnippet).ToList()
        };

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await File.WriteAllTextAsync(filePath, json, Utf8NoBom);
    }

    public async Task<TodoBackupImportResult> ImportFromFileAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath, Utf8NoBom);
        var payload = JsonSerializer.Deserialize<TodoBackupFile>(json, JsonOptions)
                      ?? throw new InvalidDataException("备份文件格式无效。");

        ValidatePayload(payload);

        var result = new TodoBackupImportResult();

        if (payload.Settings != null)
        {
            result.SettingCount = await RestoreSettingsAsync(payload.Settings);
        }

        if (payload.Shortcuts != null)
        {
            await _databaseService.ExecuteNonQueryAsync("DELETE FROM Shortcuts");

            foreach (var entry in payload.Shortcuts)
            {
                if (string.IsNullOrWhiteSpace(entry.Name) || string.IsNullOrWhiteSpace(entry.Path))
                {
                    continue;
                }

                var shortcut = entry.ToShortcut();
                var id = await _shortcutService.CreateShortcutAsync(shortcut);
                if (id > 0)
                {
                    result.ShortcutCount++;
                }
            }

            await _shortcutService.NormalizeSortOrderAsync();
        }

        if (payload.Todos != null)
        {
            await _databaseService.ExecuteNonQueryAsync("DELETE FROM Todos");

            foreach (var entry in payload.Todos)
            {
                if (string.IsNullOrWhiteSpace(entry.Title))
                {
                    continue;
                }

                await _todoService.CreateTodoAsync(entry.ToTodo());
                result.TodoCount++;
            }
        }

        if (payload.QuickNotes != null)
        {
            await _databaseService.ExecuteNonQueryAsync("DELETE FROM QuickNotes");

            foreach (var entry in payload.QuickNotes)
            {
                if (string.IsNullOrWhiteSpace(entry.Title) && string.IsNullOrWhiteSpace(entry.Content))
                {
                    continue;
                }

                await _quickNoteService.CreateQuickNoteAsync(entry.ToQuickNote());
                result.QuickNoteCount++;
            }
        }

        if (payload.ClipboardHistory != null)
        {
            await _databaseService.ExecuteNonQueryAsync("DELETE FROM ClipboardHistory");

            foreach (var entry in payload.ClipboardHistory)
            {
                var content = QuickTextService.NormalizeClipboardText(entry.Content);
                if (string.IsNullOrWhiteSpace(content))
                {
                    continue;
                }

                var hash = string.IsNullOrWhiteSpace(entry.ContentHash)
                    ? QuickTextService.ComputeHash(content)
                    : entry.ContentHash;
                var now = DateTime.UtcNow;
                var createdAt = entry.CreatedAt == default ? now : entry.CreatedAt;
                var lastUsedAt = entry.LastUsedAt == default ? createdAt : entry.LastUsedAt;

                await _databaseService.ExecuteNonQueryAsync(
                    "INSERT OR IGNORE INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt, UseCount) VALUES (@p0, @p1, @p2, @p3, @p4)",
                    content,
                    hash,
                    createdAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    lastUsedAt.ToString("o", System.Globalization.CultureInfo.InvariantCulture),
                    Math.Max(1, entry.UseCount));
                result.ClipboardHistoryCount++;
            }
        }

        if (payload.TextSnippets != null)
        {
            await _databaseService.ExecuteNonQueryAsync("DELETE FROM TextSnippets");

            foreach (var entry in payload.TextSnippets)
            {
                if (string.IsNullOrWhiteSpace(entry.Content))
                {
                    continue;
                }

                await _quickTextService.CreateTextSnippetAsync(entry.ToSnippet());
                result.TextSnippetCount++;
            }
        }

        return result;
    }

    private async Task<Dictionary<string, string?>> GetSettingsBackupAsync()
    {
        var settings = await _databaseService.QueryAsync(
            "SELECT Key, Value FROM Settings ORDER BY Key",
            reader => new KeyValuePair<string, string?>(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1)));

        return settings
            .Where(setting => !ExcludedSettingKeys.Contains(setting.Key))
            .ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal);
    }

    private async Task<int> RestoreSettingsAsync(Dictionary<string, string?> settings)
    {
        var restored = 0;
        foreach (var (key, value) in settings)
        {
            if (string.IsNullOrWhiteSpace(key) || ExcludedSettingKeys.Contains(key))
            {
                continue;
            }

            var normalizedValue = key == DashboardModuleCatalog.SettingsKey
                ? NormalizeModuleSettingsJson(value)
                : value;

            await _settingsService.SetSettingAsync(key, normalizedValue);
            restored++;
        }

        return restored;
    }

    private static string? NormalizeModuleSettingsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.Serialize(DashboardModuleCatalog.CreateDefaultModules(), JsonOptions);
        }

        try
        {
            var modules = JsonSerializer.Deserialize<List<ModuleSetting>>(json, JsonOptions);
            return JsonSerializer.Serialize(DashboardModuleCatalog.Normalize(modules), JsonOptions);
        }
        catch
        {
            return JsonSerializer.Serialize(DashboardModuleCatalog.CreateDefaultModules(), JsonOptions);
        }
    }

    private static bool HasRestorableData(TodoBackupFile payload) =>
        payload.Settings != null ||
        payload.Shortcuts != null ||
        payload.Todos != null ||
        payload.QuickNotes != null ||
        payload.ClipboardHistory != null ||
        payload.TextSnippets != null;

    private static void ValidatePayload(TodoBackupFile payload)
    {
        if (payload.Version is < 1 or > CurrentBackupVersion)
        {
            throw new InvalidDataException($"不支持的备份版本：{payload.Version}。");
        }

        if (!HasRestorableData(payload))
        {
            throw new InvalidDataException("备份文件中没有可还原的数据。");
        }

        if (payload.Settings != null && payload.Settings.Keys.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidDataException("Settings 包含空白 key。");
        }

        ValidateEntries(
            payload.Shortcuts,
            "Shortcuts",
            entry => !string.IsNullOrWhiteSpace(entry.Name) && !string.IsNullOrWhiteSpace(entry.Path));
        ValidateEntries(
            payload.Todos,
            "Todos",
            entry => !string.IsNullOrWhiteSpace(entry.Title));
        ValidateEntries(
            payload.QuickNotes,
            "QuickNotes",
            entry => !string.IsNullOrWhiteSpace(entry.Title) || !string.IsNullOrWhiteSpace(entry.Content));
        ValidateEntries(
            payload.ClipboardHistory,
            "ClipboardHistory",
            entry => !string.IsNullOrWhiteSpace(QuickTextService.NormalizeClipboardText(entry.Content)));
        ValidateEntries(
            payload.TextSnippets,
            "TextSnippets",
            entry => !string.IsNullOrWhiteSpace(entry.Content));
    }

    private static void ValidateEntries<T>(
        IReadOnlyList<T>? entries,
        string section,
        Func<T, bool> isValid)
    {
        if (entries == null)
        {
            return;
        }

        for (var index = 0; index < entries.Count; index++)
        {
            if (!isValid(entries[index]))
            {
                throw new InvalidDataException($"{section}[{index}] 包含无效数据。");
            }
        }
    }

    private sealed class TodoBackupFile
    {
        public int Version { get; set; }
        public DateTime ExportedAt { get; set; }
        public Dictionary<string, string?>? Settings { get; set; }
        public List<ShortcutBackupEntry>? Shortcuts { get; set; }
        public List<TodoBackupEntry>? Todos { get; set; }
        public List<QuickNoteBackupEntry>? QuickNotes { get; set; }
        public List<ClipboardHistoryBackupEntry>? ClipboardHistory { get; set; }
        public List<TextSnippetBackupEntry>? TextSnippets { get; set; }
    }

    private sealed class ShortcutBackupEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;
        public string? LaunchArguments { get; set; }
        public ShortcutType Type { get; set; } = ShortcutType.Application;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }

        public static ShortcutBackupEntry FromShortcut(ShortcutItem shortcut) => new()
        {
            Name = shortcut.Name,
            Path = shortcut.Path,
            LaunchArguments = shortcut.LaunchArguments,
            Type = shortcut.Type,
            SortOrder = shortcut.SortOrder,
            CreatedAt = shortcut.CreatedAt
        };

        public ShortcutItem ToShortcut()
        {
            var now = DateTime.UtcNow;
            return new ShortcutItem
            {
                Name = Name ?? string.Empty,
                Path = Path ?? string.Empty,
                LaunchArguments = LaunchArguments,
                Type = Type,
                SortOrder = Math.Max(0, SortOrder),
                CreatedAt = CreatedAt == default ? now : CreatedAt,
                IconLookupPath = Path
            };
        }
    }

    private sealed class TodoBackupEntry
    {
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime? DueDate { get; set; }
        public TodoPriority Priority { get; set; } = TodoPriority.Medium;
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }

        public static TodoBackupEntry FromTodo(TodoItem todo) => new()
        {
            Title = todo.Title,
            IsCompleted = todo.IsCompleted,
            DueDate = todo.DueDate,
            Priority = todo.Priority,
            CreatedAt = todo.CreatedAt,
            CompletedAt = todo.CompletedAt
        };

        public TodoItem ToTodo() => new()
        {
            Title = Title,
            IsCompleted = IsCompleted,
            DueDate = DueDate,
            Priority = Priority,
            CreatedAt = CreatedAt == default ? DateTime.UtcNow : CreatedAt,
            CompletedAt = CompletedAt
        };
    }

    private sealed class QuickNoteBackupEntry
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public bool IsPinned { get; set; }
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static QuickNoteBackupEntry FromQuickNote(QuickNote note) => new()
        {
            Title = note.Title,
            Content = note.Content,
            IsPinned = note.IsPinned,
            SortOrder = note.SortOrder,
            CreatedAt = note.CreatedAt,
            UpdatedAt = note.UpdatedAt
        };

        public QuickNote ToQuickNote()
        {
            var now = DateTime.UtcNow;
            return new QuickNote
            {
                Title = Title ?? string.Empty,
                Content = Content ?? string.Empty,
                IsPinned = IsPinned,
                SortOrder = SortOrder,
                CreatedAt = CreatedAt == default ? now : CreatedAt,
                UpdatedAt = UpdatedAt == default ? now : UpdatedAt
            };
        }
    }

    private sealed class ClipboardHistoryBackupEntry
    {
        public string Content { get; set; } = string.Empty;
        public string ContentHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime LastUsedAt { get; set; }
        public int UseCount { get; set; }

        public static ClipboardHistoryBackupEntry FromHistory(ClipboardHistoryItem item) => new()
        {
            Content = item.Content,
            ContentHash = item.ContentHash,
            CreatedAt = item.CreatedAt,
            LastUsedAt = item.LastUsedAt,
            UseCount = item.UseCount
        };
    }

    private sealed class TextSnippetBackupEntry
    {
        public string Title { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string Category { get; set; } = "默认";
        public bool IsPinned { get; set; }
        public int SortOrder { get; set; }
        public int UseCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }

        public static TextSnippetBackupEntry FromSnippet(TextSnippet snippet) => new()
        {
            Title = snippet.Title,
            Content = snippet.Content,
            Category = snippet.Category,
            IsPinned = snippet.IsPinned,
            SortOrder = snippet.SortOrder,
            UseCount = snippet.UseCount,
            CreatedAt = snippet.CreatedAt,
            UpdatedAt = snippet.UpdatedAt,
            LastUsedAt = snippet.LastUsedAt
        };

        public TextSnippet ToSnippet()
        {
            var now = DateTime.UtcNow;
            return new TextSnippet
            {
                Title = Title ?? string.Empty,
                Content = Content ?? string.Empty,
                Category = string.IsNullOrWhiteSpace(Category) ? "默认" : Category,
                IsPinned = IsPinned,
                SortOrder = SortOrder,
                UseCount = Math.Max(0, UseCount),
                CreatedAt = CreatedAt == default ? now : CreatedAt,
                UpdatedAt = UpdatedAt == default ? now : UpdatedAt,
                LastUsedAt = LastUsedAt
            };
        }
    }
}
