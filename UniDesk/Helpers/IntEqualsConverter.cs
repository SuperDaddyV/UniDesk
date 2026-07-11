using System.Globalization;
using System.Windows.Data;

namespace UniDesk.Helpers;

public sealed class IntEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2 || values[0] == null || values[1] == null)
        {
            return false;
        }

        return int.TryParse(values[0].ToString(), out var left) &&
               int.TryParse(values[1].ToString(), out var right) &&
               left == right;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
