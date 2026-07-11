using UniDesk.Models;
using UniDesk.Services;
using Xunit;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class QuickNoteServiceTests
{
    private readonly string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_quick_note.db");

    private async Task<QuickNoteService> InitAsync()
    {
        var db = new DatabaseService($"Data Source={_testDbFile}");
        await db.InitializeAsync();
        return new QuickNoteService(db);
    }

    [Fact]
    public async Task CreateQuickNoteAsync_ShouldInsertAndReturnId()
    {
        var svc = await InitAsync();

        var id = await svc.CreateQuickNoteAsync(new QuickNote
        {
            Title = "会议记录",
            Content = "明天同步进度",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        Assert.True(id > 0);
        var note = await svc.GetQuickNoteAsync(id);
        Assert.NotNull(note);
        Assert.Equal("会议记录", note!.Title);

        Cleanup();
    }

    [Fact]
    public async Task GetAllQuickNotesAsync_ShouldReturnPinnedFirstThenUpdatedDesc()
    {
        var svc = await InitAsync();
        var now = DateTime.UtcNow;

        await svc.CreateQuickNoteAsync(new QuickNote
        {
            Title = "普通旧",
            Content = "old",
            UpdatedAt = now.AddHours(-2)
        });
        await svc.CreateQuickNoteAsync(new QuickNote
        {
            Title = "普通新",
            Content = "new",
            UpdatedAt = now
        });
        await svc.CreateQuickNoteAsync(new QuickNote
        {
            Title = "置顶",
            Content = "pin",
            IsPinned = true,
            UpdatedAt = now.AddHours(-1)
        });

        var notes = await svc.GetAllQuickNotesAsync();

        Assert.Equal(["置顶", "普通新", "普通旧"], notes.Select(note => note.Title));

        Cleanup();
    }

    [Fact]
    public async Task UpdateQuickNoteAsync_ShouldUpdateFields()
    {
        var svc = await InitAsync();
        var id = await svc.CreateQuickNoteAsync(new QuickNote { Title = "旧", Content = "旧内容" });

        await svc.UpdateQuickNoteAsync(new QuickNote
        {
            Id = id,
            Title = "新",
            Content = "新内容",
            IsPinned = true,
            UpdatedAt = DateTime.UtcNow
        });

        var note = await svc.GetQuickNoteAsync(id);
        Assert.NotNull(note);
        Assert.Equal("新", note!.Title);
        Assert.Equal("新内容", note.Content);
        Assert.True(note.IsPinned);

        Cleanup();
    }

    [Fact]
    public async Task DeleteQuickNoteAsync_ShouldRemoveNote()
    {
        var svc = await InitAsync();
        var id = await svc.CreateQuickNoteAsync(new QuickNote { Title = "删除", Content = "content" });

        await svc.DeleteQuickNoteAsync(id);

        Assert.Null(await svc.GetQuickNoteAsync(id));

        Cleanup();
    }

    [Fact]
    public async Task UpdateAndDeleteMissingQuickNote_ShouldReturnFalse()
    {
        var svc = await InitAsync();

        var updated = await svc.UpdateQuickNoteAsync(new QuickNote { Id = 999, Title = "missing" });
        var deleted = await svc.DeleteQuickNoteAsync(999);

        Assert.False(updated);
        Assert.False(deleted);
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
