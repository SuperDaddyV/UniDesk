using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class SearchService : ISearchService
{
    private readonly IDatabaseService _databaseService;

    public SearchService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<IReadOnlyList<SearchResultItem>> SearchAsync(
        string keyword,
        int limitPerKind = 5,
        CancellationToken cancellationToken = default)
    {
        var trimmed = keyword.Trim();
        if (trimmed.Length == 0)
        {
            return [];
        }

        var pattern = $"%{EscapeLikePattern(trimmed)}%";
        var limit = Math.Clamp(limitPerKind, 1, 20);
        var results = new List<SearchResultItem>(limit * 5);

        await AddResultsAsync(results, SearchQuickNotesAsync(pattern, trimmed, limit), cancellationToken, "QuickNotes");
        await AddResultsAsync(results, SearchTodosAsync(pattern, trimmed, limit), cancellationToken, "Todos");
        await AddResultsAsync(results, SearchClipboardAsync(pattern, trimmed, limit), cancellationToken, "ClipboardHistory");
        await AddResultsAsync(results, SearchSnippetsAsync(pattern, trimmed, limit), cancellationToken, "TextSnippets");
        await AddResultsAsync(results, SearchShortcutsAsync(pattern, trimmed, limit), cancellationToken, "Shortcuts");
        return results;
    }

    private static async Task AddResultsAsync(
        ICollection<SearchResultItem> destination,
        Task<List<SearchResultItem>> query,
        CancellationToken cancellationToken,
        string source)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            foreach (var result in await query)
            {
                destination.Add(result);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"SearchService.{source}");
        }
    }

    private Task<List<SearchResultItem>> SearchQuickNotesAsync(string pattern, string keyword, int limit) =>
        _databaseService.QueryAsync(
            @"SELECT Id, Title, Content
              FROM QuickNotes
              WHERE Title LIKE @p0 ESCAPE '\' OR Content LIKE @p0 ESCAPE '\'
              ORDER BY IsPinned DESC, UpdatedAt DESC
              LIMIT @p1",
            reader => new SearchResultItem(
                SearchResultKind.QuickNote,
                reader.GetInt32(0),
                reader.GetString(1),
                BuildSnippet(reader.GetString(2), keyword),
                string.Empty),
            pattern,
            limit);

    private Task<List<SearchResultItem>> SearchTodosAsync(string pattern, string keyword, int limit) =>
        _databaseService.QueryAsync(
            @"SELECT Id, Title, IsCompleted
              FROM Todos
              WHERE Title LIKE @p0 ESCAPE '\'
              ORDER BY IsCompleted ASC, DueDate IS NULL, DueDate ASC, CreatedAt DESC
              LIMIT @p1",
            reader =>
            {
                var title = reader.GetString(1);
                return new SearchResultItem(
                    SearchResultKind.Todo,
                    reader.GetInt32(0),
                    title,
                    BuildSnippet(title, keyword),
                    string.Empty);
            },
            pattern,
            limit);

    private Task<List<SearchResultItem>> SearchClipboardAsync(string pattern, string keyword, int limit) =>
        _databaseService.QueryAsync(
            @"SELECT Id, Content
              FROM ClipboardHistory
              WHERE Content LIKE @p0 ESCAPE '\'
              ORDER BY LastUsedAt DESC
              LIMIT @p1",
            reader =>
            {
                var content = reader.GetString(1);
                return new SearchResultItem(
                    SearchResultKind.Clipboard,
                    reader.GetInt32(0),
                    ClipboardHistoryItem.BuildDisplayText(content),
                    BuildSnippet(content, keyword),
                    content);
            },
            pattern,
            limit);

    private Task<List<SearchResultItem>> SearchSnippetsAsync(string pattern, string keyword, int limit) =>
        _databaseService.QueryAsync(
            @"SELECT Id, Title, Content
              FROM TextSnippets
              WHERE Title LIKE @p0 ESCAPE '\' OR Content LIKE @p0 ESCAPE '\'
              ORDER BY IsPinned DESC, LastUsedAt DESC, SortOrder ASC
              LIMIT @p1",
            reader =>
            {
                var title = reader.GetString(1);
                var content = reader.GetString(2);
                return new SearchResultItem(
                    SearchResultKind.Snippet,
                    reader.GetInt32(0),
                    string.IsNullOrWhiteSpace(title) ? ClipboardHistoryItem.BuildDisplayText(content) : title,
                    BuildSnippet(content, keyword),
                    content);
            },
            pattern,
            limit);

    private Task<List<SearchResultItem>> SearchShortcutsAsync(string pattern, string keyword, int limit) =>
        _databaseService.QueryAsync(
            @"SELECT Id, Name, Path
              FROM Shortcuts
              WHERE Name LIKE @p0 ESCAPE '\' OR Path LIKE @p0 ESCAPE '\'
              ORDER BY SortOrder ASC, CreatedAt DESC
              LIMIT @p1",
            reader => new SearchResultItem(
                SearchResultKind.Shortcut,
                reader.GetInt32(0),
                reader.GetString(1),
                BuildSnippet(reader.GetString(2), keyword),
                reader.GetString(2)),
            pattern,
            limit);

    public static string EscapeLikePattern(string keyword) => keyword
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal);

    public static string BuildSnippet(string? value, string keyword, int radius = 30)
    {
        var text = string.Join(" ", (value ?? string.Empty)
            .Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        if (text.Length == 0)
        {
            return string.Empty;
        }

        var match = text.IndexOf(keyword, StringComparison.CurrentCultureIgnoreCase);
        if (match < 0)
        {
            return text.Length <= radius * 2 ? text : text[..(radius * 2)] + "…";
        }

        var start = Math.Max(0, match - radius);
        var end = Math.Min(text.Length, match + keyword.Length + radius);
        if (start > 0 && char.IsLowSurrogate(text[start]) && char.IsHighSurrogate(text[start - 1]))
        {
            start--;
        }
        if (end < text.Length && end > 0 && char.IsHighSurrogate(text[end - 1]) && char.IsLowSurrogate(text[end]))
        {
            end++;
        }

        return (start > 0 ? "…" : string.Empty) +
               text[start..end] +
               (end < text.Length ? "…" : string.Empty);
    }
}
