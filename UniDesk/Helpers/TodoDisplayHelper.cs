using System.Globalization;

namespace UniDesk.Helpers;

public static class TodoDisplayHelper
{
    private static readonly string[] WeekdayNames = ["周日", "周一", "周二", "周三", "周四", "周五", "周六"];
    private static readonly string[] JapaneseWeekdayNames = ["日", "月", "火", "水", "木", "金", "土"];

    public static string FormatDateWithWeekday(DateTime date)
        => $"{date:yyyy-MM-dd} ({WeekdayNames[(int)date.DayOfWeek]})";

    public static string FormatDateWithWeekday(DateTime date, string? language, CultureInfo culture)
    {
        return language switch
        {
            "zh-CN" => $"{date:yyyy-MM-dd} ({WeekdayNames[(int)date.DayOfWeek]})",
            "ja-JP" => $"{date:yyyy/MM/dd} ({JapaneseWeekdayNames[(int)date.DayOfWeek]})",
            _ => $"{date.ToString("d", culture)} ({date.ToString("ddd", culture)})"
        };
    }

    public static string FormatMonth(DateTime month, string? language, CultureInfo culture)
    {
        return language switch
        {
            "zh-CN" => $"{month:yyyy年M月}",
            "ja-JP" => $"{month:yyyy年M月}",
            "es-ES" => month.ToString("MMMM yyyy", culture),
            _ => month.ToString("MMMM yyyy", culture)
        };
    }

    public static DateTime EndOfWeek(DateTime date)
    {
        var daysUntilSunday = ((int)DayOfWeek.Sunday - (int)date.DayOfWeek + 7) % 7;
        return date.Date.AddDays(daysUntilSunday == 0 ? 7 : daysUntilSunday);
    }

    public static string FormatTime(DateTime dateTime)
        => dateTime.ToString("HH:mm", CultureInfo.InvariantCulture);

    public static (DateTime Date, TimeSpan Time) SplitDateTime(DateTime dateTime)
        => (dateTime.Date, dateTime.TimeOfDay);
}
