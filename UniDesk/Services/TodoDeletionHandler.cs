using UniDesk.Models;

namespace UniDesk.Services;

public sealed class TodoDeletionHandler : ITodoDeletionHandler
{
    private readonly ITodoService _todoService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;

    public TodoDeletionHandler(
        ITodoService todoService,
        INotificationService notificationService,
        ILocalizationService localizationService)
    {
        _todoService = todoService;
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public async Task<bool> ConfirmAndDeleteAsync(TodoItem? todo)
    {
        if (todo == null) return false;
        var confirmed = _notificationService.ShowConfirmDialog(
            _localizationService.Format("Todo.DeleteConfirmFormat", todo.Title),
            _localizationService.GetString("Dialog.DeleteConfirmTitle"));
        if (!confirmed) return false;

        await _todoService.DeleteTodoAsync(todo.Id);
        return true;
    }
}
