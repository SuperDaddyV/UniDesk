using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UniDesk.Helpers;

public class BooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Visible : Visibility.Collapsed;
        }
        return Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility == Visibility.Visible;
        }
        return false;
    }
}

public class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        return value is bool boolValue && !boolValue;
    }
}

public class InverseBooleanToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is bool boolValue)
        {
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }
        return Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
    {
        if (value is Visibility visibility)
        {
            return visibility != Visibility.Visible;
        }
        return true;
    }
}

public class ScaledFontSizeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        var scale = value switch
        {
            double doubleValue => doubleValue,
            float floatValue => floatValue,
            decimal decimalValue => (double)decimalValue,
            int intValue => intValue,
            string text when double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => 1.0
        };

        var baseSize = 12.0;
        if (parameter is string parameterText &&
            double.TryParse(parameterText, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedBaseSize))
        {
            baseSize = parsedBaseSize;
        }

        scale = Math.Clamp(scale, 0.9, 1.18);
        return Math.Round(baseSize * scale, 1);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        Binding.DoNothing;
}

public class StringEqualsConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length < 2)
        {
            return false;
        }

        var left = values[0]?.ToString();
        var right = values[1]?.ToString();
        return string.Equals(left, right, StringComparison.Ordinal);
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
