using System.IO;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public class TodoBackupService : ITodoBackupService
{
    private const int CurrentBackupVersion = 5;
    private const long MaxBackupFileSizeBytes = 25L * 1024 * 1024;
    private const int MaxSettingsCount = 1_000;
    private const int MaxEntriesPerSection = 10_000;
    private const int MaxIncludedSectionsCount = 32;
    private const int MaxSettingKeyLength = 256;
    private const int MaxShortFieldLength = 4_096;
    private const int MaxPathFieldLength = 32_768;
    private const int MaxContentFieldLength = 1_048_576;
    private readonly ITodoService _todoService;
    private readonly IQuickNoteService _quickNoteService;
    private readonly IQuickTextService _quickTextService;
    private readonly IShortcutService _shortcutService;
    private readonly ISettingsService _settingsService;
    private readonly IDatabaseService _databaseService;
    private readonly IUserDataProtector _userDataProtector;

    private static readonly HashSet<string> ExportExcludedSettingKeys = new(StringComparer.Ordinal)
    {
        "DatabaseVersion",
        "WeatherApiKey",
        "WeatherApiHost",
        WeatherApiDefaults.DefaultApiKeySettingKey,
        WeatherApiDefaults.DefaultApiHostSettingKey
    };

    private static readonly HashSet<string> RestoreExcludedSettingKeys = new(StringComparer.Ordinal)
    {
        "DatabaseVersion",
        "WeatherApiKey",
        "WeatherApiHost",
        WeatherApiDefaults.DefaultApiKeySettingKey,
        WeatherApiDefaults.DefaultApiHostSettingKey
    };

    private static readonly HashSet<string> RiskyShortcutExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".com", ".bat", ".cmd", ".ps1", ".vbs", ".js", ".msi", ".lnk"
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
        : this(
            todoService,
            quickNoteService,
            quickTextService,
            shortcutService,
            settingsService,
            databaseService,
            new DpapiUserDataProtector())
    {
    }

    public TodoBackupService(
        ITodoService todoService,
        IQuickNoteService quickNoteService,
        IQuickTextService quickTextService,
        IShortcutService shortcutService,
        ISettingsService settingsService,
        IDatabaseService databaseService,
        IUserDataProtector userDataProtector)
    {
        _todoService = todoService;
        _quickNoteService = quickNoteService;
        _quickTextService = quickTextService;
        _shortcutService = shortcutService;
        _settingsService = settingsService;
        _databaseService = databaseService;
        _userDataProtector = userDataProtector;
    }

    public async Task ExportToFileAsync(
        string filePath,
        BackupExportOptions? options = null)
    {
        options ??= new BackupExportOptions();
        await _settingsService.FlushPendingSavesAsync();
        var payload = await _databaseService.ExecuteInTransactionAsync(
            session => ReadExportSnapshotAsync(session, options));

        var json = JsonSerializer.Serialize(payload, JsonOptions);
        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("备份目标路径无效。");
        var temporaryPath = Path.Combine(directory, $".{Path.GetFileName(fullPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            await File.WriteAllTextAsync(temporaryPath, json, Utf8NoBom);
            File.Move(temporaryPath, fullPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    private async Task<TodoBackupFile> ReadExportSnapshotAsync(
        IDatabaseSession session,
        BackupExportOptions options)
    {
        var settings = await session.QueryAsync(
            "SELECT Key, Value FROM Settings ORDER BY Key",
            reader => new KeyValuePair<string, string?>(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1)));
        var shortcuts = await session.QueryAsync(
            "SELECT Name, Path, LaunchArguments, Type, SortOrder, CreatedAt FROM Shortcuts ORDER BY SortOrder, CreatedAt, Id",
            reader => new ShortcutBackupEntry
            {
                Name = reader.GetString(0),
                Path = reader.GetString(1),
                LaunchArguments = reader.IsDBNull(2) ? null : reader.GetString(2),
                Type = Enum.TryParse<ShortcutType>(reader.GetString(3), out var type) ? type : ShortcutType.Application,
                SortOrder = reader.GetInt32(4),
                CreatedAt = ReadDateTime(reader.GetString(5), "Shortcuts.CreatedAt")
            });
        var todos = await session.QueryAsync(
            "SELECT Title, IsCompleted, DueDate, Priority, CreatedAt, CompletedAt FROM Todos ORDER BY Id",
            reader => new TodoBackupEntry
            {
                Title = reader.GetString(0),
                IsCompleted = reader.GetInt32(1) != 0,
                DueDate = reader.IsDBNull(2) ? null : ReadDateTime(reader.GetString(2), "Todos.DueDate"),
                Priority = Enum.IsDefined(typeof(TodoPriority), reader.GetInt32(3))
                    ? (TodoPriority)reader.GetInt32(3)
                    : TodoPriority.Medium,
                CreatedAt = ReadDateTime(reader.GetString(4), "Todos.CreatedAt"),
                CompletedAt = reader.IsDBNull(5) ? null : ReadDateTime(reader.GetString(5), "Todos.CompletedAt")
            });
        var quickNotes = await session.QueryAsync(
            "SELECT Title, Content, IsPinned, SortOrder, CreatedAt, UpdatedAt FROM QuickNotes ORDER BY Id",
            reader => new QuickNoteBackupEntry
            {
                Title = reader.GetString(0),
                Content = reader.GetString(1),
                IsPinned = reader.GetInt32(2) != 0,
                SortOrder = reader.GetInt32(3),
                CreatedAt = ReadDateTime(reader.GetString(4), "QuickNotes.CreatedAt"),
                UpdatedAt = ReadDateTime(reader.GetString(5), "QuickNotes.UpdatedAt")
            });
        var textSnippets = await session.QueryAsync(
            "SELECT Title, Content, Category, IsPinned, SortOrder, UseCount, CreatedAt, UpdatedAt, LastUsedAt FROM TextSnippets ORDER BY Id",
            reader => new TextSnippetBackupEntry
            {
                Title = reader.GetString(0),
                Content = reader.GetString(1),
                Category = reader.GetString(2),
                IsPinned = reader.GetInt32(3) != 0,
                SortOrder = reader.GetInt32(4),
                UseCount = reader.GetInt32(5),
                CreatedAt = ReadDateTime(reader.GetString(6), "TextSnippets.CreatedAt"),
                UpdatedAt = ReadDateTime(reader.GetString(7), "TextSnippets.UpdatedAt"),
                LastUsedAt = reader.IsDBNull(8) ? null : ReadDateTime(reader.GetString(8), "TextSnippets.LastUsedAt")
            });
        List<ClipboardHistoryBackupEntry>? clipboardHistory = null;
        if (options.IncludeClipboardHistory)
        {
            clipboardHistory = await session.QueryAsync(
                "SELECT Id, Content, ContentHash, CreatedAt, LastUsedAt, UseCount FROM ClipboardHistory ORDER BY LastUsedAt DESC",
                reader =>
                {
                    var id = reader.GetInt32(0);
                    var storedContent = reader.GetString(1);
                    var content = storedContent;
                    if (_userDataProtector.IsProtected(storedContent) &&
                        !_userDataProtector.TryUnprotect(storedContent, out content))
                    {
                        throw new InvalidDataException($"剪贴板历史 {id} 无法解密，备份已取消以避免生成不完整文件。");
                    }

                    return new ClipboardHistoryBackupEntry
                    {
                        Content = content,
                        ContentHash = reader.GetString(2),
                        CreatedAt = ReadDateTime(reader.GetString(3), "ClipboardHistory.CreatedAt"),
                        LastUsedAt = ReadDateTime(reader.GetString(4), "ClipboardHistory.LastUsedAt"),
                        UseCount = reader.GetInt32(5)
                    };
                });
        }

        return new TodoBackupFile
        {
            Version = CurrentBackupVersion,
            ExportedAt = DateTime.UtcNow,
            IncludedSections = options.IncludeClipboardHistory
                ? ["settings", "shortcuts", "todos", "quickNotes", "clipboardHistory", "textSnippets"]
                : ["settings", "shortcuts", "todos", "quickNotes", "textSnippets"],
            ContainsSensitivePlaintext = options.IncludeClipboardHistory,
            Settings = settings
                .Where(setting => !ExportExcludedSettingKeys.Contains(setting.Key))
                .ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal),
            Shortcuts = shortcuts,
            Todos = todos,
            QuickNotes = quickNotes,
            ClipboardHistory = clipboardHistory,
            TextSnippets = textSnippets
        };
    }

    private static DateTime ReadDateTime(string value, string field)
    {
        if (DateTime.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            return parsed;
        }

        throw new InvalidDataException($"数据库字段 {field} 包含无效日期，备份已取消。");
    }

    public async Task<BackupImportPlan> PrepareImportAsync(string filePath)
    {
        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length > MaxBackupFileSizeBytes)
        {
            throw new InvalidDataException("备份文件超过 25 MiB 上限。");
        }

        var json = await File.ReadAllTextAsync(filePath, Utf8NoBom);
        TodoBackupFile payload;
        try
        {
            payload = JsonSerializer.Deserialize<TodoBackupFile>(json, JsonOptions)
                      ?? throw new InvalidDataException("备份文件格式无效。");
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("备份文件格式无效。", exception);
        }

        ValidatePayload(payload);
        return new BackupImportPlan(payload, BuildPreview(payload));
    }

    public async Task<TodoBackupImportResult> ApplyImportAsync(BackupImportPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (plan.Document is not TodoBackupFile payload)
        {
            throw new ArgumentException("导入计划不是由当前备份服务生成。", nameof(plan));
        }

        await _settingsService.FlushPendingSavesAsync();
        var result = await _databaseService.ExecuteInTransactionAsync(
            session => RestorePayloadAsync(session, payload));
        _settingsService.InvalidateCache();
        if (payload.Shortcuts != null)
        {
            await _shortcutService.RefreshMissingIconsAsync();
        }

        return result;
    }

    private static BackupImportPreview BuildPreview(TodoBackupFile payload)
    {
        var shortcuts = payload.Shortcuts?
            .Select(entry => new BackupShortcutPreview(
                entry.Name,
                entry.Path,
                entry.LaunchArguments,
                IsRiskyShortcut(entry.Path, entry.LaunchArguments)))
            .ToArray() ?? [];

        return new BackupImportPreview
        {
            SettingCount = payload.Settings?.Count ?? 0,
            ShortcutCount = shortcuts.Length,
            TodoCount = payload.Todos?.Count ?? 0,
            QuickNoteCount = payload.QuickNotes?.Count ?? 0,
            ClipboardHistoryCount = payload.ClipboardHistory?.Count ?? 0,
            TextSnippetCount = payload.TextSnippets?.Count ?? 0,
            ContainsSensitivePlaintext = payload.ContainsSensitivePlaintext ||
                                         payload.Settings?.ContainsKey("WeatherApiKey") == true ||
                                         payload.Settings?.ContainsKey("WeatherApiHost") == true ||
                                         payload.ClipboardHistory is { Count: > 0 },
            Shortcuts = shortcuts
        };
    }

    private static bool IsRiskyShortcut(string path, string? launchArguments)
    {
        if (!string.IsNullOrWhiteSpace(launchArguments) ||
            Uri.TryCreate(path, UriKind.Absolute, out var uri) && !uri.IsFile)
        {
            return true;
        }

        return RiskyShortcutExtensions.Contains(Path.GetExtension(path));
    }

    private async Task<Dictionary<string, string?>> GetSettingsBackupAsync()
    {
        var settings = await _databaseService.QueryAsync(
            "SELECT Key, Value FROM Settings ORDER BY Key",
            reader => new KeyValuePair<string, string?>(
                reader.GetString(0),
                reader.IsDBNull(1) ? null : reader.GetString(1)));

        return settings
            .Where(setting => !ExportExcludedSettingKeys.Contains(setting.Key))
            .ToDictionary(setting => setting.Key, setting => setting.Value, StringComparer.Ordinal);
    }

    private async Task<TodoBackupImportResult> RestorePayloadAsync(
        IDatabaseSession session,
        TodoBackupFile payload)
    {
        var result = new TodoBackupImportResult();

        if (payload.Settings != null)
        {
            foreach (var (key, value) in payload.Settings)
            {
                if (RestoreExcludedSettingKeys.Contains(key))
                {
                    continue;
                }

                var normalizedValue = key == DashboardModuleCatalog.SettingsKey
                    ? NormalizeModuleSettingsJson(value)
                    : value;
                result.SettingCount += string.IsNullOrEmpty(normalizedValue)
                    ? await session.ExecuteNonQueryAsync("DELETE FROM Settings WHERE Key = @p0", key)
                    : await session.ExecuteNonQueryAsync(
                        "INSERT OR REPLACE INTO Settings (Key, Value) VALUES (@p0, @p1)",
                        key,
                        normalizedValue);
            }
        }

        if (payload.Shortcuts != null)
        {
            await session.ExecuteNonQueryAsync("DELETE FROM Shortcuts");
            var orderedShortcuts = payload.Shortcuts
                .Select((entry, index) => new { Entry = entry, Index = index })
                .OrderBy(item => item.Entry.SortOrder)
                .ThenBy(item => item.Index)
                .ToList();

            for (var sortOrder = 0; sortOrder < orderedShortcuts.Count; sortOrder++)
            {
                var shortcut = orderedShortcuts[sortOrder].Entry.ToShortcut();
                result.ShortcutCount += await session.ExecuteNonQueryAsync(
                    "INSERT INTO Shortcuts (Name, Path, Type, IconPath, SortOrder, CreatedAt, LaunchArguments) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6)",
                    shortcut.Name,
                    shortcut.Path,
                    shortcut.Type.ToString(),
                    null,
                    sortOrder,
                    shortcut.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    shortcut.LaunchArguments);
            }
        }

        if (payload.Todos != null)
        {
            await session.ExecuteNonQueryAsync("DELETE FROM Todos");
            foreach (var entry in payload.Todos)
            {
                var todo = entry.ToTodo();
                result.TodoCount += await session.ExecuteNonQueryAsync(
                    "INSERT INTO Todos (Title, IsCompleted, DueDate, CreatedAt, CompletedAt, Priority) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
                    todo.Title,
                    todo.IsCompleted ? 1 : 0,
                    todo.DueDate?.ToString("o", CultureInfo.InvariantCulture),
                    todo.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    todo.CompletedAt?.ToString("o", CultureInfo.InvariantCulture),
                    (int)todo.Priority);
            }
        }

        if (payload.QuickNotes != null)
        {
            await session.ExecuteNonQueryAsync("DELETE FROM QuickNotes");
            foreach (var entry in payload.QuickNotes)
            {
                var note = entry.ToQuickNote();
                result.QuickNoteCount += await session.ExecuteNonQueryAsync(
                    "INSERT INTO QuickNotes (Title, Content, IsPinned, SortOrder, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4, @p5)",
                    note.Title,
                    note.Content,
                    note.IsPinned ? 1 : 0,
                    note.SortOrder,
                    note.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    note.UpdatedAt.ToString("o", CultureInfo.InvariantCulture));
            }
        }

        if (payload.ClipboardHistory != null)
        {
            await session.ExecuteNonQueryAsync("DELETE FROM ClipboardHistory");
            foreach (var entry in payload.ClipboardHistory)
            {
                var content = QuickTextService.NormalizeClipboardText(entry.Content);
                result.ClipboardHistoryCount += await session.ExecuteNonQueryAsync(
                    "INSERT OR IGNORE INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt, UseCount) VALUES (@p0, @p1, @p2, @p3, @p4)",
                    _userDataProtector.Protect(content),
                    QuickTextService.ComputeHash(content),
                    entry.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    entry.LastUsedAt.ToString("o", CultureInfo.InvariantCulture),
                    entry.UseCount);
            }
        }

        if (payload.TextSnippets != null)
        {
            await session.ExecuteNonQueryAsync("DELETE FROM TextSnippets");
            foreach (var entry in payload.TextSnippets)
            {
                var snippet = entry.ToSnippet();
                result.TextSnippetCount += await session.ExecuteNonQueryAsync(
                    "INSERT INTO TextSnippets (Title, Content, Category, IsPinned, SortOrder, UseCount, CreatedAt, UpdatedAt, LastUsedAt) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8)",
                    snippet.Title,
                    snippet.Content,
                    snippet.Category,
                    snippet.IsPinned ? 1 : 0,
                    snippet.SortOrder,
                    snippet.UseCount,
                    snippet.CreatedAt.ToString("o", CultureInfo.InvariantCulture),
                    snippet.UpdatedAt.ToString("o", CultureInfo.InvariantCulture),
                    snippet.LastUsedAt?.ToString("o", CultureInfo.InvariantCulture));
            }
        }

        return result;
    }

    private static string? NormalizeModuleSettingsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return JsonSerializer.Serialize(DashboardModuleCatalog.CreateDefaultModules(), JsonOptions);
        }

        var modules = DeserializeModuleSettingsJson(json);
        return JsonSerializer.Serialize(DashboardModuleCatalog.Normalize(modules), JsonOptions);
    }

    private static void ValidateModuleSettingsJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        _ = DeserializeModuleSettingsJson(json);
    }

    private static List<ModuleSetting> DeserializeModuleSettingsJson(string json)
    {
        List<ModuleSetting>? modules;
        try
        {
            modules = JsonSerializer.Deserialize<List<ModuleSetting>>(json, JsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException("ModuleSettings 格式无效。", exception);
        }

        if (modules == null ||
            modules.Any(module => module == null || string.IsNullOrWhiteSpace(module.ModuleId)))
        {
            throw new InvalidDataException("ModuleSettings 结构无效。模块 ID 不能为空。");
        }

        return modules;
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

        ValidateDate(payload.ExportedAt, "ExportedAt");

        ValidateEntryCount(payload.IncludedSections, "IncludedSections", MaxIncludedSectionsCount);
        if (payload.IncludedSections != null)
        {
            for (var index = 0; index < payload.IncludedSections.Count; index++)
            {
                var section = RequireEntry(
                    payload.IncludedSections[index],
                    $"IncludedSections[{index}]");
                ValidateFieldLength(
                    section,
                    $"IncludedSections[{index}]",
                    MaxShortFieldLength);
            }
        }

        if (payload.Settings != null)
        {
            if (payload.Settings.Count > MaxSettingsCount)
            {
                throw new InvalidDataException(
                    $"Settings 包含 {payload.Settings.Count} 条，最多允许 {MaxSettingsCount} 条。");
            }

            foreach (var (key, value) in payload.Settings)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    throw new InvalidDataException("Settings 包含空白 key。");
                }

                ValidateFieldLength(key, $"Settings[{key}].Key", MaxSettingKeyLength);
                ValidateFieldLength(value, $"Settings[{key}].Value", MaxContentFieldLength);
                if (string.Equals(key, DashboardModuleCatalog.SettingsKey, StringComparison.Ordinal))
                {
                    ValidateModuleSettingsJson(value);
                }
            }
        }

        ValidateEntryCount(payload.Shortcuts, "Shortcuts", MaxEntriesPerSection);
        ValidateEntryCount(payload.Todos, "Todos", MaxEntriesPerSection);
        ValidateEntryCount(payload.QuickNotes, "QuickNotes", MaxEntriesPerSection);
        ValidateEntryCount(payload.ClipboardHistory, "ClipboardHistory", MaxEntriesPerSection);
        ValidateEntryCount(payload.TextSnippets, "TextSnippets", MaxEntriesPerSection);

        if (payload.Shortcuts != null)
        {
            for (var index = 0; index < payload.Shortcuts.Count; index++)
            {
                var entry = RequireEntry(payload.Shortcuts[index], $"Shortcuts[{index}]");
                ValidateEnum(entry.Type, $"Shortcuts[{index}].Type");
                ValidateNonNegative(entry.SortOrder, $"Shortcuts[{index}].SortOrder");
                ValidateDate(entry.CreatedAt, $"Shortcuts[{index}].CreatedAt");
                ValidateRequiredField(entry.Name, $"Shortcuts[{index}].Name");
                ValidateRequiredField(entry.Path, $"Shortcuts[{index}].Path");
                ValidateFieldLength(entry.Name, $"Shortcuts[{index}].Name", MaxShortFieldLength);
                ValidateFieldLength(entry.Path, $"Shortcuts[{index}].Path", MaxPathFieldLength);
                ValidateFieldLength(
                    entry.LaunchArguments,
                    $"Shortcuts[{index}].LaunchArguments",
                    MaxPathFieldLength);
            }
        }

        if (payload.Todos != null)
        {
            for (var index = 0; index < payload.Todos.Count; index++)
            {
                var entry = RequireEntry(payload.Todos[index], $"Todos[{index}]");
                ValidateEnum(entry.Priority, $"Todos[{index}].Priority");
                ValidateDate(entry.CreatedAt, $"Todos[{index}].CreatedAt");
                ValidateOptionalDate(entry.DueDate, $"Todos[{index}].DueDate");
                ValidateOptionalDate(entry.CompletedAt, $"Todos[{index}].CompletedAt");
                if (entry.IsCompleted != entry.CompletedAt.HasValue)
                {
                    throw new InvalidDataException(
                        $"Todos[{index}] 的 IsCompleted 与 CompletedAt 不一致。");
                }

                if (entry.CompletedAt < entry.CreatedAt)
                {
                    throw new InvalidDataException(
                        $"Todos[{index}].CompletedAt 不能早于 CreatedAt。");
                }

                ValidateRequiredField(entry.Title, $"Todos[{index}].Title");
                ValidateFieldLength(entry.Title, $"Todos[{index}].Title", MaxShortFieldLength);
            }
        }

        if (payload.QuickNotes != null)
        {
            for (var index = 0; index < payload.QuickNotes.Count; index++)
            {
                var entry = RequireEntry(payload.QuickNotes[index], $"QuickNotes[{index}]");
                ValidateDate(entry.CreatedAt, $"QuickNotes[{index}].CreatedAt");
                ValidateDate(entry.UpdatedAt, $"QuickNotes[{index}].UpdatedAt");
                if (entry.UpdatedAt < entry.CreatedAt)
                {
                    throw new InvalidDataException(
                        $"QuickNotes[{index}].UpdatedAt 不能早于 CreatedAt。");
                }

                ValidateRequiredField(entry.Title, $"QuickNotes[{index}].Title");
                ValidateRequiredField(entry.Content, $"QuickNotes[{index}].Content");
                ValidateNonNegative(entry.SortOrder, $"QuickNotes[{index}].SortOrder");
                ValidateFieldLength(entry.Title, $"QuickNotes[{index}].Title", MaxShortFieldLength);
                ValidateFieldLength(entry.Content, $"QuickNotes[{index}].Content", MaxContentFieldLength);
            }
        }

        if (payload.ClipboardHistory != null)
        {
            for (var index = 0; index < payload.ClipboardHistory.Count; index++)
            {
                var entry = RequireEntry(
                    payload.ClipboardHistory[index],
                    $"ClipboardHistory[{index}]");
                ValidateDate(entry.CreatedAt, $"ClipboardHistory[{index}].CreatedAt");
                ValidateDate(entry.LastUsedAt, $"ClipboardHistory[{index}].LastUsedAt");
                if (entry.LastUsedAt < entry.CreatedAt)
                {
                    throw new InvalidDataException(
                        $"ClipboardHistory[{index}].LastUsedAt 不能早于 CreatedAt。");
                }

                ValidatePositive(entry.UseCount, $"ClipboardHistory[{index}].UseCount");
                ValidateRequiredField(entry.Content, $"ClipboardHistory[{index}].Content");
                ValidateFieldLength(
                    entry.Content,
                    $"ClipboardHistory[{index}].Content",
                    MaxContentFieldLength);
            }
        }

        if (payload.TextSnippets != null)
        {
            for (var index = 0; index < payload.TextSnippets.Count; index++)
            {
                var entry = RequireEntry(
                    payload.TextSnippets[index],
                    $"TextSnippets[{index}]");
                ValidateDate(entry.CreatedAt, $"TextSnippets[{index}].CreatedAt");
                ValidateDate(entry.UpdatedAt, $"TextSnippets[{index}].UpdatedAt");
                ValidateOptionalDate(entry.LastUsedAt, $"TextSnippets[{index}].LastUsedAt");
                if (entry.UpdatedAt < entry.CreatedAt ||
                    entry.LastUsedAt.HasValue && entry.LastUsedAt < entry.CreatedAt)
                {
                    throw new InvalidDataException(
                        $"TextSnippets[{index}] 的日期字段顺序无效。");
                }

                ValidateNonNegative(entry.SortOrder, $"TextSnippets[{index}].SortOrder");
                ValidateNonNegative(entry.UseCount, $"TextSnippets[{index}].UseCount");
                ValidateRequiredField(entry.Title, $"TextSnippets[{index}].Title");
                ValidateRequiredField(entry.Content, $"TextSnippets[{index}].Content");
                ValidateRequiredNonWhitespace(entry.Category, $"TextSnippets[{index}].Category");
                ValidateFieldLength(entry.Title, $"TextSnippets[{index}].Title", MaxShortFieldLength);
                ValidateFieldLength(entry.Content, $"TextSnippets[{index}].Content", MaxContentFieldLength);
                ValidateFieldLength(entry.Category, $"TextSnippets[{index}].Category", MaxShortFieldLength);
            }
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

    private static void ValidateEntryCount<T>(IReadOnlyCollection<T>? entries, string section, int maximum)
    {
        if (entries != null && entries.Count > maximum)
        {
            throw new InvalidDataException(
                $"{section} 包含 {entries.Count} 条，最多允许 {maximum} 条。");
        }
    }

    private static void ValidateFieldLength(string? value, string field, int maximum)
    {
        if (value?.Length > maximum)
        {
            throw new InvalidDataException($"{field} 超过 {maximum} 个字符上限。");
        }
    }

    private static void ValidateRequiredField(string? value, string field)
    {
        if (value == null)
        {
            throw new InvalidDataException($"{field} 不能为 null。");
        }
    }

    private static T RequireEntry<T>(T? value, string field)
        where T : class
    {
        if (value == null)
        {
            throw new InvalidDataException($"{field} 不能为 null。");
        }

        return value;
    }

    private static void ValidateDate(DateTime value, string field)
    {
        if (value == default)
        {
            throw new InvalidDataException($"{field} 包含无效日期。");
        }
    }

    private static void ValidateOptionalDate(DateTime? value, string field)
    {
        if (value.HasValue)
        {
            ValidateDate(value.Value, field);
        }
    }

    private static void ValidateNonNegative(int value, string field)
    {
        if (value < 0)
        {
            throw new InvalidDataException($"{field} 不能为负数。");
        }
    }

    private static void ValidatePositive(int value, string field)
    {
        if (value <= 0)
        {
            throw new InvalidDataException($"{field} 必须大于零。");
        }
    }

    private static void ValidateRequiredNonWhitespace(string? value, string field)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidDataException($"{field} 不能为 null、空字符串或纯空白。");
        }
    }

    private static void ValidateEnum<TEnum>(TEnum? value, string field)
        where TEnum : struct, Enum
    {
        if (!value.HasValue || !Enum.IsDefined(value.Value))
        {
            throw new InvalidDataException($"{field} 包含未定义的枚举值。");
        }
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
        public List<string>? IncludedSections { get; set; }
        public bool ContainsSensitivePlaintext { get; set; }
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
        public ShortcutType? Type { get; set; }
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
            return new ShortcutItem
            {
                Name = Name,
                Path = Path,
                LaunchArguments = LaunchArguments,
                Type = Type!.Value,
                SortOrder = SortOrder,
                CreatedAt = CreatedAt,
                IconLookupPath = Path
            };
        }
    }

    private sealed class TodoBackupEntry
    {
        public string Title { get; set; } = string.Empty;
        public bool IsCompleted { get; set; }
        public DateTime? DueDate { get; set; }
        public TodoPriority? Priority { get; set; }
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
            Priority = Priority!.Value,
            CreatedAt = CreatedAt,
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
            return new QuickNote
            {
                Title = Title,
                Content = Content,
                IsPinned = IsPinned,
                SortOrder = SortOrder,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt
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
            return new TextSnippet
            {
                Title = Title,
                Content = Content,
                Category = Category,
                IsPinned = IsPinned,
                SortOrder = SortOrder,
                UseCount = UseCount,
                CreatedAt = CreatedAt,
                UpdatedAt = UpdatedAt,
                LastUsedAt = LastUsedAt
            };
        }
    }
}
