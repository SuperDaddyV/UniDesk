using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using System.Collections.ObjectModel;
using System.Globalization;

namespace UniDesk.ViewModels;

public partial class TodoEditViewModel : ObservableObject
{
    private readonly ITodoService _todoService;
    private readonly ILocalizationService _localizationService;
    private readonly bool _isNew;
    private readonly int _todoId;
    private readonly DateTime _createdAt;
    private readonly bool _isCompleted;
    private readonly DateTime? _completedAt;
    private bool _suppressPresetUpdate;

    public const int MaxTitleLength = 50;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private DateTime _dueDateTime = DateTime.Today.AddHours(9);

    [ObservableProperty]
    private TodoDatePreset _selectedDatePreset = TodoDatePreset.Today;

    [ObservableProperty]
    private TodoPriority _selectedPriority = TodoPriority.Medium;

    [ObservableProperty]
    private bool _isDatePopupOpen;

    [ObservableProperty]
    private bool _isTimePopupOpen;

    [ObservableProperty]
    private DateTime _calendarDisplayDate = DateTime.Today;

    public int TitleLength => Title?.Length ?? 0;

    public string DateDisplayText => TodoDisplayHelper.FormatDateWithWeekday(
        DueDateTime.Date,
        _localizationService.CurrentLanguage,
        _localizationService.CurrentCulture);

    public string TimeDisplayText => TodoDisplayHelper.FormatTime(DueDateTime);

    public ObservableCollection<string> TimeOptions { get; } = BuildTimeOptions();

    public IReadOnlyList<string> CalendarWeekdayLabels =>
        CalendarDayBuilder.GetWeekdayLabels(_localizationService.CurrentLanguage);

    public string WindowTitle => _isNew ? L("Todo.Add") : L("Todo.Edit");

    public TodoEditViewModel(
        ITodoService todoService,
        ILocalizationService localizationService,
        TodoItem? todo = null)
    {
        _todoService = todoService;
        _localizationService = localizationService;

        if (todo == null)
        {
            _isNew = true;
            return;
        }

        _isNew = false;
        _todoId = todo.Id;
        _createdAt = todo.CreatedAt == default ? DateTime.UtcNow : todo.CreatedAt;
        _isCompleted = todo.IsCompleted;
        _completedAt = todo.CompletedAt;
        Title = todo.Title;
        DueDateTime = todo.DueDate ?? DateTime.Today.AddHours(9);
        SelectedPriority = todo.Priority;
        SelectedDatePreset = InferDatePreset(DueDateTime.Date);
    }

    partial void OnTitleChanged(string value)
    {
        if (value.Length > MaxTitleLength)
        {
            Title = value[..MaxTitleLength];
            return;
        }

        OnPropertyChanged(nameof(TitleLength));
    }

    partial void OnDueDateTimeChanged(DateTime value)
    {
        OnPropertyChanged(nameof(DateDisplayText));
        OnPropertyChanged(nameof(TimeDisplayText));
        CalendarDisplayDate = value.Date;
    }

    partial void OnSelectedDatePresetChanged(TodoDatePreset value)
    {
        if (_suppressPresetUpdate || value == TodoDatePreset.Custom)
        {
            return;
        }

        ApplyDatePreset(value);
    }

    [RelayCommand]
    private void SelectDatePreset(string? presetName)
    {
        if (!Enum.TryParse<TodoDatePreset>(presetName, out var preset))
        {
            return;
        }

        if (preset == TodoDatePreset.Custom)
        {
            SelectedDatePreset = TodoDatePreset.Custom;
            IsDatePopupOpen = true;
            IsTimePopupOpen = false;
            return;
        }

        SelectedDatePreset = preset;
    }

    [RelayCommand]
    private void SelectPriority(string? priorityName)
    {
        if (!Enum.TryParse<TodoPriority>(priorityName, out var priority))
        {
            return;
        }

        SelectedPriority = priority;
    }

    [RelayCommand]
    private void OpenDatePicker()
    {
        IsTimePopupOpen = false;
        CalendarDisplayDate = DueDateTime.Date;
        IsDatePopupOpen = true;
    }

    [RelayCommand]
    private void OpenTimePicker()
    {
        IsDatePopupOpen = false;
        IsTimePopupOpen = true;
    }

    [RelayCommand]
    private void ClosePopups()
    {
        IsDatePopupOpen = false;
        IsTimePopupOpen = false;
    }

    [RelayCommand]
    private void PickCalendarDate(DateTime? date)
    {
        if (date == null) return;

        var time = DueDateTime.TimeOfDay;
        DueDateTime = date.Value.Date.Add(time);
        _suppressPresetUpdate = true;
        SelectedDatePreset = TodoDatePreset.Custom;
        _suppressPresetUpdate = false;
        IsDatePopupOpen = false;
    }

    [RelayCommand]
    private void PickTime(string? timeText)
    {
        if (string.IsNullOrWhiteSpace(timeText)) return;

        if (TimeSpan.TryParseExact(
                timeText.Trim(),
                ["h\\:m", "hh\\:mm", "H\\:m", "HH\\:mm"],
                CultureInfo.InvariantCulture,
                out var time))
        {
            DueDateTime = DueDateTime.Date.Add(time);
        }

        IsTimePopupOpen = false;
    }

    public async Task<bool> SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            return false;
        }

        try
        {
            if (_isNew)
            {
                await _todoService.CreateTodoAsync(new TodoItem
                {
                    Title = Title.Trim(),
                    DueDate = DueDateTime,
                    Priority = SelectedPriority,
                    CreatedAt = DateTime.UtcNow
                });
            }
            else
            {
                await _todoService.UpdateTodoAsync(new TodoItem
                {
                    Id = _todoId,
                    Title = Title.Trim(),
                    DueDate = DueDateTime,
                    Priority = SelectedPriority,
                    IsCompleted = _isCompleted,
                    CreatedAt = _createdAt,
                    CompletedAt = _completedAt
                });
            }

            return true;
        }
        catch
        {
            return false;
        }
    }

    private void ApplyDatePreset(TodoDatePreset preset)
    {
        var time = DueDateTime.TimeOfDay;
        var today = DateTime.Today;
        var date = preset switch
        {
            TodoDatePreset.Today => today,
            TodoDatePreset.Tomorrow => today.AddDays(1),
            TodoDatePreset.DayAfterTomorrow => today.AddDays(2),
            TodoDatePreset.ThisWeek => TodoDisplayHelper.EndOfWeek(today),
            _ => DueDateTime.Date
        };

        DueDateTime = date.Add(time);
    }

    private static ObservableCollection<string> BuildTimeOptions()
    {
        var options = new ObservableCollection<string>();
        for (var hour = 8; hour <= 22; hour++)
        {
            options.Add($"{hour:00}:00");
            options.Add($"{hour:00}:30");
        }

        return options;
    }

    private static TodoDatePreset InferDatePreset(DateTime date)
    {
        var today = DateTime.Today;
        if (date == today) return TodoDatePreset.Today;
        if (date == today.AddDays(1)) return TodoDatePreset.Tomorrow;
        if (date == today.AddDays(2)) return TodoDatePreset.DayAfterTomorrow;
        if (date == TodoDisplayHelper.EndOfWeek(today)) return TodoDatePreset.ThisWeek;

        return TodoDatePreset.Custom;
    }

    public string FormatCalendarMonth(DateTime month) =>
        TodoDisplayHelper.FormatMonth(month, _localizationService.CurrentLanguage, _localizationService.CurrentCulture);

    private string L(string key) => _localizationService.GetString(key);
}
