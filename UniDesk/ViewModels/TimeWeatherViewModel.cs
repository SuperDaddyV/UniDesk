using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Windows.Media;
using System.Windows.Threading;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.ViewModels;

public partial class TimeWeatherViewModel : ObservableObject, IDisposable
{
    private readonly IClockService _clockService;
    private readonly IWeatherService _weatherService;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private readonly DispatcherTimer _weatherRefreshTimer;
    private readonly object _weatherRefreshLock = new();
    private CancellationTokenSource? _weatherRefreshCts;
    private int _weatherRefreshGeneration;
    private DateTime _calendarDisplayMonth = DateTime.Today;
    private DateTime _calendarSelectedDate = DateTime.Today;
    private bool _disposed;

    [ObservableProperty] private string _clockTimeText = "--:--";
    [ObservableProperty] private string _clockDateText = "--";
    [ObservableProperty] private string _clockLunarText = string.Empty;
    [ObservableProperty] private bool _isCalendarPopupOpen;
    [ObservableProperty] private string _calendarMonthTitle = string.Empty;
    [ObservableProperty] private string _calendarSelectedDetailText = string.Empty;
    [ObservableProperty] private string _weatherCity = string.Empty;
    [ObservableProperty] private string _weatherTemperature = "--";
    [ObservableProperty] private string _weatherDescription = string.Empty;
    [ObservableProperty] private string _weatherDetailLine = string.Empty;
    [ObservableProperty] private string _weatherRangeLine = string.Empty;
    [ObservableProperty] private ImageSource? _weatherIconImage;
    [ObservableProperty] private bool _useWeatherIconImage;
    [ObservableProperty] private string _weatherIconGlyph = string.Empty;
    [ObservableProperty] private Brush _weatherIconForeground = Brushes.White;
    [ObservableProperty] private string _weatherStatusMessage = string.Empty;
    [ObservableProperty] private bool _isWeatherLoading;
    [ObservableProperty] private bool _hasWeatherData;

    public IReadOnlyList<string> CalendarWeekdayLabels =>
        CalendarDayBuilder.GetWeekdayLabels(_localizationService.CurrentLanguage);
    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = [];
    public bool IsEnabled { get; set; } = true;

    public TimeWeatherViewModel(
        IClockService clockService,
        IWeatherService weatherService,
        INotificationService notificationService,
        ILocalizationService localizationService,
        bool startWeatherTimer = true)
    {
        _clockService = clockService;
        _weatherService = weatherService;
        _notificationService = notificationService;
        _localizationService = localizationService;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;
        _clockService.TimeChanged += ClockService_OnTimeChanged;
        _clockService.Start();
        UpdateClockText();
        RefreshCalendarDays();

        _weatherRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _weatherRefreshTimer.Tick += WeatherRefreshTimer_OnTick;
        if (startWeatherTimer) _weatherRefreshTimer.Start();
        _ = InitializeAsync();
    }

    private void WeatherRefreshTimer_OnTick(object? sender, EventArgs e)
    {
        if (IsEnabled) _ = RefreshWeatherCoreAsync(notifyUser: false);
    }

    private void ClockService_OnTimeChanged() => UpdateClockText();
    
    private void UpdateClockText()
    {
        try
        {
            var now = _clockService.CurrentTime;
            ClockTimeText = now.ToString("HH:mm", CultureInfo.InvariantCulture);
            ClockDateText = FormatDateText(now);
            ClockLunarText = IsChineseLanguage ? ToChineseLunarText(now) : string.Empty;
            if (_calendarSelectedDate.Date == now.Date)
            {
                CalendarSelectedDetailText = BuildCalendarSelectedDetail(now.Date);
            }
        }
        catch
        {
            ClockTimeText = "--:--";
            ClockDateText = "--";
            ClockLunarText = string.Empty;
        }
    }
    
    private string FormatDateText(DateTime date)
    {
        return _localizationService.CurrentLanguage switch
        {
            "en-US" => date.ToString("MMM d, yyyy", _localizationService.CurrentCulture),
            "ja-JP" => $"{date:yyyy年M月d日}（{ToJapaneseDayOfWeek(date.DayOfWeek)}）",
            "es-ES" => date.ToString("d MMM yyyy", _localizationService.CurrentCulture),
            _ => $"{date:yyyy年M月d日} {ToChineseDayOfWeek(date.DayOfWeek)}"
        };
    }
    
