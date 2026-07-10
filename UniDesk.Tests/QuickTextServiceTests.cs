using UniDesk.Models;
using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class QuickTextServiceTests
{
    private readonly string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_quick_text.db");

    private async Task<(DatabaseService Db, SettingsService Settings, QuickTextService Service)> InitAsync()
    {
        Cleanup();
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        var settings = new SettingsService(db);
        var service = new QuickTextService(db, settings);
        return (db, settings, service);
    }

    [Fact]
    public async Task RecordClipboardTextAsync_ShouldProtectStoredContentAndReturnPlainText()
    {
        var (db, _, service) = await InitAsync();

        var recorded = await service.RecordClipboardTextAsync("hello clipboard");

        Assert.True(recorded);
        var stored = await db.QuerySingleAsync(
            "SELECT Content FROM ClipboardHistory LIMIT 1",
            reader => reader.GetString(0));
        Assert.NotNull(stored);
        Assert.StartsWith("dpapi:v1:", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("hello clipboard", stored, StringComparison.Ordinal);
        var items = await service.GetClipboardHistoryAsync();
        Assert.Single(items);
        Assert.Equal("hello clipboard", items[0].Content);

        Cleanup();
    }

    [Fact]
    public async Task GetClipboardHistoryAsync_UnreadableProtectedRow_ShouldOmitWithoutDeleting()
    {
        var (db, _, service) = await InitAsync();
        await db.ExecuteNonQueryAsync(
            "INSERT INTO ClipboardHistory (Content, ContentHash, CreatedAt, LastUsedAt, UseCount) VALUES (@p0, @p1, @p2, @p3, @p4)",
            "dpapi:v1:not-base64",
            "unreadable-hash",
            DateTime.UtcNow.ToString("o"),
            DateTime.UtcNow.ToString("o"),
            1);

        Assert.Empty(await service.GetClipboardHistoryAsync());
        var count = await db.QuerySingleAsync(
            "SELECT COUNT(*) FROM ClipboardHistory",
            reader => reader.GetInt32(0));
        Assert.Equal(1, count);
        Cleanup();
    }

    [Fact]
    public async Task RecordClipboardTextAsync_ShouldNotDuplicateLatestText()
    {
        var (_, _, service) = await InitAsync();

        Assert.True(await service.RecordClipboardTextAsync("same"));
        Assert.False(await service.RecordClipboardTextAsync("same"));

        Assert.Single(await service.GetClipboardHistoryAsync());

        Cleanup();
    }

    [Theory]
    [InlineData("123456")]
    [InlineData("password=abc")]
    [InlineData("token: abc")]
    [InlineData("Authorization: Bearer abc")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxIn0.signature")]
    public void IsSensitiveContent_ShouldDetectSensitiveText(string text)
    {
        Assert.True(QuickTextService.IsSensitiveContent(text));
    }

    [Fact]
    public async Task RecordClipboardTextAsync_ShouldFilterSensitiveContentByDefault()
    {
        var (_, _, service) = await InitAsync();

        var recorded = await service.RecordClipboardTextAsync("123456");

        Assert.False(recorded);
        Assert.Empty(await service.GetClipboardHistoryAsync());

        Cleanup();
    }

    [Fact]
    public async Task RecordClipboardTextAsync_ShouldRespectDisabledHistorySetting()
    {
        var (_, settings, service) = await InitAsync();
        settings.SetValue(QuickTextService.HistoryEnabledSettingKey, "false");
        await settings.FlushPendingSavesAsync();

        Assert.False(await service.RecordClipboardTextAsync("normal text"));
        Assert.Empty(await service.GetClipboardHistoryAsync());

        Cleanup();
    }

    [Fact]
    public async Task TrimClipboardHistoryAsync_ShouldKeepNewestItems()
    {
        var (_, settings, service) = await InitAsync();
        settings.SetValue(QuickTextService.SensitiveFilterSettingKey, "false");
        await settings.FlushPendingSavesAsync();

        await service.RecordClipboardTextAsync("item1");
        await Task.Delay(5);
        await service.RecordClipboardTextAsync("item2");
        await Task.Delay(5);
        await service.RecordClipboardTextAsync("item3");

        await service.TrimClipboardHistoryAsync(20);
        var items = await service.GetClipboardHistoryAsync();
        Assert.Equal(3, items.Count);

        await service.TrimClipboardHistoryAsync(50);
        items = await service.GetClipboardHistoryAsync();
        Assert.Equal(3, items.Count);

        Cleanup();
    }

    [Fact]
    public async Task CreateTextSnippetAsync_ShouldSaveSnippet()
    {
        var (_, _, service) = await InitAsync();

        var id = await service.CreateTextSnippetAsync(new TextSnippet
        {
            Title = "问候",
            Content = "你好",
            Category = "工作"
        });

        Assert.True(id > 0);
        var snippets = await service.GetTextSnippetsAsync();
        Assert.Single(snippets);
        Assert.Equal("问候", snippets[0].Title);

        Cleanup();
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
        }
        catch
        {
        }
    }
}
