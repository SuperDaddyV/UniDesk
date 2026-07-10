using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class TodoDeletionHandlerTests
{
    [Fact]
    public async Task ConfirmAndDeleteAsync_WhenCancelled_ShouldNotDelete()
    {
        var todoService = new RecordingTodoService();
        var notifications = new ConfirmationService { ConfirmResult = false };
        var handler = new TodoDeletionHandler(todoService, notifications, new TestLocalizationService());

        var deleted = await handler.ConfirmAndDeleteAsync(new TodoItem { Id = 7, Title = "Keep me" });

        Assert.False(deleted);
        Assert.Empty(todoService.DeletedIds);
    }

    [Fact]
    public async Task ConfirmAndDeleteAsync_WhenConfirmed_ShouldDelete()
    {
        var todoService = new RecordingTodoService();
        var notifications = new ConfirmationService { ConfirmResult = true };
        var handler = new TodoDeletionHandler(todoService, notifications, new TestLocalizationService());

        var deleted = await handler.ConfirmAndDeleteAsync(new TodoItem { Id = 9, Title = "Delete me" });

        Assert.True(deleted);
        Assert.Equal([9], todoService.DeletedIds);
        Assert.Contains("Delete me", notifications.LastConfirmMessage);
    }

    private sealed class RecordingTodoService : ITodoService
    {
        public List<int> DeletedIds { get; } = [];
        public Task<List<TodoItem>> GetAllTodosAsync() => Task.FromResult(new List<TodoItem>());
        public Task<TodoItem?> GetTodoAsync(int id) => Task.FromResult<TodoItem?>(null);
        public Task<int> CreateTodoAsync(TodoItem todo) => Task.FromResult(1);
        public Task UpdateTodoAsync(TodoItem todo) => Task.CompletedTask;
        public Task DeleteTodoAsync(int id)
        {
            DeletedIds.Add(id);
            return Task.CompletedTask;
        }
        public Task ToggleCompleteAsync(int id) => Task.CompletedTask;
        public Task MarkCompletedAsync(int id) => Task.CompletedTask;
        public Task MarkUncompletedAsync(int id) => Task.CompletedTask;
        public Task<List<TodoItem>> GetTodayTodosAsync() => Task.FromResult(new List<TodoItem>());
    }

    private sealed class ConfirmationService : INotificationService
    {
        public bool ConfirmResult { get; set; }
        public string LastConfirmMessage { get; private set; } = string.Empty;
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null)
        {
            LastConfirmMessage = message;
            return ConfirmResult;
        }
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        public event EventHandler? LanguageChanged;
        public string CurrentLanguage => "en-US";
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo("en-US");
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => "en-US";
        public void SetLanguage(string? language) => LanguageChanged?.Invoke(this, EventArgs.Empty);
        public string GetString(string key) => key;
        public string Format(string key, params object?[] args) => $"Delete {args[0]}?";
    }
}
