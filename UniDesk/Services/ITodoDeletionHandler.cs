using UniDesk.Models;

namespace UniDesk.Services;

public interface ITodoDeletionHandler
{
    Task<bool> ConfirmAndDeleteAsync(TodoItem? todo);
}
