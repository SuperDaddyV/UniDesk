using UniDesk.Models;

namespace UniDesk.Services;

public interface IQuickNoteService
{
    Task<List<QuickNote>> GetAllQuickNotesAsync();
    Task<QuickNote?> GetQuickNoteAsync(int id);
    Task<int> CreateQuickNoteAsync(QuickNote note);
    Task<bool> UpdateQuickNoteAsync(QuickNote note);
    Task<bool> DeleteQuickNoteAsync(int id);
    Task SetPinnedAsync(int id, bool isPinned);
}
