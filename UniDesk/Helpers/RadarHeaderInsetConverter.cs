using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace UniDesk.Helpers;

public sealed class RadarHeaderInsetConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture) =>
        values is [double width, double firstLabelWidth, double lastLabelWidth] &&
            double.IsFinite(width) && double.IsFinite(firstLabelWidth) && double.IsFinite(lastLabelWidth)
            ? new Thickness(Math.Max(0, width / 10 - firstLabelWidth / 2), 0,
                Math.Max(0, width / 10 - lastLabelWidth / 2), 0)
            : new Thickness(0);

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
