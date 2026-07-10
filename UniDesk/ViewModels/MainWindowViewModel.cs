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
    private readonly INoteService _noteService;
    private readonly IQuickNoteService _quickNoteService;
    private readonly IQuickTextService _quickTextService;
    private readonly ITodoService _todoService;
    private readonly IShortcutService _shortcutService;
    private readonly IWeatherService _weatherService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private readonly IStartupService _startupService;
    private readonly ITodoBackupService _todoBackupService;
    private readonly ISystemMetricsMonitor _systemMetricsMonitor;
    private readonly DispatcherTimer _weatherRefreshTimer;
    private CancellationTokenSource? _weatherRefreshCts;
    private int _notesLoadGeneration;
    private int _quickNotesLoadGeneration;
    private int _quickTextLoadGeneration;
    private int _todosLoadGeneration;
    private int _shortcutsLoadGeneration;
    private int? _shortcutLimitPreview;
    private List<ShortcutItem> _allShortcuts = [];
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
    private bool _isEditingShortcuts;

    [ObservableProperty]
    private bool _isShortcutDropTargetActive;

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

    [ObservableProperty]
    private ObservableCollection<NoteItem> _notes = new();

    [ObservableProperty]
    private ObservableCollection<QuickNote> _quickNotes = new();

    [ObservableProperty]
    private ObservableCollection<ClipboardHistoryItem> _clipboardHistory = new();

    [ObservableProperty]
    private ObservableCollection<TextSnippet> _textSnippets = new();

    [ObservableProperty]
    private QuickTextMode _selectedQuickTextMode = QuickTextMode.History;

    [ObservableProperty]
    private ObservableCollection<TodoItem> _todos = new();

    [ObservableProperty]
    private ObservableCollection<ShortcutItem> _shortcuts = new();

    /// <summary>快捷方式区展示项（含末尾添加按钮占位）。</summary>
    public ObservableCollection<object> ShortcutDisplayEntries { get; } = new();

    public ObservableCollection<ModuleSetting> ModuleSettings { get; } = new();

    public bool HasShortcuts => _allShortcuts.Count > 0;

    public bool HasQuickNotes => QuickNotes.Count > 0;

    public bool HasClipboardHistory => ClipboardHistory.Count > 0;

    public bool HasTextSnippets => TextSnippets.Count > 0;

    public bool IsQuickTextHistorySelected => SelectedQuickTextMode == QuickTextMode.History;

    public bool IsQuickTextSnippetsSelected => SelectedQuickTextMode == QuickTextMode.Snippets;

    [ObservableProperty]
    private bool _isShortcutAddMenuOpen;

    [ObservableProperty]
    private bool _isSystemAppMenuOpen;

    public IReadOnlyList<SystemAppShortcut> SystemApps => SystemAppCatalog.Apps;

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

    [ObservableProperty]
    private string _systemCpuUsageText = "--";

    [ObservableProperty]
    private string _systemCpuTemperatureText = "CPU --";

    [ObservableProperty]
    private string _systemMemoryUsageText = "--";

    [ObservableProperty]
    private string _systemGpuUsageText = "--";

    [ObservableProperty]
    private string _systemGpuTemperatureText = "GPU --";

    [ObservableProperty]
    private string _systemNetworkReceivedText = "--";

    [ObservableProperty]
    private string _systemNetworkSentText = "--";

    public string WindowLockToolTip => L(IsWindowLocked ? "Header.UnlockWindow" : "Header.LockWindow");

    public string PanelCollapseToolTip => L(IsPanelCollapsed ? "Header.ExpandPanel" : "Header.CollapsePanel");

    [ObservableProperty]
    private TodoItem? _collapsedPanelTodo;

    [ObservableProperty]
    private string _collapsedPanelTodoDueText = string.Empty;

    public bool HasCollapsedPanelTodo => CollapsedPanelTodo != null;

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
        _noteService = noteService;
        _quickNoteService = quickNoteService;
        _quickTextService = quickTextService;
        _todoService = todoService;
        _shortcutService = shortcutService;
        _weatherService = weatherService;
        _hotkeyService = hotkeyService;
        _startupService = startupService;
        _todoBackupService = todoBackupService;
        _systemMetricsMonitor = systemMetricsMonitor;
        _clipboardMonitorService = clipboardMonitorService;
        _clipboardMonitorService.ClipboardHistoryChanged += ClipboardMonitor_OnHistoryChanged;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;

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

        _systemMetricsMonitor.SnapshotAvailable += SystemMetricsMonitor_OnSnapshotAvailable;
        _systemMetricsMonitor.IsEnabled = IsModuleEnabled(DashboardModuleIds.HardwareMonitor);
        _systemMetricsMonitor.Start();

        _ = LoadNotesAsync();
        if (IsModuleEnabled(DashboardModuleIds.QuickNotes))
        {
            _ = LoadQuickNotesAsync();
        }

        if (IsModuleEnabled(DashboardModuleIds.QuickText))
        {
            _ = LoadQuickTextAsync();
        }

        _ = LoadTodosAsync();
        _ = LoadShortcutsAsync();
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

        _systemMetricsMonitor.IsEnabled = IsModuleEnabled(DashboardModuleIds.HardwareMonitor);

        if (IsModuleEnabled(DashboardModuleIds.TimeWeather) && !HasWeatherData)
        {
            _ = InitializeWeatherAsync();
        }

        if (IsModuleEnabled(DashboardModuleIds.QuickNotes) && QuickNotes.Count == 0)
        {
            _ = LoadQuickNotesAsync();
        }

        if (IsModuleEnabled(DashboardModuleIds.QuickText) &&
            ClipboardHistory.Count == 0 &&
            TextSnippets.Count == 0)
        {
            _ = LoadQuickTextAsync();
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

    partial void OnCollapsedPanelTodoChanged(TodoItem? value) => OnPropertyChanged(nameof(HasCollapsedPanelTodo));

    public Task ReloadTodosAsync() => LoadTodosAsync();

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

    public async Task ReloadShortcutsAsync() => await LoadShortcutsAsync();

    [RelayCommand]
    private async Task RefreshNotesAsync() => await LoadNotesAsync();

    [RelayCommand]
    private void NewNote()
    {
        var window = new NoteEditWindow(new NoteEditViewModel(_noteService, _localizationService));
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _ = LoadNotesAsync();
        }
    }

    [RelayCommand]
    private void EditNote(NoteItem? note)
    {
        if (note == null) return;
        var window = new NoteEditWindow(new NoteEditViewModel(_noteService, _localizationService, note));
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _ = LoadNotesAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteNoteAsync(NoteItem? note)
    {
        if (note == null) return;
        if (!_notificationService.ShowConfirmDialog(L("QuickNote.DeleteConfirm"), L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _noteService.DeleteNoteAsync(note.Id);
        await LoadNotesAsync();
    }

    private async Task LoadNotesAsync()
    {
        var generation = Interlocked.Increment(ref _notesLoadGeneration);
        try
        {
            var notes = await _noteService.GetAllNotesAsync();
            if (generation != _notesLoadGeneration) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Notes.Clear();
                foreach (var note in notes.OrderByDescending(n => n.UpdatedAt))
                {
                    Notes.Add(note);
                }
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.LoadNotes");
            if (generation == _notesLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Common.OperationFailed"));
            }
        }
    }

    public async Task ReloadQuickNotesAsync() => await LoadQuickNotesAsync();

    [RelayCommand]
    private void NewQuickNote()
    {
        var window = new QuickNoteEditorWindow(
            new QuickNoteEditorViewModel(_quickNoteService, _notificationService, _localizationService),
            PanelWidth);
        window.Owner = App.Current.MainWindow;
        window.ShowDialog();
        _ = LoadQuickNotesAsync();
    }

    [RelayCommand]
    private void EditQuickNote(QuickNote? note)
    {
        if (note == null)
        {
            return;
        }

        var window = new QuickNoteEditorWindow(
            new QuickNoteEditorViewModel(_quickNoteService, _notificationService, _localizationService, note),
            PanelWidth);
        window.Owner = App.Current.MainWindow;
        window.ShowDialog();
        _ = LoadQuickNotesAsync();
    }

    [RelayCommand]
    private async Task DeleteQuickNoteAsync(QuickNote? note)
    {
        if (note == null)
        {
            return;
        }

        if (!_notificationService.ShowConfirmDialog(L("QuickNote.DeleteConfirm"), L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _quickNoteService.DeleteQuickNoteAsync(note.Id);
        await LoadQuickNotesAsync();
    }

    [RelayCommand]
    private async Task ToggleQuickNotePinnedAsync(QuickNote? note)
    {
        if (note == null)
        {
            return;
        }

        await _quickNoteService.SetPinnedAsync(note.Id, !note.IsPinned);
        await LoadQuickNotesAsync();
    }

    [RelayCommand]
    private void CopyQuickNoteContent(QuickNote? note)
    {
        if (note == null)
        {
            return;
        }

        var text = string.IsNullOrWhiteSpace(note.Content) ? note.Title : note.Content;
        if (string.IsNullOrWhiteSpace(text))
        {
            _notificationService.ShowWarningMessage(L("QuickNote.ContentEmpty"));
            return;
        }

        try
        {
            Clipboard.SetText(text);
            _notificationService.ShowSuccessMessage(L("Common.Copied"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.CopyQuickNoteContent");
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
        }
    }

    private async Task LoadQuickNotesAsync()
    {
        var generation = Interlocked.Increment(ref _quickNotesLoadGeneration);
        try
        {
            var notes = await _quickNoteService.GetAllQuickNotesAsync();
            if (generation != _quickNotesLoadGeneration) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                QuickNotes.Clear();
                foreach (var note in notes.Take(5))
                {
                    QuickNotes.Add(note);
                }

                OnPropertyChanged(nameof(HasQuickNotes));
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.LoadQuickNotes");
            if (generation == _quickNotesLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Common.OperationFailed"));
            }
        }
    }

    public async Task ReloadQuickTextAsync() => await LoadQuickTextAsync();

    partial void OnSelectedQuickTextModeChanged(QuickTextMode value)
    {
        OnPropertyChanged(nameof(IsQuickTextHistorySelected));
        OnPropertyChanged(nameof(IsQuickTextSnippetsSelected));
    }

    private void ClipboardMonitor_OnHistoryChanged()
    {
        if (IsModuleEnabled(DashboardModuleIds.QuickText))
        {
            _ = LoadQuickTextAsync();
        }
    }

    [RelayCommand]
    private void SelectQuickTextMode(string? mode)
    {
        SelectedQuickTextMode = string.Equals(mode, "Snippets", StringComparison.OrdinalIgnoreCase)
            ? QuickTextMode.Snippets
            : QuickTextMode.History;
    }

    [RelayCommand]
    private async Task CopyClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.Content))
        {
            return;
        }

        if (!await _clipboardMonitorService.TrySetTextAsync(item.Content))
        {
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
            return;
        }

        _notificationService.ShowSuccessMessage(L("Common.Copied"));

        try
        {
            await _quickTextService.RecordClipboardTextAsync(item.Content);
            await LoadQuickTextAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.CopyClipboardHistoryAsync.AfterCopy");
        }
    }

    [RelayCommand]
    private async Task DeleteClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        if (item == null)
        {
            return;
        }

        await _quickTextService.DeleteClipboardHistoryAsync(item.Id);
        await LoadQuickTextAsync();
    }

    [RelayCommand]
    private async Task ClearClipboardHistoryAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickText.ClearHistoryConfirm"), L("QuickText.ClearHistoryTitle")))
        {
            return;
        }

        await _quickTextService.ClearClipboardHistoryAsync();
        await LoadQuickTextAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.HistoryCleared"));
    }

    [RelayCommand]
    private async Task FavoriteClipboardHistoryAsync(ClipboardHistoryItem? item)
    {
        var snippet = await _quickTextService.CreateSnippetFromHistoryAsync(item);
        if (snippet == null)
        {
            _notificationService.ShowWarningMessage(L("QuickText.FavoriteFailed"));
            return;
        }

        SelectedQuickTextMode = QuickTextMode.Snippets;
        await LoadQuickTextAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.Favorited"));
    }

    [RelayCommand]
    private async Task CopyTextSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null || string.IsNullOrWhiteSpace(snippet.Content))
        {
            return;
        }

        if (!await _clipboardMonitorService.TrySetTextAsync(snippet.Content))
        {
            _notificationService.ShowWarningMessage(L("Common.CopyFailed"));
            return;
        }

        _notificationService.ShowSuccessMessage(L("Common.Copied"));

        try
        {
            await _quickTextService.MarkSnippetUsedAsync(snippet.Id);
            await LoadQuickTextAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.CopyTextSnippetAsync.AfterCopy");
        }
    }

    [RelayCommand]
    private void NewTextSnippet()
    {
        var window = new TextSnippetEditWindow(
            new TextSnippetEditViewModel(_quickTextService, _localizationService),
            PanelWidth);
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            SelectedQuickTextMode = QuickTextMode.Snippets;
            _ = LoadQuickTextAsync();
        }
    }

    [RelayCommand]
    private void EditTextSnippet(TextSnippet? snippet)
    {
        if (snippet == null)
        {
            return;
        }

        var window = new TextSnippetEditWindow(
            new TextSnippetEditViewModel(_quickTextService, _localizationService, snippet),
            PanelWidth);
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            _ = LoadQuickTextAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteTextSnippetAsync(TextSnippet? snippet)
    {
        if (snippet == null)
        {
            return;
        }

        if (!_notificationService.ShowConfirmDialog(
                _localizationService.Format("QuickText.DeleteSnippetConfirmFormat", snippet.DisplayTitle),
                L("Dialog.DeleteConfirmTitle")))
        {
            return;
        }

        await _quickTextService.DeleteTextSnippetAsync(snippet.Id);
        await LoadQuickTextAsync();
    }

    [RelayCommand]
    private void OpenQuickTextManager()
    {
        var window = new QuickTextManagerWindow(
            new QuickTextManagerViewModel(
                _quickTextService,
                _clipboardMonitorService,
                _notificationService,
                _localizationService,
                PanelWidth),
            PanelWidth)
        {
            Owner = App.Current.MainWindow
        };

        window.ShowDialog();
        _ = LoadQuickTextAsync();
    }

    private async Task LoadQuickTextAsync()
    {
        var generation = Interlocked.Increment(ref _quickTextLoadGeneration);
        try
        {
            var historyTask = _quickTextService.GetClipboardHistoryAsync(5);
            var snippetsTask = _quickTextService.GetTextSnippetsAsync();
            await Task.WhenAll(historyTask, snippetsTask);

            if (generation != _quickTextLoadGeneration) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                ClipboardHistory.Clear();
                foreach (var item in historyTask.Result.Take(5))
                {
                    ClipboardHistory.Add(item);
                }

                TextSnippets.Clear();
                foreach (var snippet in snippetsTask.Result.Take(5))
                {
                    TextSnippets.Add(snippet);
                }

                OnPropertyChanged(nameof(HasClipboardHistory));
                OnPropertyChanged(nameof(HasTextSnippets));
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.LoadQuickText");
        }
    }

    [RelayCommand]
    private async Task AddTodoAsync()
    {
        var window = new TodoEditWindow(new TodoEditViewModel(_todoService, _localizationService), PanelWidth);
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            await LoadTodosAsync();
        }
    }

    [RelayCommand]
    private async Task EditTodoAsync(TodoItem? todo)
    {
        if (todo == null) return;

        var window = new TodoEditWindow(new TodoEditViewModel(_todoService, _localizationService, todo), PanelWidth);
        window.Owner = App.Current.MainWindow;
        if (window.ShowDialog() == true)
        {
            await LoadTodosAsync();
        }
    }

    [RelayCommand]
    private async Task ToggleTodoAsync(TodoItem? todo)
    {
        if (todo == null) return;
        await _todoService.ToggleCompleteAsync(todo.Id);
        await LoadTodosAsync();
    }

    [RelayCommand]
    private async Task DeleteTodoAsync(TodoItem? todo)
    {
        if (todo == null) return;
        await _todoService.DeleteTodoAsync(todo.Id);
        await LoadTodosAsync();
    }

    private async Task LoadTodosAsync()
    {
        var generation = Interlocked.Increment(ref _todosLoadGeneration);
        try
        {
            var todos = await _todoService.GetAllTodosAsync();
            if (generation != _todosLoadGeneration) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Todos.Clear();
                foreach (var todo in TodoSortHelper.Sort(todos))
                {
                    Todos.Add(todo);
                }

                RefreshCollapsedPanelTodo();
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.LoadTodos");
            if (generation == _todosLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Todo.LoadFailed"));
            }
        }
    }

    private void RefreshCollapsedPanelTodo()
    {
        CollapsedPanelTodo = Todos.FirstOrDefault(todo => !todo.IsCompleted) ?? Todos.FirstOrDefault();
        CollapsedPanelTodoDueText = BuildTodoDueText(CollapsedPanelTodo);
    }

    private string BuildTodoDueText(TodoItem? todo)
    {
        if (todo?.DueDate == null)
        {
            return string.Empty;
        }

        var due = todo.DueDate.Value;
        var today = DateTime.Today;
        var hasTime = due.TimeOfDay.TotalSeconds > 0;

        if (due.Date == today)
        {
            return hasTime ? $"{L("Common.Today")} {due:HH:mm}" : L("Common.Today");
        }

        if (due.Date == today.AddDays(1))
        {
            return hasTime ? $"{L("Common.Tomorrow")} {due:HH:mm}" : L("Common.Tomorrow");
        }

        return hasTime
            ? due.ToString("M/d HH:mm", CultureInfo.CurrentCulture)
            : due.ToString("M/d", CultureInfo.CurrentCulture);
    }

    [RelayCommand]
    private async Task LaunchShortcutAsync(ShortcutItem? shortcut)
    {
        if (shortcut == null || IsEditingShortcuts) return;
        await _shortcutService.LaunchShortcutAsync(shortcut.Id);
    }

    [RelayCommand]
    private void ToggleShortcutEdit()
    {
        IsEditingShortcuts = !IsEditingShortcuts;
        if (!IsEditingShortcuts)
        {
            IsShortcutAddMenuOpen = false;
            IsSystemAppMenuOpen = false;
        }
    }

    partial void OnIsEditingShortcutsChanged(bool value)
    {
        if (!value)
        {
            IsShortcutAddMenuOpen = false;
            IsSystemAppMenuOpen = false;
        }

        NotifyShortcutOrderCommandsCanExecuteChanged();
        RefreshShortcutDisplayEntries();
    }

    [RelayCommand]
    private void OpenShortcutAddMenu()
    {
        IsSystemAppMenuOpen = false;
        IsShortcutAddMenuOpen = !IsShortcutAddMenuOpen;
    }

    [RelayCommand]
    private void CloseShortcutAddMenus()
    {
        IsShortcutAddMenuOpen = false;
        IsSystemAppMenuOpen = false;
    }

    [RelayCommand]
    private void OpenSystemAppMenu()
    {
        IsSystemAppMenuOpen = true;
    }

    [RelayCommand]
    private void BackToShortcutAddMenu()
    {
        IsSystemAppMenuOpen = false;
    }

    [RelayCommand]
    private void AddShortcutFromFile()
    {
        CloseShortcutAddMenus();

        var path = ShortcutPickDialogHelper.PickFile();
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        _ = AddShortcutsFromPathsAsync([path]);
    }

    [RelayCommand]
    private void AddShortcutFromFolder()
    {
        CloseShortcutAddMenus();

        var folderDialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = L("Shortcut.SelectFolder")
        };

        if (folderDialog.ShowDialog() != true)
        {
            return;
        }

        _ = AddShortcutsFromPathsAsync([folderDialog.FolderName]);
    }

    [RelayCommand]
    private void AddShortcutFromSystemApp(SystemAppShortcut? app)
    {
        if (app == null)
        {
            return;
        }

        CloseShortcutAddMenus();
        _ = CreateShortcutAndReloadAsync(new ShortcutItem
        {
            Name = app.Name,
            Path = app.Path,
            LaunchArguments = app.LaunchArguments,
            IconLookupPath = app.IconLookupPath ?? app.Path,
            BundledIconFileName = app.BundledIconFileName,
            Type = app.Type
        });
    }

    public void SetShortcutLimitPreview(int? limit)
    {
        _shortcutLimitPreview = limit;
        RefreshVisibleShortcuts();
    }

    private int GetShortcutMaxCount() =>
        _shortcutLimitPreview
        ?? ShortcutLimitHelper.ParseLimit(_settingsService.GetValue("ShortcutMaxCount", ShortcutLimitHelper.DefaultLimit.ToString()));

    private static bool IsDuplicateShortcut(IEnumerable<ShortcutItem> existing, ShortcutItem candidate)
    {
        var candidatePath = NormalizeShortcutPath(candidate.Path);
        return existing.Any(s =>
            string.Equals(NormalizeShortcutPath(s.Path), candidatePath, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(s.LaunchArguments ?? string.Empty, candidate.LaunchArguments ?? string.Empty, StringComparison.Ordinal));
    }

    private async Task CreateShortcutAndReloadAsync(ShortcutItem shortcut)
    {
        var maxCount = GetShortcutMaxCount();
        var allShortcuts = await _shortcutService.GetAllShortcutsAsync();

        if (allShortcuts.Count >= maxCount)
        {
            _notificationService.ShowWarningMessage(_localizationService.Format("Shortcut.LimitExceededFormat", maxCount));
            return;
        }

        if (IsDuplicateShortcut(allShortcuts, shortcut))
        {
            _notificationService.ShowWarningMessage(L("Shortcut.Duplicate"));
            return;
        }

        shortcut.SortOrder = GetNextShortcutSortOrder(allShortcuts);
        var id = await _shortcutService.CreateShortcutAsync(shortcut);
        if (id <= 0)
        {
            _notificationService.ShowWarningMessage(L("Shortcut.AddFailed"));
            return;
        }

        await LoadShortcutsAsync();
    }

    public async Task<ShortcutImportResult> AddShortcutsFromPathsAsync(IEnumerable<string>? paths)
    {
        var result = new ShortcutImportResult();
        var incomingPaths = paths?
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? [];

        if (incomingPaths.Count == 0)
        {
            result.InvalidCount++;
            return result;
        }

        try
        {
            var allShortcuts = await _shortcutService.GetAllShortcutsAsync();
            var maxCount = GetShortcutMaxCount();
            var existingPaths = allShortcuts
                .Select(shortcut => NormalizeShortcutPath(shortcut.Path))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var path in incomingPaths)
            {
                if (!ShortcutPathHelper.IsSupportedPath(path))
                {
                    result.InvalidCount++;
                    continue;
                }

                var normalizedPath = NormalizeShortcutPath(path);
                if (existingPaths.Contains(normalizedPath))
                {
                    result.DuplicateCount++;
                    continue;
                }

                if (allShortcuts.Count >= maxCount)
                {
                    result.LimitSkippedCount++;
                    continue;
                }

                try
                {
                    var shortcut = ShortcutPathHelper.CreateFromPath(path, GetNextShortcutSortOrder(allShortcuts));
                    var id = await _shortcutService.CreateShortcutAsync(shortcut);
                    if (id <= 0)
                    {
                        result.InvalidCount++;
                        continue;
                    }

                    shortcut.Id = id;
                    allShortcuts.Add(shortcut);
                    existingPaths.Add(normalizedPath);
                    result.AddedCount++;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, $"MainWindowViewModel.AddShortcutFromPath: {path}");
                    result.InvalidCount++;
                }
            }

            if (result.HasChanges)
            {
                await LoadShortcutsAsync();
            }

            if (result.AddedCount > 0)
            {
                _notificationService.ShowSuccessMessage(result.ToUserMessage(_localizationService));
            }
            else if (result.DuplicateCount > 0 || result.InvalidCount > 0 || result.LimitSkippedCount > 0)
            {
                _notificationService.ShowWarningMessage(result.ToUserMessage(_localizationService));
            }

            return result;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.AddShortcutsFromPathsAsync");
            _notificationService.ShowErrorMessage(L("Shortcut.DropFailed"));
            result.InvalidCount += incomingPaths.Count;
            return result;
        }
    }

    private static int GetNextShortcutSortOrder(IReadOnlyCollection<ShortcutItem> shortcuts) =>
        shortcuts.Count == 0 ? 0 : shortcuts.Max(shortcut => shortcut.SortOrder) + 1;

    private static string NormalizeShortcutPath(string path)
    {
        try
        {
            var expanded = Environment.ExpandEnvironmentVariables(path).Trim();
            var fullPath = Path.GetFullPath(expanded);
            return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim();
        }
    }

    private void RefreshShortcutDisplayEntries()
    {
        ShortcutDisplayEntries.Clear();
        foreach (var shortcut in Shortcuts)
        {
            ShortcutDisplayEntries.Add(shortcut);
        }

        if (IsEditingShortcuts && Shortcuts.Count < GetShortcutMaxCount())
        {
            ShortcutDisplayEntries.Add(AddShortcutPlaceholder.Instance);
        }

        OnPropertyChanged(nameof(HasShortcuts));
        NotifyShortcutOrderCommandsCanExecuteChanged();
    }

    private void RefreshVisibleShortcuts()
    {
        Shortcuts.Clear();
        foreach (var shortcut in _allShortcuts
                     .OrderBy(shortcut => shortcut.SortOrder)
                     .ThenBy(shortcut => shortcut.CreatedAt)
                     .ThenBy(shortcut => shortcut.Id)
                     .Take(GetShortcutMaxCount()))
        {
            Shortcuts.Add(shortcut);
        }

        RefreshShortcutDisplayEntries();
    }

    [RelayCommand]
    private async Task DeleteShortcutAsync(ShortcutItem? shortcut)
    {
        if (shortcut == null) return;

        await _shortcutService.DeleteShortcutAsync(shortcut.Id);
        await LoadShortcutsAsync();
    }

    [RelayCommand(CanExecute = nameof(CanMoveShortcutUp))]
    private async Task MoveShortcutUpAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index <= 0)
        {
            return;
        }

        await MoveShortcutToIndexAsync(shortcut, index - 1);
    }

    private bool CanMoveShortcutUp(ShortcutItem? shortcut) =>
        GetShortcutIndex(shortcut) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveShortcutDown))]
    private async Task MoveShortcutDownAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index < 0 || index >= _allShortcuts.Count - 1)
        {
            return;
        }

        await MoveShortcutToIndexAsync(shortcut, index + 1);
    }

    private bool CanMoveShortcutDown(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        return index >= 0 && index < _allShortcuts.Count - 1;
    }

    [RelayCommand(CanExecute = nameof(CanMoveShortcutToFirst))]
    private async Task MoveShortcutToFirstAsync(ShortcutItem? shortcut)
    {
        if (GetShortcutIndex(shortcut) <= 0)
        {
            return;
        }

        await MoveShortcutToIndexAsync(shortcut, 0);
    }

    private bool CanMoveShortcutToFirst(ShortcutItem? shortcut) =>
        GetShortcutIndex(shortcut) > 0;

    [RelayCommand(CanExecute = nameof(CanMoveShortcutToLast))]
    private async Task MoveShortcutToLastAsync(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        if (index < 0 || index >= _allShortcuts.Count - 1)
        {
            return;
        }

        await MoveShortcutToIndexAsync(shortcut, _allShortcuts.Count - 1);
    }

    private bool CanMoveShortcutToLast(ShortcutItem? shortcut)
    {
        var index = GetShortcutIndex(shortcut);
        return index >= 0 && index < _allShortcuts.Count - 1;
    }

    public async Task MoveShortcutAsync(ShortcutItem? source, ShortcutItem? target)
    {
        if (source == null || target == null || source.Id == target.Id)
        {
            return;
        }

        var sourceIndex = GetShortcutIndex(source);
        var targetIndex = GetShortcutIndex(target);
        if (sourceIndex < 0 || targetIndex < 0 || sourceIndex == targetIndex)
        {
            return;
        }

        await MoveShortcutToIndexAsync(source, targetIndex);
    }

    private int GetShortcutIndex(ShortcutItem? shortcut)
    {
        if (shortcut == null)
        {
            return -1;
        }

        return _allShortcuts.FindIndex(item => item.Id == shortcut.Id);
    }

    private async Task MoveShortcutToIndexAsync(ShortcutItem? shortcut, int targetIndex)
    {
        var sourceIndex = GetShortcutIndex(shortcut);
        if (shortcut == null || sourceIndex < 0)
        {
            return;
        }

        targetIndex = Math.Clamp(targetIndex, 0, _allShortcuts.Count - 1);
        if (sourceIndex == targetIndex)
        {
            return;
        }

        var ordered = _allShortcuts.ToList();
        ordered.RemoveAt(sourceIndex);
        ordered.Insert(targetIndex, shortcut);
        await ApplyShortcutOrderAsync(ordered);
    }

    private async Task ApplyShortcutOrderAsync(List<ShortcutItem> orderedShortcuts)
    {
        for (var i = 0; i < orderedShortcuts.Count; i++)
        {
            orderedShortcuts[i].SortOrder = i;
        }

        _allShortcuts = orderedShortcuts;
        RefreshVisibleShortcuts();
        await _shortcutService.UpdateSortOrderAsync(_allShortcuts.Select(shortcut => shortcut.Id).ToList());
        NotifyShortcutOrderCommandsCanExecuteChanged();
    }

    private void NotifyShortcutOrderCommandsCanExecuteChanged()
    {
        MoveShortcutUpCommand.NotifyCanExecuteChanged();
        MoveShortcutDownCommand.NotifyCanExecuteChanged();
        MoveShortcutToFirstCommand.NotifyCanExecuteChanged();
        MoveShortcutToLastCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadShortcutsAsync()
    {
        var generation = Interlocked.Increment(ref _shortcutsLoadGeneration);
        try
        {
            await _shortcutService.NormalizeSortOrderAsync();
            var shortcuts = await _shortcutService.GetAllShortcutsAsync();
            if (generation != _shortcutsLoadGeneration) return;

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                _allShortcuts = shortcuts
                    .OrderBy(shortcut => shortcut.SortOrder)
                    .ThenBy(shortcut => shortcut.CreatedAt)
                    .ThenBy(shortcut => shortcut.Id)
                    .ToList();
                RefreshVisibleShortcuts();
            });
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "MainWindowViewModel.LoadShortcuts");
            if (generation == _shortcutsLoadGeneration)
            {
                _notificationService.ShowWarningMessage(L("Shortcut.LoadFailed"));
            }
        }
    }

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

    private void SystemMetricsMonitor_OnSnapshotAvailable(object? sender, SystemMetricsSnapshot metrics)
    {
        if (_disposed || !IsModuleEnabled(DashboardModuleIds.HardwareMonitor))
        {
            return;
        }

        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null || dispatcher.CheckAccess())
        {
            ApplySystemMetrics(metrics);
            return;
        }

        _ = dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && IsModuleEnabled(DashboardModuleIds.HardwareMonitor))
            {
                ApplySystemMetrics(metrics);
            }
        });
    }

    private void ApplySystemMetrics(SystemMetricsSnapshot metrics)
    {
        SystemCpuUsageText = FormatPercent(metrics.CpuUsage);
        SystemCpuTemperatureText = FormatTemperature("CPU", metrics.CpuTemperature);
        SystemMemoryUsageText = FormatPercent(metrics.MemoryUsage);
        SystemGpuUsageText = FormatPercent(metrics.GpuUsage);
        SystemGpuTemperatureText = FormatTemperature("GPU", metrics.GpuTemperature);
        SystemNetworkReceivedText = FormatSpeed(metrics.NetworkReceivedBytesPerSecond);
        SystemNetworkSentText = FormatSpeed(metrics.NetworkSentBytesPerSecond);
    }

    private static string FormatPercent(double? value) => value.HasValue ? $"{value.Value:0}%" : "--";

    private static string FormatTemperature(string label, double? value) =>
        value.HasValue ? $"{label} {value.Value:0}℃" : $"{label} --℃";

    private static string FormatSpeed(double? bytesPerSecond)
    {
        if (!bytesPerSecond.HasValue) return "--";

        var value = Math.Max(0, bytesPerSecond.Value);
        if (value < 1) return "0 B/s";
        if (value < 1024) return $"{value:0} B/s";

        value /= 1024;
        if (value < 1024) return $"{value:0} KB/s";

        value /= 1024;
        if (value < 1024) return $"{value:0.0} MB/s";

        value /= 1024;
        return $"{value:0.0} GB/s";
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
        RefreshCollapsedPanelTodo();
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
        _clipboardMonitorService.ClipboardHistoryChanged -= ClipboardMonitor_OnHistoryChanged;
        _weatherRefreshTimer.Stop();
        _systemMetricsMonitor.SnapshotAvailable -= SystemMetricsMonitor_OnSnapshotAvailable;
        _systemMetricsMonitor.Dispose();
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
