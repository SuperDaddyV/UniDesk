using Xunit;
using UniDesk.Services;
using UniDesk.Models;
using UniDesk.Helpers;
using System.IO;

namespace UniDesk.Tests;

[Collection("Database Tests")]
public class NoteServiceTests
{
    private string _testDbFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "test_note.db");

    private async Task<(DatabaseService db, NoteService svc)> InitAsync()
    {
        var connectionString = $"Data Source={_testDbFile}";
        var db = new DatabaseService(connectionString);
        await db.InitializeAsync();
        var svc = new NoteService(db);
        return (db, svc);
    }

    [Fact]
    public async Task CreateNoteAsync_ShouldInsertAndReturnId()
    {
        var (db, svc) = await InitAsync();

        var note = new NoteItem
        {
            Title = "Test Note",
            Content = "Hello world",
            Color = "#FFFFFF",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateNoteAsync(note);
        Assert.True(id > 0);

        Cleanup();
    }

    [Fact]
    public async Task CreateNoteAsync_ShouldDefaultTitleAndContent()
    {
        var (db, svc) = await InitAsync();

        var note = new NoteItem
        {
            Title = null!,
            Content = null!,
            Color = "",
            CreatedAt = default,
            UpdatedAt = default
        };

        var id = await svc.CreateNoteAsync(note);
        Assert.True(id > 0);

        var fetched = await svc.GetNoteAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal(string.Empty, fetched!.Title);
        Assert.Equal(string.Empty, fetched.Content);
        Assert.Equal("#FFFFFF", fetched.Color);

        Cleanup();
    }

    [Fact]
    public async Task GetNoteAsync_ShouldReturnInsertedNote()
    {
        var (db, svc) = await InitAsync();

        var note = new NoteItem
        {
            Title = "Fetch Test",
            Content = "Content here",
            Color = "#FF0000",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateNoteAsync(note);
        var fetched = await svc.GetNoteAsync(id);

        Assert.NotNull(fetched);
        Assert.Equal("Fetch Test", fetched!.Title);
        Assert.Equal("Content here", fetched.Content);
        Assert.Equal("#FF0000", fetched.Color);
        Assert.Equal(id, fetched.Id);

        Cleanup();
    }

    [Fact]
    public async Task GetNoteAsync_ShouldReturnNullForNonExistent()
    {
        var (db, svc) = await InitAsync();

        var result = await svc.GetNoteAsync(9999);
        Assert.Null(result);

        Cleanup();
    }

    [Fact]
    public async Task GetAllNotesAsync_ShouldReturnAllOrderedByUpdatedAtDesc()
    {
        var (db, svc) = await InitAsync();

        var now = DateTime.UtcNow;

        await svc.CreateNoteAsync(new NoteItem
        {
            Title = "Old Note", Content = "c1", Color = "#FFFFFF",
            CreatedAt = now.AddHours(-2), UpdatedAt = now.AddHours(-1)
        });

        await svc.CreateNoteAsync(new NoteItem
        {
            Title = "New Note", Content = "c2", Color = "#FFFFFF",
            CreatedAt = now, UpdatedAt = now
        });

        var notes = await svc.GetAllNotesAsync();
        Assert.Equal(2, notes.Count);
        Assert.Equal("New Note", notes[0].Title);
        Assert.Equal("Old Note", notes[1].Title);

        Cleanup();
    }

    [Fact]
    public async Task GetAllNotesAsync_ShouldReturnEmptyListWhenNone()
    {
        var (db, svc) = await InitAsync();

        var notes = await svc.GetAllNotesAsync();
        Assert.Empty(notes);

        Cleanup();
    }

    [Fact]
    public async Task UpdateNoteAsync_ShouldUpdateFields()
    {
        var (db, svc) = await InitAsync();

        var note = new NoteItem
        {
            Title = "Original", Content = "Original content", Color = "#FFFFFF",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateNoteAsync(note);

        var updated = new NoteItem
        {
            Id = id,
            Title = "Updated Title",
            Content = "Updated content",
            Color = "#00FF00",
            CreatedAt = note.CreatedAt,
            UpdatedAt = DateTime.UtcNow
        };

        await svc.UpdateNoteAsync(updated);

        var fetched = await svc.GetNoteAsync(id);
        Assert.NotNull(fetched);
        Assert.Equal("Updated Title", fetched!.Title);
        Assert.Equal("Updated content", fetched.Content);
        Assert.Equal("#00FF00", fetched.Color);

        Cleanup();
    }

    [Fact]
    public async Task DeleteNoteAsync_ShouldRemoveNote()
    {
        var (db, svc) = await InitAsync();

        var note = new NoteItem
        {
            Title = "To Delete", Content = "will be deleted", Color = "#FFFFFF",
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        var id = await svc.CreateNoteAsync(note);
        Assert.True(id > 0);

        await svc.DeleteNoteAsync(id);

        var fetched = await svc.GetNoteAsync(id);
        Assert.Null(fetched);

        Cleanup();
    }

    [Fact]
    public async Task DeleteNoteAsync_ShouldRejectNonExistentTarget()
    {
        var (db, svc) = await InitAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => svc.DeleteNoteAsync(9999));

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
