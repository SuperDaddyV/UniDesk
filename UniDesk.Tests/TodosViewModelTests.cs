using System.Globalization;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;
using Xunit;

namespace UniDesk.Tests;

public class TodosViewModelTests
{
    [Fact]
    public async Task ReloadAsync_ShouldSortAndSelectFirstIncompleteTodo()
    {
        var completed = new TodoItem { Id = 1, Title = "completed", IsCompleted = true, Priority = TodoPriority.High };
        var low = new TodoItem { Id = 2, Title = "low", Priority = TodoPriority.Low };
        var high = new TodoItem { Id = 3, Title = "high", Priority = TodoPriority.High };
        var service = new FakeTodoService([completed, low, high]);
        var viewModel = CreateViewModel(service, new FakeDeletionHandler());

        await viewModel.ReloadAsync();

        Assert.Equal(["high", "low", "completed"], viewModel.Todos.Select(todo => todo.Title));
        Assert.Same(high, viewModel.CollapsedPanelTodo);
        Assert.True(viewModel.HasCollapsedPanelTodo);
    }

    [Fact]
    public async Task ToggleTodoCommand_ShouldToggleAndReload()
    {
        var todo = new TodoItem { Id = 7, Title = "toggle" };
        var service = new FakeTodoService([todo]);
        var viewModel = CreateViewModel(service, new FakeDeletionHandler());
        await viewModel.ReloadAsync();

        await viewModel.ToggleTodoCommand.ExecuteAsync(todo);

        Assert.Equal([7], service.ToggledIds);
        Assert.True(todo.IsCompleted);
        Assert.Equal(2, service.LoadCount);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 2)]
    public async Task DeleteTodoCommand_ShouldReloadOnlyAfterConfirmedDeletion(
        bool confirmed,
        int expectedLoadCount)
    {
        var todo = new TodoItem { Id = 9, Title = "delete" };
        var service = new FakeTodoService([todo]);
        var deletion = new FakeDeletionHandler { Result = confirmed };
        var viewModel = CreateViewModel(service, deletion);
        await viewModel.ReloadAsync();

        await viewModel.DeleteTodoCommand.ExecuteAsync(todo);

        Assert.Same(todo, deletion.LastTodo);
        Assert.Equal(expectedLoadCount, service.LoadCount);
    }

    private static TodosViewModel CreateViewModel(
        ITodoService todoService,
        ITodoDeletionHandler deletionHandler) =>
        new(
            todoService,
            deletionHandler,
            new NoOpNotificationService(),
            new TestLocalizationService(),
            () => 360);

    private sealed class FakeTodoService(List<TodoItem> todos) : ITodoService
    {
        public int LoadCount { get; private set; }
        public List<int> ToggledIds { get; } = [];
        public Task<List<TodoItem>> GetAllTodosAsync()
        {
            LoadCount++;
            return Task.FromResult(todos.ToList());
        }

        public Task<TodoItem?> GetTodoAsync(int id) => Task.FromResult(todos.FirstOrDefault(todo => todo.Id == id));
        public Task<int> CreateTodoAsync(TodoItem todo) => Task.FromResult(1);
        public Task UpdateTodoAsync(TodoItem todo) => Task.CompletedTask;
        public Task DeleteTodoAsync(int id) => Task.CompletedTask;
        public Task ToggleCompleteAsync(int id)
        {
            ToggledIds.Add(id);
            var todo = todos.First(item => item.Id == id);
            todo.IsCompleted = !todo.IsCompleted;
            return Task.CompletedTask;
        }
        public Task MarkCompletedAsync(int id) => Task.CompletedTask;
        public Task MarkUncompletedAsync(int id) => Task.CompletedTask;
        public Task<List<TodoItem>> GetTodayTodosAsync() => Task.FromResult(todos.ToList());
    }

    private sealed class FakeDeletionHandler : ITodoDeletionHandler
    {
        public bool Result { get; set; }
        public TodoItem? LastTodo { get; private set; }
        public Task<bool> ConfirmAndDeleteAsync(TodoItem? todo)
        {
            LastTodo = todo;
            return Task.FromResult(Result);
        }
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => false;
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
        public string Format(string key, params object?[] args) => string.Format(CultureInfo.InvariantCulture, key, args);
    }
}
