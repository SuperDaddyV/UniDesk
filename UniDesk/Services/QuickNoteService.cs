using System.Globalization;
using Microsoft.Data.Sqlite;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public class QuickNoteService : IQuickNoteService
{
    private readonly IDatabaseService _databaseService;

    private const string SelectColumns =
        "Id, Title, Content, IsPinned, SortOrder, CreatedAt, UpdatedAt";

    public QuickNoteService(IDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public async Task<List<QuickNote>> GetAllQuickNotesAsync()
    {
        try
        {
            var notes = await _databaseService.QueryAsync(
                $"SELECT {SelectColumns} FROM QuickNotes",
                MapQuickNote);

            return Sort(notes).ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNoteService.GetAllQuickNotesAsync");
            return [];
        }
    }

    public async Task<QuickNote?> GetQuickNoteAsync(int id)
    {
        try
        {
            return await _databaseService.QuerySingleAsync(
                $"SELECT {SelectColumns} FROM QuickNotes WHERE Id = @p0",
                MapQuickNote,
                id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"QuickNoteService.GetQuickNoteAsync({id})");
            return null;
        }
    }

    public async Task<int> CreateQuickNoteAsync(QuickNote note)
    {
        try
        {
            var now = DateTime.UtcNow;
            var createdAt = note.CreatedAt == default ? now : note.CreatedAt;
            var updatedAt = note.UpdatedAt == default ? now : note.UpdatedAt;

            return await _databaseService.QuerySingleAsync(
                "INSERT INTO QuickNotes (Title, Content, IsPinned, SortOrder, CreatedAt, UpdatedAt) VALUES (@p0, @p1, @p2, @p3, @p4, @p5) RETURNING Id",
                reader => reader.GetInt32(0),
                note.Title ?? string.Empty,
                note.Content ?? string.Empty,
                note.IsPinned ? 1 : 0,
                note.SortOrder,
                createdAt.ToString("o", CultureInfo.InvariantCulture),
                updatedAt.ToString("o", CultureInfo.InvariantCulture));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "QuickNoteService.CreateQuickNoteAsync");
            return 0;
        }
    }

    public async Task<bool> UpdateQuickNoteAsync(QuickNote note)
    {
        try
        {
            var updatedAt = note.UpdatedAt == default ? DateTime.UtcNow : note.UpdatedAt;

            return await _databaseService.ExecuteNonQueryAsync(
                "UPDATE QuickNotes SET Title = @p0, Content = @p1, IsPinned = @p2, SortOrder = @p3, UpdatedAt = @p4 WHERE Id = @p5",
                note.Title ?? string.Empty,
                note.Content ?? string.Empty,
                note.IsPinned ? 1 : 0,
                note.SortOrder,
                updatedAt.ToString("o", CultureInfo.InvariantCulture),
                note.Id) > 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"QuickNoteService.UpdateQuickNoteAsync({note.Id})");
            return false;
        }
    }

    public async Task<bool> DeleteQuickNoteAsync(int id)
    {
        try
        {
            return await _databaseService.ExecuteNonQueryAsync(
                "DELETE FROM QuickNotes WHERE Id = @p0",
                id) > 0;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"QuickNoteService.DeleteQuickNoteAsync({id})");
            return false;
        }
    }

    public async Task SetPinnedAsync(int id, bool isPinned)
    {
        try
        {
            await _databaseService.ExecuteNonQueryAsync(
                "UPDATE QuickNotes SET IsPinned = @p0, UpdatedAt = @p1 WHERE Id = @p2",
                isPinned ? 1 : 0,
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture),
                id);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"QuickNoteService.SetPinnedAsync({id})");
        }
    }

    private static IEnumerable<QuickNote> Sort(IEnumerable<QuickNote> notes) =>
        notes
            .OrderByDescending(note => note.IsPinned)
            .ThenBy(note => note.IsPinned ? note.SortOrder : 0)
            .ThenByDescending(note => note.UpdatedAt == default ? note.CreatedAt : note.UpdatedAt);

    private static QuickNote MapQuickNote(SqliteDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Title = reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
        Content = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
        IsPinned = !reader.IsDBNull(3) && reader.GetInt32(3) != 0,
        SortOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4),
        CreatedAt = ParseDateTime(reader.IsDBNull(5) ? null : reader.GetString(5)) ?? DateTime.UtcNow,
        UpdatedAt = ParseDateTime(reader.IsDBNull(6) ? null : reader.GetString(6)) ?? DateTime.UtcNow
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
}
