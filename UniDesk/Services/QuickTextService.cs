using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public partial class QuickTextService : IQuickTextService
{
    public const int MaxContentLength = 5000;
    public static readonly int[] AllowedHistoryLimits = [20, 50, 100, 200];
    public const int DefaultHistoryLimit = 50;

    public const string HistoryEnabledSettingKey = "ClipboardHistoryEnabled";
    public const string SensitiveFilterSettingKey = "ClipboardSensitiveFilterEnabled";
    public const string HistoryMaxCountSettingKey = "ClipboardHistoryMaxCount";

    private readonly IDatabaseService _databaseService;
    private readonly ISettingsService _settingsService;
    private readonly IUserDataProtector _userDataProtector;

    private const string HistoryColumns =
        "Id, Content, ContentHash, CreatedAt, LastUsedAt, UseCount";

    private const string SnippetColumns =
        "Id, Title, Content, Category, IsPinned, SortOrder, UseCount, CreatedAt, UpdatedAt, LastUsedAt";

    private const string TrimHistorySql =
        """
        DELETE FROM ClipboardHistory
        WHERE Id NOT IN (
            SELECT Id FROM ClipboardHistory
            ORDER BY LastUsedAt DESC
            LIMIT @p0
        )
        """;

    public QuickTextService(IDatabaseService databaseService, ISettingsService settingsService)
        : this(databaseService, settingsService, new DpapiUserDataProtector())
    {
    }

    public QuickTextService(
        IDatabaseService databaseService,
        ISettingsService settingsService,
        IUserDataProtector userDataProtector)
    {
        _databaseService = databaseService;
        _settingsService = settingsService;
        _userDataProtector = userDataProtector;
    }

    public async Task<List<ClipboardHistoryItem>> GetClipboardHistoryAsync(int? limit = null)
    {
        var take = Math.Max(1, limit ?? GetHistoryMaxCount());
        var items = await _databaseService.QueryAsync<ClipboardHistoryItem?>(
            $"SELECT {HistoryColumns} FROM ClipboardHistory ORDER BY LastUsedAt DESC LIMIT @p0",
            TryMapHistory,
            take);
        return items.OfType<ClipboardHistoryItem>().ToList();
    }

    public async Task<List<TextSnippet>> GetTextSnippetsAsync()
    {
        var snippets = await _databaseService.QueryAsync(
                $"SELECT {SnippetColumns} FROM TextSnippets",
                MapSnippet);

        return snippets
            .OrderByDescending(snippet => snippet.IsPinned)
            .ThenBy(snippet => snippet.SortOrder)
            .ThenByDescending(snippet => snippet.LastUsedAt ?? snippet.UpdatedAt)
            .ToList();
    }

    public async Task<bool> RecordClipboardTextAsync(string? text)
    {
        if (!_settingsService.GetSetting(HistoryEnabledSettingKey, true))
        {
            return false;
        }

        var normalized = NormalizeClipboardText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        if (_settingsService.GetSetting(SensitiveFilterSettingKey, true) && IsSensitiveContent(normalized))
        {
            Logger.LogInfo("QuickTextService.RecordClipboardTextAsync: filtered sensitive clipboard text.");
            return false;
        }

        var hash = ComputeHash(normalized);
        var now = DateTime.UtcNow;
        var safeMax = NormalizeHistoryLimit(GetHistoryMaxCount());

        return await _databaseService.ExecuteInTransactionAsync(async session =>
        {
            var latest = await session.QuerySingleAsync(
                "SELECT Id, ContentHash FROM ClipboardHistory ORDER BY LastUsedAt DESC LIMIT 1",
                MapHistoryIdentity);

            if (string.Equals(latest?.ContentHash, hash, StringComparison.Ordinal))
            {
                return false;
            }

            var existing = await session.QuerySingleAsync(
                "SELECT Id, ContentHash FROM ClipboardHistory WHERE ContentHash = @p0",
                MapHistoryIdentity,
                hash);

            int affected;
            if (existing != null)
            {
                affected = await session.ExecuteNonQueryAsync(
                    "UPDATE ClipboardHistory SET LastUsedAt = @p0, UseCount = UseCount + 1 WHERE Id = @p1",
                    now.ToString("o", CultureInfo.InvariantCulture),
                    existing.Id);
            }
            else
            {
                affected = await session.ExecuteNonQueryAsync(
                    "INSERT INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt, UseCount) VALUES (@p0, @p1, @p2, @p3, @p4)",
                    _userDataProtector.Protect(normalized),
                    hash,
                    now.ToString("o", CultureInfo.InvariantCulture),
                    now.ToString("o", CultureInfo.InvariantCulture),
                    1);
            }

            if (affected != 1)
            {
                throw new InvalidOperationException("剪贴板历史未能写入数据库。");
            }

            await session.ExecuteNonQueryAsync(TrimHistorySql, safeMax);
            return true;
        });
    }

    public async Task DeleteClipboardHistoryAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync("DELETE FROM ClipboardHistory WHERE Id = @p0", id);
        if (affected != 1)
        {
            throw new InvalidOperationException($"剪贴板历史 {id} 不存在或未能删除。");
        }
    }

    public async Task ClearClipboardHistoryAsync()
    {
        await _databaseService.ExecuteNonQueryAsync("DELETE FROM ClipboardHistory");
    }

    public async Task TrimClipboardHistoryAsync(int maxCount)
    {
        var safeMax = NormalizeHistoryLimit(maxCount);
        await _databaseService.ExecuteNonQueryAsync(TrimHistorySql, safeMax);
    }

    public async Task<int> CreateTextSnippetAsync(TextSnippet snippet)
    {
        var now = DateTime.UtcNow;
            var createdAt = snippet.CreatedAt == default ? now : snippet.CreatedAt;
            var updatedAt = snippet.UpdatedAt == default ? now : snippet.UpdatedAt;

        var id = await _databaseService.QuerySingleAsync(
                "INSERT INTO TextSnippets (Title, Content, Category, IsPinned, SortOrder, UseCount, CreatedAt, UpdatedAt, LastUsedAt) VALUES (@p0, @p1, @p2, @p3, @p4, @p5, @p6, @p7, @p8) RETURNING Id",
                reader => reader.GetInt32(0),
                snippet.Title ?? string.Empty,
                snippet.Content ?? string.Empty,
                NormalizeCategory(snippet.Category),
                snippet.IsPinned ? 1 : 0,
                snippet.SortOrder,
                Math.Max(0, snippet.UseCount),
                createdAt.ToString("o", CultureInfo.InvariantCulture),
                updatedAt.ToString("o", CultureInfo.InvariantCulture),
                snippet.LastUsedAt?.ToString("o", CultureInfo.InvariantCulture));
        if (id <= 0)
        {
            throw new InvalidOperationException("快捷文本未能写入数据库。");
        }

        return id;
    }

    public async Task UpdateTextSnippetAsync(TextSnippet snippet)
    {
        var updatedAt = DateTime.UtcNow;
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE TextSnippets SET Title = @p0, Content = @p1, Category = @p2, IsPinned = @p3, SortOrder = @p4, UpdatedAt = @p5 WHERE Id = @p6",
                snippet.Title ?? string.Empty,
                snippet.Content ?? string.Empty,
                NormalizeCategory(snippet.Category),
                snippet.IsPinned ? 1 : 0,
                snippet.SortOrder,
                updatedAt.ToString("o", CultureInfo.InvariantCulture),
                snippet.Id);
        if (affected != 1)
        {
            throw new InvalidOperationException($"快捷文本 {snippet.Id} 不存在或未能更新。");
        }
    }

    public async Task DeleteTextSnippetAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync("DELETE FROM TextSnippets WHERE Id = @p0", id);
        if (affected != 1)
        {
            throw new InvalidOperationException($"快捷文本 {id} 不存在或未能删除。");
        }
    }

    public async Task<TextSnippet?> CreateSnippetFromHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Content))
        {
            return null;
        }

        var title = BuildTitle(item.Content);
        var snippet = new TextSnippet
        {
            Title = title,
            Content = item.Content,
            Category = "默认",
            SortOrder = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var id = await CreateTextSnippetAsync(snippet);
        if (id <= 0)
        {
            return null;
        }

        snippet.Id = id;
        return snippet;
    }

    public async Task MarkSnippetUsedAsync(int id)
    {
        var affected = await _databaseService.ExecuteNonQueryAsync(
                "UPDATE TextSnippets SET UseCount = UseCount + 1, LastUsedAt = @p0 WHERE Id = @p1",
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                id);
        if (affected != 1)
        {
            throw new InvalidOperationException($"快捷文本 {id} 不存在或未能更新使用次数。");
        }
    }

    public static int NormalizeHistoryLimit(int value) =>
        AllowedHistoryLimits.Contains(value) ? value : DefaultHistoryLimit;

    public static bool IsSensitiveContent(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (VerificationCodeRegex().IsMatch(trimmed) ||
            ChinaIdRegex().IsMatch(trimmed) ||
            BankCardRegex().IsMatch(trimmed) ||
            JwtRegex().IsMatch(trimmed) ||
            OpenAiKeyRegex().IsMatch(trimmed) ||
            GitHubClassicTokenRegex().IsMatch(trimmed) ||
            GitHubFineGrainedTokenRegex().IsMatch(trimmed) ||
            AwsAccessKeyRegex().IsMatch(trimmed) ||
            PrivateKeyHeaderRegex().IsMatch(trimmed) ||
            LabeledSecretRegex().IsMatch(trimmed) ||
            BearerTokenRegex().IsMatch(trimmed))
        {
            return true;
        }

        return false;
    }

    internal static string NormalizeClipboardText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var normalized = text.Trim();
        return normalized.Length <= MaxContentLength
            ? normalized
            : normalized[..MaxContentLength];
    }

    internal static string ComputeHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes);
    }

    private int GetHistoryMaxCount() =>
        NormalizeHistoryLimit(_settingsService.GetSetting(HistoryMaxCountSettingKey, DefaultHistoryLimit));

    private static string BuildTitle(string content)
    {
        var title = ClipboardHistoryItem.BuildDisplayText(content);
        return title.Length <= 40 ? title : title[..40];
    }

    private static string NormalizeCategory(string? category) =>
        string.IsNullOrWhiteSpace(category) ? "默认" : category.Trim();

    private ClipboardHistoryItem? TryMapHistory(SqliteDataReader reader)
    {
        var id = reader.GetInt32(0);
        var storedContent = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
        string content;
        if (_userDataProtector.IsProtected(storedContent))
        {
            if (!_userDataProtector.TryUnprotect(storedContent, out content))
            {
                Logger.LogWarning(
                    $"剪贴板历史 ID {id} 无法由当前 Windows 用户解密，已从显示结果中省略。",
                    "QuickTextService.GetClipboardHistoryAsync");
                return null;
            }
        }
        else
        {
            content = storedContent;
        }

        return new ClipboardHistoryItem
        {
            Id = id,
            Content = content,
            ContentHash = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            CreatedAt = ParseDateTime(reader.IsDBNull(3) ? null : reader.GetString(3)) ?? DateTime.UtcNow,
            LastUsedAt = ParseDateTime(reader.IsDBNull(4) ? null : reader.GetString(4)) ?? DateTime.UtcNow,
            UseCount = reader.IsDBNull(5) ? 0 : reader.GetInt32(5)
        };
    }

    private static HistoryIdentity MapHistoryIdentity(SqliteDataReader reader) =>
        new(reader.GetInt32(0), reader.IsDBNull(1) ? string.Empty : reader.GetString(1));

    private sealed record HistoryIdentity(int Id, string ContentHash);

    private static TextSnippet MapSnippet(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        Content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        Category = reader.IsDBNull(3) ? "默认" : reader.GetString(3),
        IsPinned = !reader.IsDBNull(4) && reader.GetInt32(4) != 0,
        SortOrder = reader.IsDBNull(5) ? 0 : reader.GetInt32(5),
        UseCount = reader.IsDBNull(6) ? 0 : reader.GetInt32(6),
        CreatedAt = ParseDateTime(reader.IsDBNull(7) ? null : reader.GetString(7)) ?? DateTime.UtcNow,
        UpdatedAt = ParseDateTime(reader.IsDBNull(8) ? null : reader.GetString(8)) ?? DateTime.UtcNow,
        LastUsedAt = ParseDateTime(reader.IsDBNull(9) ? null : reader.GetString(9))
    };

    private static DateTime? ParseDateTime(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind,
            out var parsed)
            ? parsed
            : null;
    }

    [GeneratedRegex(@"^\d{4,8}$")]
    private static partial Regex VerificationCodeRegex();

    [GeneratedRegex(@"^\d{17}[\dXx]$")]
    private static partial Regex ChinaIdRegex();

    [GeneratedRegex(@"^\d{13,19}$")]
    private static partial Regex BankCardRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+\.[A-Za-z0-9_-]+$")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"^sk-(?:proj-)?[A-Za-z0-9_-]{20,}$")]
    private static partial Regex OpenAiKeyRegex();

    [GeneratedRegex(@"^ghp_[A-Za-z0-9]{30,}$")]
    private static partial Regex GitHubClassicTokenRegex();

    [GeneratedRegex(@"^github_pat_[A-Za-z0-9_]{20,}$")]
    private static partial Regex GitHubFineGrainedTokenRegex();

    [GeneratedRegex(@"^AKIA[0-9A-Z]{16}$")]
    private static partial Regex AwsAccessKeyRegex();

    [GeneratedRegex(@"^-----BEGIN (?:[A-Z0-9]+ )?PRIVATE KEY-----", RegexOptions.Multiline)]
    private static partial Regex PrivateKeyHeaderRegex();

    [GeneratedRegex(
        @"(?:^|[^a-z0-9_])(?:password|passwd|token|api[_-]?key|apikey|secret|authorization|cookie|session)\s*[:=]\s*\S+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex LabeledSecretRegex();

    [GeneratedRegex(
        @"(?:^|\s)bearer\s+[A-Za-z0-9._~-]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex BearerTokenRegex();
}
