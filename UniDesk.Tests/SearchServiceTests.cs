using UniDesk.Models;
using UniDesk.Services;
using Microsoft.Data.Sqlite;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class SearchServiceTests : IDisposable
{
    private readonly string _databasePath = Path.Combine(
        AppContext.BaseDirectory,
        $"search-{Guid.NewGuid():N}.db");

    [Fact]
    public void EscapeLikePattern_ShouldTreatWildcardsAsLiteralText()
    {
        Assert.Equal(@"50\%\_done\\", SearchService.EscapeLikePattern(@"50%_done\"));
    }

    [Fact]
    public void BuildSnippet_ShouldPreserveUnicodeAroundMatch()
    {
        var snippet = SearchService.BuildSnippet("开头😀中间关键字结尾", "关键字", 4);

        Assert.Contains("关键字", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain('\uFFFD', snippet);
    }

    [Fact]
    public async Task SearchAsync_ShouldReturnAllFiveKinds()
    {
        var database = await CreateDatabaseAsync();
        var now = DateTime.Now.ToString("O");
        await database.ExecuteNonQueryAsync(
            "INSERT INTO QuickNotes (Title, Content, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3)",
            "项目 Alpha", "便签内容", now, now);
        await database.ExecuteNonQueryAsync(
            "INSERT INTO Todos (Title, IsCompleted, CreatedAt, Priority) VALUES (@p0, 0, @p1, 1)",
            "完成 Alpha 检查", now);
        await database.ExecuteNonQueryAsync(
            "INSERT INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt) VALUES (@p0, @p1, @p2, @p3)",
            "复制 Alpha 文本", Guid.NewGuid().ToString("N"), now, now);
        await database.ExecuteNonQueryAsync(
            "INSERT INTO TextSnippets (Title, Content, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3)",
            "Alpha 短语", "短语内容", now, now);
        await database.ExecuteNonQueryAsync(
            "INSERT INTO Shortcuts (Name, Path, CreatedAt) VALUES (@p0, @p1, @p2)",
            "Alpha 工具", @"C:\Tools\alpha.exe", now);

        var results = await new SearchService(database).SearchAsync("Alpha");

        Assert.Equal(5, results.Select(result => result.Kind).Distinct().Count());
        Assert.Contains(results, result => result.Kind == SearchResultKind.QuickNote);
        Assert.Contains(results, result => result.Kind == SearchResultKind.Todo);
        Assert.Contains(results, result => result.Kind == SearchResultKind.Clipboard);
        Assert.Contains(results, result => result.Kind == SearchResultKind.Snippet);
        Assert.Contains(results, result => result.Kind == SearchResultKind.Shortcut);
    }

    [Fact]
    public async Task SearchAsync_ShouldTreatPercentAndUnderscoreLiterally()
    {
        var database = await CreateDatabaseAsync();
        var now = DateTime.Now.ToString("O");
        await database.ExecuteNonQueryAsync(
            "INSERT INTO QuickNotes (Title, Content, CreatedAt, UpdatedAt) VALUES (@p0, '', @p1, @p2)",
            "预算 50%_done", now, now);
        await database.ExecuteNonQueryAsync(
            "INSERT INTO QuickNotes (Title, Content, CreatedAt, UpdatedAt) VALUES (@p0, '', @p1, @p2)",
            "预算 50XXdone", now, now);

        var results = await new SearchService(database).SearchAsync("50%_done");

        var result = Assert.Single(results);
        Assert.Equal("预算 50%_done", result.Title);
    }

    private async Task<DatabaseService> CreateDatabaseAsync()
    {
        var database = new DatabaseService($"Data Source={_databasePath}");
        await database.InitializeAsync();
        return database;
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var path in new[] { _databasePath, _databasePath + "-wal", _databasePath + "-shm" })
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
