using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using UniDesk.Models;

namespace UniDesk.Helpers;

public class TodoPriorityToBrushConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var resourceKey = value is TodoPriority priority
            ? priority switch
            {
                TodoPriority.Low => "MutedTextBrush",
                TodoPriority.Medium => "WarningBrush",
                TodoPriority.High => "DangerBrush",
                _ => "MutedTextBrush"
            }
            : "MutedTextBrush";

        return Application.Current?.TryFindResource(resourceKey) as Brush ?? Brushes.Gray;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
