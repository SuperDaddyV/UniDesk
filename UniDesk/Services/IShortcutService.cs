using UniDesk.Models;

namespace UniDesk.Services;

public interface IShortcutService
{
    Task<List<ShortcutItem>> GetAllShortcutsAsync();
    Task<ShortcutItem?> GetShortcutAsync(int id);
    Task<int> CreateShortcutAsync(ShortcutItem shortcut);
    Task UpdateShortcutAsync(ShortcutItem shortcut);
    Task DeleteShortcutAsync(int id);
    Task UpdateSortOrderAsync(List<int> ids);
    Task NormalizeSortOrderAsync();
    Task RefreshMissingIconsAsync();
    Task LaunchShortcutAsync(int id);
}
