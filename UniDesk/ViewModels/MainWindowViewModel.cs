using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace UniDesk.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly INotificationService _notificationService;
    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IUpdateService _updateService;
    private readonly IWindowService _windowService;
    private readonly ILayoutService _layoutService;
    private readonly IClockService _clockService;
    private readonly IQuickTextService _quickTextService;
    private readonly IWeatherService _weatherService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IStartupService _startupService;
    private readonly ITodoBackupService _todoBackupService;
    private readonly DispatcherTimer _weatherRefreshTimer;
    private CancellationTokenSource? _weatherRefreshCts;
    private bool _disposed;
    private bool _isLoadingModuleSettings;

    private static readonly JsonSerializerOptions ModuleSettingsJsonOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    [ObservableProperty]
    private bool _isTopMost = true;

    /// <summary>窗口已锁定，不可拖动。</summary>
    [ObservableProperty]
    private bool _isWindowLocked;

    /// <summary>面板已收缩，仅显示标题与时间。</summary>
    [ObservableProperty]
    private bool _isPanelCollapsed;

    [ObservableProperty]
    private double _windowOpacity = 0.70;

    [ObservableProperty]
    private double _panelWidth = 320;

    [ObservableProperty]
    private double _panelHeight = 702;

    [ObservableProperty]
    private double _fontScale = 1.0;

    [ObservableProperty]
    private string _displayTitle = "UniDesk";

    [ObservableProperty]
    private int _moduleLayoutVersion;

    [ObservableProperty]
    private string _clockTimeText = "--:--";

    [ObservableProperty]
    private string _clockDateText = "--";

    [ObservableProperty]
    private string _clockLunarText = string.Empty;

    [ObservableProperty]
    private bool _isCalendarPopupOpen;

    [ObservableProperty]
    private string _calendarMonthTitle = string.Empty;

    [ObservableProperty]
    private string _calendarSelectedDetailText = string.Empty;

    public IReadOnlyList<string> CalendarWeekdayLabels => CalendarDayBuilder.GetWeekdayLabels(_localizationService.CurrentLanguage);

    public ObservableCollection<CalendarDayItem> CalendarDays { get; } = new();

    private DateTime _calendarDisplayMonth = DateTime.Today;
    private DateTime _calendarSelectedDate = DateTime.Today;

    public ObservableCollection<ModuleSetting> ModuleSettings { get; } = new();

    [ObservableProperty]
    private string _weatherCity = string.Empty;

    [ObservableProperty]
    private string _weatherTemperature = "--";

    [ObservableProperty]
    private string _weatherDescription = string.Empty;

    [ObservableProperty]
    private string _weatherDetailLine = string.Empty;

    [ObservableProperty]
    private string _weatherRangeLine = string.Empty;

    [ObservableProperty]
    private ImageSource? _weatherIconImage;

    [ObservableProperty]
    private bool _useWeatherIconImage;

    [ObservableProperty]
    private string _weatherIconGlyph = string.Empty;

    [ObservableProperty]
    private Brush _weatherIconForeground = Brushes.White;

    [ObservableProperty]
    private string _weatherStatusMessage = string.Empty;

    [ObservableProperty]
    private bool _isWeatherLoading;

    [ObservableProperty]
    private bool _hasWeatherData;

    public string WindowLockToolTip => L(IsWindowLocked ? "Header.UnlockWindow" : "Header.LockWindow");

    public string PanelCollapseToolTip => L(IsPanelCollapsed ? "Header.ExpandPanel" : "Header.CollapsePanel");

    public HardwareMonitorViewModel HardwareMonitor { get; }

    public TodosViewModel Todos { get; }

    public QuickNotesViewModel QuickNotes { get; }

    public QuickTextViewModel QuickText { get; }

    public ShortcutsViewModel Shortcuts { get; }

    public MainWindowViewModel(
        INotificationService notificationService,
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IUpdateService updateService,
        IWindowService windowService,
        IHotkeyService hotkeyService,
        ILayoutService layoutService,
        IClockService clockService,
        INoteService noteService,
        IQuickNoteService quickNoteService,
        IQuickTextService quickTextService,
        ITodoService todoService,
        ITodoDeletionHandler todoDeletionHandler,
        IShortcutService shortcutService,
        IWeatherService weatherService,
        IStartupService startupService,
        ITodoBackupService todoBackupService,
        ISystemMetricsMonitor systemMetricsMonitor,
        IClipboardMonitorService clipboardMonitorService)
    {
        _notificationService = notificationService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _updateService = updateService;
        _windowService = windowService;
        _layoutService = layoutService;
        _clockService = clockService;
        _quickTextService = quickTextService;
        _weatherService = weatherService;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _todoBackupService = todoBackupService;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;

        HardwareMonitor = new HardwareMonitorViewModel(systemMetricsMonitor);
        Todos = new TodosViewModel(
            todoService,
            todoDeletionHandler,
            notificationService,
            localizationService,
            () => PanelWidth);
        QuickNotes = new QuickNotesViewModel(
            noteService,
            quickNoteService,
            notificationService,
            localizationService,
            () => PanelWidth);
        QuickText = new QuickTextViewModel(
            quickTextService,
            clipboardMonitorService,
            notificationService,
            localizationService,
            () => PanelWidth);
        Shortcuts = new ShortcutsViewModel(
            shortcutService,
            settingsService,
            notificationService,
            localizationService);
        LoadSettings();
        _layoutService.LoadOrGetDefault();

        _clockService.TimeChanged += ClockService_OnTimeChanged;
        _clockService.Start();
        UpdateClockText();
        RefreshCalendarDays();

        _weatherRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(30) };
        _weatherRefreshTimer.Tick += (_, _) =>
        {
            if (IsModuleEnabled(DashboardModuleIds.TimeWeather))
            {
                _ = RefreshWeatherCoreAsync(notifyUser: false);
            }
        };
        _weatherRefreshTimer.Start();

        if (IsModuleEnabled(DashboardModuleIds.QuickNotes))
        {
            _ = QuickNotes.ReloadAsync();
        }

        if (IsModuleEnabled(DashboardModuleIds.QuickText))
        {
            _ = QuickText.ReloadAsync();
        }

        _ = Todos.ReloadAsync();
        _ = Shortcuts.ReloadAsync();
        _ = InitializeWeatherAsync();
    }

    private void LoadSettings()
    {
        IsTopMost = _settingsService.GetSetting("TopMost", true);
        WindowOpacity = _settingsService.GetSetting("WindowOpacity", 0.70);
        IsWindowLocked = _settingsService.GetSetting("WindowLocked", false);
        IsPanelCollapsed = _settingsService.GetSetting("PanelCollapsed", false);
        var savedPanelWidth = _settingsService.GetSetting("PanelWidth", 320.0);
        if (savedPanelWidth < IWindowService.MinPanelWidth) savedPanelWidth = IWindowService.MinPanelWidth;
        if (savedPanelWidth > IWindowService.MaxPanelWidth) savedPanelWidth = IWindowService.MaxPanelWidth;
        PanelWidth = savedPanelWidth;

        var savedPanelHeight = _settingsService.GetSetting("PanelHeight", 702.0);
        if (savedPanelHeight < IWindowService.MinPanelHeight) savedPanelHeight = IWindowService.MinPanelHeight;
        if (savedPanelHeight > IWindowService.MaxPanelHeight) savedPanelHeight = IWindowService.MaxPanelHeight;
        PanelHeight = savedPanelHeight;

        var savedFontScale = _settingsService.GetSetting("FontScale", 1.0);
        if (savedFontScale < 0.9) savedFontScale = 0.9;
        if (savedFontScale > 1.18) savedFontScale = 1.18;
        FontScale = savedFontScale;

        DisplayTitle = NormalizeDisplayTitle(_settingsService.GetValue("DisplayTitle", "UniDesk"));
        LoadModuleSettings();
    }

    private void LoadModuleSettings()
    {
        if (_isLoadingModuleSettings)
        {
            return;
        }

        _isLoadingModuleSettings = true;
        try
        {
            var json = _settingsService.GetValue(DashboardModuleCatalog.SettingsKey, string.Empty);
            var modules = DeserializeModuleSettings(json);
            ApplyModuleSettingsCore(modules, persist: ShouldPersistNormalizedModuleSettings(json, modules));
        }
        finally
        {
            _isLoadingModuleSettings = false;
        }
    }

    private static List<ModuleSetting> DeserializeModuleSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return DashboardModuleCatalog.CreateDefaultModules();
        }

        try
        {
            var modules = JsonSerializer.Deserialize<List<ModuleSetting>>(json, ModuleSettingsJsonOptions);
            return DashboardModuleCatalog.Normalize(modules);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.DeserializeModuleSettings");
            return DashboardModuleCatalog.CreateDefaultModules();
        }
    }

    private static bool ShouldPersistNormalizedModuleSettings(string? json, IEnumerable<ModuleSetting> modules)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return true;
        }

        try
        {
            var normalizedJson = JsonSerializer.Serialize(
                DashboardModuleCatalog.Normalize(modules),
                ModuleSettingsJsonOptions);
            return !string.Equals(json.Trim(), normalizedJson, StringComparison.Ordinal);
        }
        catch
        {
            return true;
        }
    }

    public List<ModuleSetting> GetModuleSettingsSnapshot() =>
        DashboardModuleCatalog.Normalize(ModuleSettings.Select(module => module.Clone()));

    public void ApplyModuleSettings(IEnumerable<ModuleSetting> modules, bool persist) =>
        ApplyModuleSettingsCore(modules, persist);

    private void ApplyModuleSettingsCore(IEnumerable<ModuleSetting> modules, bool persist)
    {
        var normalized = DashboardModuleCatalog.Normalize(modules);

        ModuleSettings.Clear();
        foreach (var module in normalized)
        {
            ModuleSettings.Add(module);
        }

        ModuleLayoutVersion++;

        if (persist)
        {
            SaveModuleSettings();
        }

        HardwareMonitor.IsEnabled = IsModuleEnabled(DashboardModuleIds.HardwareMonitor);

        if (IsModuleEnabled(DashboardModuleIds.TimeWeather) && !HasWeatherData)
        {
            _ = InitializeWeatherAsync();
        }

        if (IsModuleEnabled(DashboardModuleIds.QuickNotes) && QuickNotes.QuickNotes.Count == 0)
        {
            _ = QuickNotes.ReloadAsync();
        }

        QuickText.IsEnabled = IsModuleEnabled(DashboardModuleIds.QuickText);
        if (QuickText.IsEnabled &&
            QuickText.ClipboardHistory.Count == 0 &&
            QuickText.TextSnippets.Count == 0)
        {
            _ = QuickText.ReloadAsync();
        }
    }

    private void SaveModuleSettings()
    {
        var json = JsonSerializer.Serialize(GetModuleSettingsSnapshot(), ModuleSettingsJsonOptions);
        _settingsService.SetValue(DashboardModuleCatalog.SettingsKey, json);
    }

    public bool IsModuleEnabled(string moduleId) =>
        ModuleSettings.FirstOrDefault(module => module.ModuleId == moduleId)?.IsEnabled ?? true;

    public void UpdatePanelWidth(double width)
    {
        if (width < IWindowService.MinPanelWidth) width = IWindowService.MinPanelWidth;
        if (width > IWindowService.MaxPanelWidth) width = IWindowService.MaxPanelWidth;
        PanelWidth = width;
        _settingsService.SetValue("PanelWidth", width.ToString(CultureInfo.InvariantCulture));
        _windowService.SetWidth(width);
    }

    public void UpdatePanelHeight(double height)
    {
        if (height < IWindowService.MinPanelHeight) height = IWindowService.MinPanelHeight;
        if (height > IWindowService.MaxPanelHeight) height = IWindowService.MaxPanelHeight;
        PanelHeight = height;
        _settingsService.SetValue("PanelHeight", height.ToString(CultureInfo.InvariantCulture));
        if (!IsPanelCollapsed)
        {
            _windowService.SetHeight(height);
        }
    }

    public void UpdateFontScale(double scale)
    {
        if (scale < 0.9) scale = 0.9;
        if (scale > 1.18) scale = 1.18;
        FontScale = scale;
        _settingsService.SetValue("FontScale", scale.ToString(CultureInfo.InvariantCulture));
    }

    public void UpdateDisplayTitle(string? title)
    {
        DisplayTitle = NormalizeDisplayTitle(title);
        _settingsService.SetValue("DisplayTitle", DisplayTitle);
    }

    public static string NormalizeDisplayTitle(string? title)
    {
        var normalized = string.IsNullOrWhiteSpace(title) ? "UniDesk" : title.Trim();
        return normalized.Length > 20 ? normalized[..20] : normalized;
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

    [RelayCommand]
    private void ToggleTopMost()
    {
        IsTopMost = !IsTopMost;
        _settingsService.SetValue("TopMost", IsTopMost.ToString());
        _windowService.SetTopMost(IsTopMost);
    }

    [RelayCommand]
    private void ToggleWindowLock() => IsWindowLocked = !IsWindowLocked;

    [RelayCommand]
    private void TogglePanelCollapse() => IsPanelCollapsed = !IsPanelCollapsed;

    partial void OnIsWindowLockedChanged(bool value)
    {
        OnPropertyChanged(nameof(WindowLockToolTip));
        _settingsService.SetValue("WindowLocked", value.ToString());
    }

    partial void OnIsPanelCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(PanelCollapseToolTip));
        _settingsService.SetValue("PanelCollapsed", value.ToString());
    }

    public (double Left, double Top)? GetSavedWindowPosition()
    {
        var leftText = _settingsService.GetSetting("WindowLeft");
        var topText = _settingsService.GetSetting("WindowTop");
        if (!double.TryParse(leftText, NumberStyles.Float, CultureInfo.InvariantCulture, out var left) ||
            !double.TryParse(topText, NumberStyles.Float, CultureInfo.InvariantCulture, out var top) ||
            !double.IsFinite(left) ||
            !double.IsFinite(top))
        {
            return null;
        }

        return (left, top);
    }

    public void SaveWindowPosition(double left, double top)
    {
        if (!double.IsFinite(left) || !double.IsFinite(top))
        {
            return;
        }

        _settingsService.SetValue("WindowLeft", left.ToString(CultureInfo.InvariantCulture));
        _settingsService.SetValue("WindowTop", top.ToString(CultureInfo.InvariantCulture));
    }

    public Task ReloadTodosAsync() => Todos.ReloadAsync();

    [RelayCommand]
    private void ToggleWindowVisibility() => _windowService.ToggleWindow();

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsViewModel? viewModel = null;

        try
        {
            var owner = Application.Current.MainWindow;
            var ownerWidth = owner?.ActualWidth ?? PanelWidth;
            if (ownerWidth <= 0)
            {
                ownerWidth = PanelWidth;
            }

            var ownerHeight = owner?.ActualHeight ?? 520;
            if (ownerHeight <= 0)
            {
                ownerHeight = 520;
            }

            viewModel = new SettingsViewModel(
                _settingsService,
                _localizationService,
                _updateService,
                _windowService,
                _notificationService,
                _layoutService,
                _weatherService,
                _startupService,
                _todoBackupService,
                _quickTextService,
                this);

            var settingsWindow = new SettingsWindow(viewModel, ownerWidth, ownerHeight);
            if (owner != null)
            {
                settingsWindow.Owner = owner;
                settingsWindow.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }

            settingsWindow.ShowActivated = true;
            settingsWindow.ShowDialog();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.OpenSettings");
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.SaveFailedFormat", ex.Message));
            return;
        }

        if (viewModel is not { LastSaveSucceeded: true })
        {
            return;
        }

        var scheme = AppColorSchemeCatalog.NormalizeId(
            _settingsService.GetValue("ColorScheme", _settingsService.GetValue("Theme", AppColorSchemeCatalog.DefaultSchemeId)));
        AppColorSchemeCatalog.Apply(scheme);

        var savedViewModel = viewModel;
        _ = savedViewModel.CompleteSaveFollowUpAsync(
            savedViewModel.PendingApiKey,
            savedViewModel.PendingApiHost,
            savedViewModel.PendingWeatherSettingsChanged);
    }

    public void ApplyWindowSettings()
    {
        LoadSettings();
        _windowService.SetTopMost(IsTopMost);
        _windowService.SetOpacity(WindowOpacity);
        _windowService.SetWidth(PanelWidth);
        if (!IsPanelCollapsed)
        {
            _windowService.SetHeight(PanelHeight);
        }
    }

    public Task ReloadShortcutsAsync() => Shortcuts.ReloadAsync();

    public void SetShortcutLimitPreview(int? limit) => Shortcuts.SetShortcutLimitPreview(limit);

    public Task ReloadQuickNotesAsync() => QuickNotes.ReloadAsync();

    public Task ReloadQuickTextAsync() => QuickText.ReloadAsync();


    private async Task InitializeWeatherAsync()
    {
        ApplyWeatherInfo(await _weatherService.GetCachedWeatherAsync());

        if (string.IsNullOrEmpty(_weatherService.GetEffectiveApiKey()))
        {
            WeatherStatusMessage = L("Weather.ConfigureApiKey");
            return;
        }

        await RefreshWeatherCoreAsync(notifyUser: false);
    }

    public async Task RefreshWeatherAfterSettingsAsync()
    {
        await RefreshWeatherCoreAsync(notifyUser: false);
    }

    [RelayCommand]
    private async Task RefreshWeatherAsync() => await RefreshWeatherCoreAsync(notifyUser: true);

    private async Task RefreshWeatherCoreAsync(bool notifyUser)
    {
        if (!IsModuleEnabled(DashboardModuleIds.TimeWeather))
        {
            return;
        }

        _weatherRefreshCts?.Cancel();
        _weatherRefreshCts?.Dispose();
        _weatherRefreshCts = new CancellationTokenSource();

        if (string.IsNullOrEmpty(_weatherService.GetEffectiveApiKey()))
        {
            ApplyWeatherInfo(await _weatherService.GetCachedWeatherAsync());
            WeatherStatusMessage = L("Weather.ConfigureApiKey");
            return;
        }

        IsWeatherLoading = true;
        WeatherStatusMessage = string.Empty;

        try
        {
            var info = await _weatherService.RefreshWeatherAsync(_weatherRefreshCts.Token, notifyUser);
            ApplyWeatherInfo(info ?? await _weatherService.GetCachedWeatherAsync());
        }
        catch (OperationCanceledException)
        {
            ApplyWeatherInfo(await _weatherService.GetCachedWeatherAsync());
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.RefreshWeather");
            ApplyWeatherInfo(await _weatherService.GetCachedWeatherAsync());
            if (notifyUser)
            {
                _notificationService.ShowWarningMessage(L("Weather.RefreshFailed"));
            }
        }
        finally
        {
            IsWeatherLoading = false;
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
        string.Equals(_localizationService.CurrentLanguage, ILocalizationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WindowLockToolTip));
        OnPropertyChanged(nameof(PanelCollapseToolTip));
        OnPropertyChanged(nameof(CalendarWeekdayLabels));
        UpdateClockText();
        RefreshCalendarDays();
        Todos.RefreshCollapsedPanelTodo();
        if (WeatherStatusMessage == "请在设置中配置 API Key" || WeatherStatusMessage == "Configure API Key in Settings")
        {
            WeatherStatusMessage = L("Weather.ConfigureApiKey");
        }
    }

    private string L(string key) => _localizationService.GetString(key);

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        QuickText.Dispose();
        _weatherRefreshTimer.Stop();
        HardwareMonitor.Dispose();
        var weatherRefreshCts = _weatherRefreshCts;
        _weatherRefreshCts = null;
        try
        {
            weatherRefreshCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
        weatherRefreshCts?.Dispose();
        _clockService.Stop();
    }
}
