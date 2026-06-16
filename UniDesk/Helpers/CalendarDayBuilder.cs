using UniDesk.Models;
using System.Globalization;

namespace UniDesk.Helpers;

public static class CalendarDayBuilder
{
    private static readonly string[] ChineseWeekdayLabels = ["一", "二", "三", "四", "五", "六", "日"];
    private static readonly string[] EnglishWeekdayLabels = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];
    private static readonly string[] JapaneseWeekdayLabels = ["月", "火", "水", "木", "金", "土", "日"];
    private static readonly string[] SpanishWeekdayLabels = ["lun", "mar", "mié", "jue", "vie", "sáb", "dom"];

    public static IReadOnlyList<string> WeekdayLabelsList => ChineseWeekdayLabels;

    public static IReadOnlyList<string> GetWeekdayLabels(string? language) => language switch
    {
        "en-US" => EnglishWeekdayLabels,
        "ja-JP" => JapaneseWeekdayLabels,
        "es-ES" => SpanishWeekdayLabels,
        _ => ChineseWeekdayLabels
    };

    public static IReadOnlyList<CalendarDayItem> BuildMonth(DateTime month, DateTime selectedDate)
    {
        var firstOfMonth = new DateTime(month.Year, month.Month, 1);
        var start = firstOfMonth;
        while (start.DayOfWeek != DayOfWeek.Monday)
        {
            start = start.AddDays(-1);
        }

        var today = DateTime.Today;
        var days = new List<CalendarDayItem>(42);
        for (var i = 0; i < 42; i++)
        {
            var date = start.AddDays(i);
            days.Add(new CalendarDayItem
            {
                Day = date.Day,
                LunarText = ToChineseLunarText(date),
                Date = date,
                IsCurrentMonth = date.Month == month.Month,
                IsSelected = date.Date == selectedDate.Date,
                IsToday = date.Date == today
            });
        }

        return days;
    }

    public static string ToChineseLunarText(DateTime date)
    {
        try
        {
            var calendar = new ChineseLunisolarCalendar();
            var lunarYear = calendar.GetYear(date);
            var lunarMonth = calendar.GetMonth(date);
            var lunarDay = calendar.GetDayOfMonth(date);
            var leapMonth = calendar.GetLeapMonth(lunarYear);
            var isLeapMonth = leapMonth > 0 && lunarMonth == leapMonth;

            if (leapMonth > 0 && lunarMonth >= leapMonth)
            {
                lunarMonth--;
            }

            if (lunarDay == 1)
            {
                return $"{(isLeapMonth ? "闰" : "")}{ToChineseLunarMonth(lunarMonth)}";
            }

            return ToChineseLunarDay(lunarDay);
        }
        catch
        {
            return string.Empty;
        }
    }

    public static string ToChineseLunarYearText(DateTime date)
    {
        try
        {
            var calendar = new ChineseLunisolarCalendar();
            var sexagenaryYear = calendar.GetSexagenaryYear(date);
            var stem = calendar.GetCelestialStem(sexagenaryYear);
            var branch = calendar.GetTerrestrialBranch(sexagenaryYear);
            var stems = new[] { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
            var branches = new[] { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
            return $"{stems[stem - 1]}{branches[branch - 1]}年";
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ToChineseLunarMonth(int month)
    {
        var months = new[] { "", "正月", "二月", "三月", "四月", "五月", "六月", "七月", "八月", "九月", "十月", "冬月", "腊月" };
        return month >= 1 && month < months.Length ? months[month] : string.Empty;
    }

    private static string ToChineseLunarDay(int day)
    {
        var days = new[]
        {
            "", "初一", "初二", "初三", "初四", "初五", "初六", "初七", "初八", "初九", "初十",
            "十一", "十二", "十三", "十四", "十五", "十六", "十七", "十八", "十九", "二十",
            "廿一", "廿二", "廿三", "廿四", "廿五", "廿六", "廿七", "廿八", "廿九", "三十"
        };
        return day >= 1 && day < days.Length ? days[day] : string.Empty;
    }
}
