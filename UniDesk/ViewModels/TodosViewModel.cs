using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class TodosViewModel : ObservableObject
{
    [ObservableProperty]
    private int? _highlightedTodoId;
    private readonly ITodoService _todoService;
    private readonly ITodoDeletionHandler _todoDeletionHandler;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly Func<double> _getPanelWidth;
    private int _loadGeneration;

    public ObservableCollection<TodoItem> Todos { get; } = [];

    [ObservableProperty]
    private TodoItem? _collapsedPanelTodo;

    [ObservableProperty]
    private string _collapsedPanelTodoDueText = string.Empty;

    public bool HasCollapsedPanelTodo => CollapsedPanelTodo != null;

    public TodosViewModel(
        ITodoService todoService,
        ITodoDeletionHandler todoDeletionHandler,
        INotificationService notificationService,
        ILocalizationService localizationService,
        Func<double> getPanelWidth)
    {
        _todoService = todoService;
        _todoDeletionHandler = todoDeletionHandler;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _getPanelWidth = getPanelWidth;
    }

    partial void OnCollapsedPanelTodoChanged(TodoItem? value) =>
        OnPropertyChanged(nameof(HasCollapsedPanelTodo));

    [RelayCommand]
    private async Task AddTodoAsync()
    {
        var window = new TodoEditWindow(
            new TodoEditViewModel(_todoService, _localizationService),
            _getPanelWidth())
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task EditTodoAsync(TodoItem? todo)
    {
        if (todo == null) return;

        var window = new TodoEditWindow(
            new TodoEditViewModel(_todoService, _localizationService, todo),
            _getPanelWidth())
        {
            Owner = App.Current.MainWindow
        };
        if (window.ShowDialog() == true)
        {
            await ReloadAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleTodoAsync(TodoItem? todo)
    {
        if (todo == null) return;
        try
        {
            await _todoService.ToggleCompleteAsync(todo.Id);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, $"TodosViewModel.ToggleTodoAsync({todo.Id})");
            _notificationService.ShowWarningMessage(L("Common.OperationFailed"));
        }
    }

    [RelayCommand]
    private async Task DeleteTodoAsync(TodoItem? todo)
    {
        if (await _todoDeletionHandler.ConfirmAndDeleteAsync(todo))
        {
            await ReloadAsync();
        }
    }

    public async Task ReloadAsync()
    {
        var generation = Interlocked.Increment(ref _loadGeneration);
        try
        {
            var todos = await _todoService.GetAllTodosAsync();
            if (generation != _loadGeneration) return;

            void Apply()
            {
                Todos.Clear();
                foreach (var todo in TodoSortHelper.Sort(todos))
                {
                    Todos.Add(todo);
                }

                RefreshCollapsedPanelTodo();
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                Apply();
            }
            else
            {
                await dispatcher.InvokeAsync(Apply);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TodosViewModel.ReloadAsync");
            if (generation == _loadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Todo.LoadFailed"));
            }
        }
    }

    public async Task HighlightSearchResultAsync(int todoId)
    {
        if (Todos.All(todo => todo.Id != todoId))
        {
            await ReloadAsync();
        }

        HighlightedTodoId = todoId;
        _ = ClearSearchHighlightAsync(todoId);
    }

    private async Task ClearSearchHighlightAsync(int todoId)
    {
        await Task.Delay(TimeSpan.FromSeconds(2));
        if (HighlightedTodoId == todoId)
        {
            HighlightedTodoId = null;
        }
    }

    public void RefreshCollapsedPanelTodo()
    {
        CollapsedPanelTodo = Todos.FirstOrDefault(todo => !todo.IsCompleted) ?? Todos.FirstOrDefault();
        CollapsedPanelTodoDueText = BuildTodoDueText(CollapsedPanelTodo);
    }

    private string BuildTodoDueText(TodoItem? todo)
    {
        if (todo?.DueDate == null) return string.Empty;

        var due = todo.DueDate.Value;
        var today = DateTime.Today;
        var hasTime = due.TimeOfDay.TotalSeconds > 0;
        if (due.Date == today)
        {
            return hasTime ? $"{L("Common.Today")} {due:HH:mm}" : L("Common.Today");
        }

        if (due.Date == today.AddDays(1))
        {
            return hasTime ? $"{L("Common.Tomorrow")} {due:HH:mm}" : L("Common.Tomorrow");
        }

        return hasTime
            ? due.ToString("M/d HH:mm", CultureInfo.CurrentCulture)
            : due.ToString("M/d", CultureInfo.CurrentCulture);
    }

    private string L(string key) => _localizationService.GetString(key);
}
