using System.Globalization;
using System.Windows.Data;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Helpers;

public class TodoDueTextConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TodoItem todo || todo.DueDate == null)
        {
            return string.Empty;
        }

        var due = todo.DueDate.Value;
        var today = DateTime.Today;
        var hasTime = due.TimeOfDay.TotalSeconds > 0;

        if (due.Date == today)
        {
            var todayText = LocalizationService.Current?.GetString("Common.Today") ?? "今天";
            return hasTime
                ? $"{todayText} {due:HH:mm}"
                : todayText;
        }

        if (due.Date == today.AddDays(1))
        {
            var tomorrowText = LocalizationService.Current?.GetString("Common.Tomorrow") ?? "明天";
            return hasTime
                ? $"{tomorrowText} {due:HH:mm}"
                : tomorrowText;
        }

        return hasTime
            ? due.ToString("M/d HH:mm", culture)
            : due.ToString("M/d", culture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class TodoDueIsTodayConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not TodoItem todo || todo.DueDate == null)
        {
            return false;
        }

        return todo.DueDate.Value.Date == DateTime.Today;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