    private static string ToChineseDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "星期一",
        DayOfWeek.Tuesday => "星期二",
        DayOfWeek.Wednesday => "星期三",
        DayOfWeek.Thursday => "星期四",
        DayOfWeek.Friday => "星期五",
        DayOfWeek.Saturday => "星期六",
        DayOfWeek.Sunday => "星期日",
        _ => ""
    };
    
    private static string ToJapaneseDayOfWeek(DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => "月",
        DayOfWeek.Tuesday => "火",
        DayOfWeek.Wednesday => "水",
        DayOfWeek.Thursday => "木",
        DayOfWeek.Friday => "金",
        DayOfWeek.Saturday => "土",
        DayOfWeek.Sunday => "日",
        _ => ""
    };
    
    private static string ToChineseLunarText(DateTime date)
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
    
            var sexagenaryYear = calendar.GetSexagenaryYear(date);
            var stem = calendar.GetCelestialStem(sexagenaryYear);
            var branch = calendar.GetTerrestrialBranch(sexagenaryYear);
    
            var stems = new[] { "甲", "乙", "丙", "丁", "戊", "己", "庚", "辛", "壬", "癸" };
            var branches = new[] { "子", "丑", "寅", "卯", "辰", "巳", "午", "未", "申", "酉", "戌", "亥" };
            return $"{stems[stem - 1]}{branches[branch - 1]}年 {(isLeapMonth ? "闰" : "")}{ToChineseLunarMonth(lunarMonth)} {CalendarDayBuilder.ToChineseLunarText(date)}";
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
    
    [RelayCommand]
    private void ToggleCalendarPopup()
    {
        if (!IsCalendarPopupOpen)
        {
            var today = _clockService.CurrentTime.Date;
            _calendarSelectedDate = today;
            _calendarDisplayMonth = new DateTime(today.Year, today.Month, 1);
            RefreshCalendarDays();
        }
    
        IsCalendarPopupOpen = !IsCalendarPopupOpen;
    }
    
    [RelayCommand]
    private void PreviousCalendarMonth()
    {
        _calendarDisplayMonth = _calendarDisplayMonth.AddMonths(-1);
        RefreshCalendarDays();
    }
    
    [RelayCommand]
    private void NextCalendarMonth()
    {
        _calendarDisplayMonth = _calendarDisplayMonth.AddMonths(1);
        RefreshCalendarDays();
    }
    
    [RelayCommand]
    private void BackToToday()
    {
        var today = _clockService.CurrentTime.Date;
        _calendarSelectedDate = today;
        _calendarDisplayMonth = new DateTime(today.Year, today.Month, 1);
        RefreshCalendarDays();
    }
    
    [RelayCommand]
    private void SelectCalendarDate(CalendarDayItem? item)
    {
        if (item == null)
        {
            return;
        }
    
        _calendarSelectedDate = item.Date.Date;
        _calendarDisplayMonth = new DateTime(item.Date.Year, item.Date.Month, 1);
        RefreshCalendarDays();
    }
    
    private void RefreshCalendarDays()
    {
        CalendarMonthTitle = _localizationService.CurrentLanguage switch
        {
            "en-US" => _calendarDisplayMonth.ToString("MMMM yyyy", _localizationService.CurrentCulture),
            "ja-JP" => $"{_calendarDisplayMonth:yyyy年M月}",
            "es-ES" => _calendarDisplayMonth.ToString("MMMM yyyy", _localizationService.CurrentCulture),
            _ => $"{_calendarDisplayMonth:yyyy年M月}"
        };
        CalendarSelectedDetailText = BuildCalendarSelectedDetail(_calendarSelectedDate);
        CalendarDays.Clear();
        foreach (var day in CalendarDayBuilder.BuildMonth(_calendarDisplayMonth, _calendarSelectedDate))
        {
            CalendarDays.Add(day);
        }
    }
    
    private string BuildCalendarSelectedDetail(DateTime date)
    {
        if (!IsChineseLanguage)
        {
            return FormatDateText(date);
        }
    
        var lunarYear = CalendarDayBuilder.ToChineseLunarYearText(date);
        var lunarDay = CalendarDayBuilder.ToChineseLunarText(date);
        return $"{date:yyyy年M月d日} {ToChineseDayOfWeek(date.DayOfWeek)}  {lunarYear} {lunarDay}".Trim();
    }
    
    public async Task InitializeAsync()
    {
        ApplyWeatherInfo(await _weatherService.GetCachedWeatherAsync());
    
        if (string.IsNullOrEmpty(_weatherService.GetEffectiveApiKey()))
        {
            WeatherStatusMessage = L("Weather.ConfigureApiKey");
            return;
        }
    
        await RefreshWeatherCoreAsync(notifyUser: false);
    }
    
    public async Task RefreshAfterSettingsAsync()
    {
        await RefreshWeatherCoreAsync(notifyUser: false);
    }
    
    [RelayCommand]
    private async Task RefreshWeatherAsync() => await RefreshWeatherCoreAsync(notifyUser: true);
    
    private async Task RefreshWeatherCoreAsync(bool notifyUser)
    {
        if (!IsEnabled || _disposed)
        {
            return;
        }

        var refreshCts = new CancellationTokenSource();
        CancellationTokenSource? previousCts;
        int generation;
        lock (_weatherRefreshLock)
        {
            if (_disposed)
            {
                refreshCts.Dispose();
                return;
            }

            generation = ++_weatherRefreshGeneration;
            previousCts = _weatherRefreshCts;
            _weatherRefreshCts = refreshCts;
        }

        CancelRefreshSafely(previousCts);

        try
        {
            if (string.IsNullOrEmpty(_weatherService.GetEffectiveApiKey()))
            {
                var cached = await _weatherService.GetCachedWeatherAsync();
                if (IsCurrentWeatherRefresh(refreshCts, generation))
                {
                    ApplyWeatherInfo(cached);
                    WeatherStatusMessage = L("Weather.ConfigureApiKey");
                }

                return;
            }

            if (IsCurrentWeatherRefresh(refreshCts, generation))
            {
                IsWeatherLoading = true;
                WeatherStatusMessage = string.Empty;
            }

            var info = await _weatherService.RefreshWeatherAsync(refreshCts.Token, notifyUser);
            var resolvedInfo = info ?? await _weatherService.GetCachedWeatherAsync();
            if (IsCurrentWeatherRefresh(refreshCts, generation))
            {
                ApplyWeatherInfo(resolvedInfo);
            }
        }
        catch (OperationCanceledException)
        {
            var cached = await _weatherService.GetCachedWeatherAsync();
            if (IsCurrentWeatherRefresh(refreshCts, generation))
            {
                ApplyWeatherInfo(cached);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "TimeWeatherViewModel.RefreshWeather");
            var cached = await _weatherService.GetCachedWeatherAsync();
            if (IsCurrentWeatherRefresh(refreshCts, generation))
            {
                ApplyWeatherInfo(cached);
                if (notifyUser)
                {
                    _notificationService.ShowWarningMessage(L("Weather.RefreshFailed"));
                }
            }
        }
        finally
        {
            var isCurrent = false;
            lock (_weatherRefreshLock)
            {
                if (generation == _weatherRefreshGeneration &&
                    ReferenceEquals(_weatherRefreshCts, refreshCts))
                {
                    _weatherRefreshCts = null;
                    isCurrent = true;
                }
            }

            if (isCurrent)
            {
                IsWeatherLoading = false;
            }

            refreshCts.Dispose();
        }
    }

    private bool IsCurrentWeatherRefresh(CancellationTokenSource refreshCts, int generation)
    {
        lock (_weatherRefreshLock)
        {
            return !_disposed &&
                   generation == _weatherRefreshGeneration &&
                   ReferenceEquals(_weatherRefreshCts, refreshCts);
        }
    }

    private static void CancelRefreshSafely(CancellationTokenSource? refreshCts)
    {
        try
        {
            refreshCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }
    
    private void ApplyWeatherInfo(WeatherInfo? info)
    {
        if (info == null)
        {
            HasWeatherData = false;
            WeatherCity = L("Weather.LoadFailed");
            WeatherTemperature = "--";
            WeatherDescription = string.Empty;
            WeatherDetailLine = string.Empty;
            WeatherRangeLine = string.Empty;
            WeatherIconImage = null;
            UseWeatherIconImage = false;
            WeatherIconGlyph = string.Empty;
            WeatherIconForeground = Brushes.White;
            if (string.IsNullOrEmpty(WeatherStatusMessage))
            {
                WeatherStatusMessage = string.Empty;
            }
    
            return;
        }
    
        HasWeatherData = true;
        WeatherCity = info.City;
        WeatherTemperature = info.Temperature;
        WeatherDescription = info.WeatherDesc;
        ApplyWeatherIcon(info);
    
        var details = new List<string>
        {
            string.IsNullOrWhiteSpace(info.AirQuality) ? L("Weather.AirFallback") : info.AirQuality
        };
    
        if (!string.IsNullOrWhiteSpace(info.Humidity))
        {
            details.Add(info.Humidity);
        }
    
        var range = BuildTempRange(info.MinTemp, info.MaxTemp);
        WeatherDetailLine = string.Join("  |  ", details);
        WeatherRangeLine = range;
        WeatherStatusMessage = info.IsExpired ? L("Weather.Expired") : string.Empty;
    }
    
    private static string BuildTempRange(string minTemp, string maxTemp)
    {
        if (string.IsNullOrWhiteSpace(minTemp) && string.IsNullOrWhiteSpace(maxTemp))
        {
            return string.Empty;
        }
    
        if (string.IsNullOrWhiteSpace(minTemp)) return maxTemp;
        if (string.IsNullOrWhiteSpace(maxTemp)) return minTemp;
        return $"{minTemp} ~ {maxTemp}";
    }
    
    private void ApplyWeatherIcon(WeatherInfo info)
    {
        var iconCode = ResolveIconCode(info);
        var display = WeatherIconResolver.Resolve(iconCode, info.WeatherDesc);
        UseWeatherIconImage = display.UseImage;
        WeatherIconImage = display.ImageSource;
        WeatherIconGlyph = display.Glyph;
        WeatherIconForeground = display.GlyphForeground;
    }
    
    private static string ResolveIconCode(WeatherInfo info)
    {
        if (!string.IsNullOrWhiteSpace(info.IconCode))
        {
            return info.IconCode.Trim();
        }
    
        if (!string.IsNullOrWhiteSpace(info.IconUri))
        {
            var fileName = Path.GetFileNameWithoutExtension(info.IconUri);
            if (!string.IsNullOrEmpty(fileName) && fileName.All(char.IsDigit))
            {
                return fileName;
            }
        }
    
        return WeatherIconResolver.NormalizeIconCode(null, info.WeatherDesc);
    }

    private bool IsChineseLanguage =>
        string.Equals(
            _localizationService.CurrentLanguage,
            ILocalizationService.DefaultLanguage,
            StringComparison.OrdinalIgnoreCase);

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(CalendarWeekdayLabels));
        UpdateClockText();
        RefreshCalendarDays();
        if (WeatherStatusMessage == "请在设置中配置 API Key" ||
            WeatherStatusMessage == "Configure API Key in Settings")
        {
            WeatherStatusMessage = L("Weather.ConfigureApiKey");
        }
    }

    private string L(string key) => _localizationService.GetString(key);

    public void Dispose()
    {
        CancellationTokenSource? refreshCts;
        lock (_weatherRefreshLock)
        {
            if (_disposed) return;
            _disposed = true;
            _weatherRefreshGeneration++;
            refreshCts = _weatherRefreshCts;
            _weatherRefreshCts = null;
        }

        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        _clockService.TimeChanged -= ClockService_OnTimeChanged;
        _weatherRefreshTimer.Stop();
        _weatherRefreshTimer.Tick -= WeatherRefreshTimer_OnTick;
        CancelRefreshSafely(refreshCts);
        _clockService.Stop();
    }
}
