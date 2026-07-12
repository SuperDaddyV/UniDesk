using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
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
    private readonly IQuickTextService _quickTextService;
    private readonly IWeatherService _weatherService;
    private readonly IHotkeyService _hotkeyService;
    private readonly IStartupService _startupService;
    private readonly ITodoBackupService _todoBackupService;
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

    public ObservableCollection<ModuleSetting> ModuleSettings { get; } = new();

    public string WindowLockToolTip => L(IsWindowLocked ? "Header.UnlockWindow" : "Header.LockWindow");

    public string PanelCollapseToolTip => L(IsPanelCollapsed ? "Header.ExpandPanel" : "Header.CollapsePanel");

    public HardwareMonitorViewModel HardwareMonitor { get; }

    public TodosViewModel Todos { get; }

    public QuickNotesViewModel QuickNotes { get; }

    public QuickTextViewModel QuickText { get; }

    public ShortcutsViewModel Shortcuts { get; }

    public TimeWeatherViewModel TimeWeather { get; }

    public SearchViewModel Search { get; }

    public event EventHandler? TodoSearchResultActivated;

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
        IClipboardMonitorService clipboardMonitorService,
        ISearchService searchService)
    {
        _notificationService = notificationService;
        _settingsService = settingsService;
        _localizationService = localizationService;
        _updateService = updateService;
        _windowService = windowService;
        _layoutService = layoutService;
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
        TimeWeather = new TimeWeatherViewModel(
            clockService,
            weatherService,
            notificationService,
            localizationService);
        Search = new SearchViewModel(searchService, localizationService, ActivateSearchResultAsync);
        LoadSettings();
        _layoutService.LoadOrGetDefault();

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

        TimeWeather.IsEnabled = IsModuleEnabled(DashboardModuleIds.TimeWeather);
        if (TimeWeather.IsEnabled && !TimeWeather.HasWeatherData)
        {
            _ = TimeWeather.InitializeAsync();
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

    public HotkeyRegistrationResult ApplyGlobalHotkey(string? hotkey)
    {
        return _hotkeyService.ReplaceHotkey(
            hotkey,
            () =>
            {
                var dispatcher = Application.Current?.Dispatcher;
                if (dispatcher == null || dispatcher.CheckAccess())
                {
                    _windowService.ToggleWindow();
                    return;
                }

                _ = dispatcher.BeginInvoke(
                    DispatcherPriority.Send,
                    () => _windowService.ToggleWindow());
            });
    }

    public Task ReloadShortcutsAsync() => Shortcuts.ReloadAsync();

    public void SetShortcutLimitPreview(int? limit) => Shortcuts.SetShortcutLimitPreview(limit);

    public Task ReloadQuickNotesAsync() => QuickNotes.ReloadAsync();

    public Task ReloadQuickTextAsync() => QuickText.ReloadAsync();

    public Task RefreshWeatherAfterSettingsAsync() => TimeWeather.RefreshAfterSettingsAsync();



    private bool IsChineseLanguage =>
        string.Equals(_localizationService.CurrentLanguage, ILocalizationService.DefaultLanguage, StringComparison.OrdinalIgnoreCase);

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(WindowLockToolTip));
        OnPropertyChanged(nameof(PanelCollapseToolTip));
        Todos.RefreshCollapsedPanelTodo();
    }

    private string L(string key) => _localizationService.GetString(key);

    private async Task ActivateSearchResultAsync(SearchResultItem result)
    {
        switch (result.Kind)
        {
            case SearchResultKind.QuickNote:
                await QuickNotes.OpenSearchResultAsync(result.Id);
                break;
            case SearchResultKind.Todo:
                TodoSearchResultActivated?.Invoke(this, EventArgs.Empty);
                await Todos.HighlightSearchResultAsync(result.Id);
                break;
            case SearchResultKind.Clipboard:
            case SearchResultKind.Snippet:
                await QuickText.CopySearchResultAsync(result);
                break;
            case SearchResultKind.Shortcut:
                await Shortcuts.LaunchSearchResultAsync(result.Id);
                break;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        Search.Dispose();
        QuickText.Dispose();
        HardwareMonitor.Dispose();
        TimeWeather.Dispose();
    }
}
