using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.Windows;

namespace UniDesk.ViewModels;

public partial class SettingsViewModel : ObservableObject, IDisposable
{
    public const string DefaultHotkey = "Ctrl+Alt+Space";

    private readonly ISettingsService _settingsService;
    private readonly ILocalizationService _localizationService;
    private readonly IUpdateService _updateService;
    private readonly IWindowService _windowService;
    private readonly INotificationService _notificationService;
    private readonly ILayoutService _layoutService;
    private readonly IWeatherService _weatherService;
    private readonly IStartupService _startupService;
    private readonly ITodoBackupService _todoBackupService;
    private readonly IQuickTextService _quickTextService;
    private readonly MainWindowViewModel _mainWindowViewModel;
    private readonly ISystemThemeService? _systemThemeService;
    private readonly ISensorDiagnosticsService? _sensorDiagnosticsService;

    private readonly Dictionary<string, string> _originalSettings = new();
    private bool _isLoading;

    [ObservableProperty]
    private string _selectedColorScheme = AppColorSchemeCatalog.DefaultSchemeId;

    [ObservableProperty]
    private bool _followSystemTheme;

    [ObservableProperty]
    private string _colorSchemeLight = AppColorSchemeCatalog.DefaultSchemeId;

    [ObservableProperty]
    private string _colorSchemeDark = "DarkGrey";

    [ObservableProperty]
    private bool _startupEnabled;

    [ObservableProperty]
    private double _windowOpacity;

    [ObservableProperty]
    private double _panelWidth;

    [ObservableProperty]
    private double _panelHeight;

    [ObservableProperty]
    private double _fontScale = 1.0;

    [ObservableProperty]
    private string _displayTitle = "UniDesk";

    [ObservableProperty]
    private string _weatherApiKey = string.Empty;

    [ObservableProperty]
    private string _weatherApiHost = string.Empty;

    [ObservableProperty]
    private bool _isEditingWeatherApi;

    [ObservableProperty]
    private int _shortcutMaxCount = ShortcutLimitHelper.DefaultLimit;

    [ObservableProperty]
    private bool _globalHotkeyEnabled = true;

    [ObservableProperty]
    private string _hotkey = DefaultHotkey;

    [ObservableProperty]
    private string _hotkeyStatusText = string.Empty;

    [ObservableProperty]
    private bool _clipboardHistoryEnabled = true;

    [ObservableProperty]
    private bool _clipboardSensitiveFilterEnabled = true;

    [ObservableProperty]
    private int _clipboardHistoryMaxCount = QuickTextService.DefaultHistoryLimit;

    [ObservableProperty]
    private string _selectedLanguage = ILocalizationService.DefaultLanguage;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private string _updateStatusMessage = string.Empty;

    public IReadOnlyList<int> ClipboardHistoryLimitOptions => QuickTextService.AllowedHistoryLimits;

    public IReadOnlyList<LanguageOption> LanguageOptions => _localizationService.SupportedLanguages;

    public ObservableCollection<ModuleSettingOptionViewModel> ModuleSettings { get; } = new();

    private List<ModuleSetting> _originalModuleSettings = [];

    public string FontScaleLabel => FontScale switch
    {
        <= 0.95 => L("Settings.FontSmall"),
        >= 1.1 => L("Settings.FontLarge"),
        _ => L("Settings.FontNormal")
    };

    public string CurrentVersionText => _localizationService.Format(
        "Update.CurrentVersionFormat",
        AppVersionProvider.CurrentVersionWithPrefix);

    public string ClipboardHistoryCurrentText => _localizationService.Format(
        "Settings.CurrentCountFormat",
        ClipboardHistoryMaxCount);

    public ObservableCollection<ColorSchemeOptionViewModel> ColorSchemes { get; } = new();

    public bool LastSaveSucceeded { get; private set; }

    public bool PendingWeatherSettingsChanged { get; private set; }

    public string PendingApiKey { get; private set; } = string.Empty;

    public string PendingApiHost { get; private set; } = string.Empty;

    public SettingsViewModel(
        ISettingsService settingsService,
        ILocalizationService localizationService,
        IUpdateService updateService,
        IWindowService windowService,
        INotificationService notificationService,
        ILayoutService layoutService,
        IWeatherService weatherService,
        IStartupService startupService,
        ITodoBackupService todoBackupService,
        IQuickTextService quickTextService,
        MainWindowViewModel mainWindowViewModel,
        ISystemThemeService? systemThemeService = null,
        ISensorDiagnosticsService? sensorDiagnosticsService = null)
    {
        _settingsService = settingsService;
        _localizationService = localizationService;
        _updateService = updateService;
        _windowService = windowService;
        _notificationService = notificationService;
        _layoutService = layoutService;
        _weatherService = weatherService;
        _startupService = startupService;
        _todoBackupService = todoBackupService;
        _quickTextService = quickTextService;
        _mainWindowViewModel = mainWindowViewModel;
        _systemThemeService = systemThemeService;
        _sensorDiagnosticsService = sensorDiagnosticsService;

        foreach (var scheme in AppColorSchemeCatalog.All)
        {
            ColorSchemes.Add(new ColorSchemeOptionViewModel(scheme));
        }

        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;
        if (_systemThemeService != null)
        {
            _systemThemeService.ThemeChanged += SystemThemeService_OnThemeChanged;
        }
        LoadSettings();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        try
        {
            SelectedColorScheme = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorScheme", _settingsService.GetValue("Theme", AppColorSchemeCatalog.DefaultSchemeId)));
            FollowSystemTheme = _settingsService.GetSetting("FollowSystemTheme", false);
            ColorSchemeLight = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorSchemeLight", AppColorSchemeCatalog.DefaultSchemeId));
            ColorSchemeDark = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorSchemeDark", "DarkGrey"));

            StartupEnabled = ReadStartupSetting();
            WindowOpacity = _settingsService.GetSetting("WindowOpacity", 0.70);
            PanelWidth = _settingsService.GetSetting("PanelWidth", 320.0);
            PanelHeight = _settingsService.GetSetting("PanelHeight", 702.0);
            FontScale = _settingsService.GetSetting("FontScale", 1.0);
            DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(_settingsService.GetValue("DisplayTitle", "UniDesk"));
            WeatherApiKey = _settingsService.GetValue("WeatherApiKey", "");
            WeatherApiHost = _settingsService.GetValue("WeatherApiHost", "");
            ShortcutMaxCount = ShortcutLimitHelper.ParseLimit(
                _settingsService.GetValue("ShortcutMaxCount", ShortcutLimitHelper.DefaultLimit.ToString()));
            var storedHotkey = _settingsService.GetValue("Hotkey", DefaultHotkey);
            GlobalHotkeyEnabled = !string.IsNullOrWhiteSpace(storedHotkey);
            Hotkey = GlobalHotkeyEnabled && HotkeyGestureParser.TryParse(storedHotkey, out var parsedHotkey)
                ? parsedHotkey.DisplayText
                : DefaultHotkey;
            HotkeyStatusText = GlobalHotkeyEnabled ? string.Empty : L("Hotkey.Disabled");
            ClipboardHistoryEnabled = _settingsService.GetSetting(QuickTextService.HistoryEnabledSettingKey, true);
            ClipboardSensitiveFilterEnabled = _settingsService.GetSetting(QuickTextService.SensitiveFilterSettingKey, true);
            ClipboardHistoryMaxCount = QuickTextService.NormalizeHistoryLimit(
                _settingsService.GetSetting(QuickTextService.HistoryMaxCountSettingKey, QuickTextService.DefaultHistoryLimit));
            SelectedLanguage = _localizationService.NormalizeLanguage(
                _settingsService.GetValue(ILocalizationService.LanguageSettingKey, ILocalizationService.DefaultLanguage));
            SyncSelectedLanguage();
            LoadModuleSettings(_mainWindowViewModel.GetModuleSettingsSnapshot());

            PanelWidth = Math.Clamp(PanelWidth, IWindowService.MinPanelWidth, IWindowService.MaxPanelWidth);
            PanelHeight = Math.Clamp(PanelHeight, IWindowService.MinPanelHeight, IWindowService.MaxPanelHeight);
            FontScale = Math.Clamp(FontScale, 0.9, 1.18);
        }
        finally
        {
            _isLoading = false;
        }

        UpdateColorSchemeSelection();
        ApplyEffectiveThemePreview();
        SaveOriginalSettings();
    }

    private void SaveOriginalSettings()
    {
        _originalSettings["ColorScheme"] = SelectedColorScheme;
        _originalSettings["FollowSystemTheme"] = FollowSystemTheme.ToString();
        _originalSettings["ColorSchemeLight"] = ColorSchemeLight;
        _originalSettings["ColorSchemeDark"] = ColorSchemeDark;
        _originalSettings["Startup"] = StartupEnabled.ToString();
        _originalSettings["WindowOpacity"] = WindowOpacity.ToString(CultureInfo.InvariantCulture);
        _originalSettings["PanelWidth"] = PanelWidth.ToString(CultureInfo.InvariantCulture);
        _originalSettings["PanelHeight"] = PanelHeight.ToString(CultureInfo.InvariantCulture);
        _originalSettings["FontScale"] = FontScale.ToString(CultureInfo.InvariantCulture);
        _originalSettings["DisplayTitle"] = DisplayTitle;
        _originalSettings["WeatherApiKey"] = WeatherApiKey;
        _originalSettings["WeatherApiHost"] = WeatherApiHost;
        _originalSettings["ShortcutMaxCount"] = ShortcutMaxCount.ToString(CultureInfo.InvariantCulture);
        _originalSettings["Hotkey"] = GlobalHotkeyEnabled ? Hotkey : string.Empty;
        _originalSettings["ClipboardHistoryEnabled"] = ClipboardHistoryEnabled.ToString();
        _originalSettings["ClipboardSensitiveFilterEnabled"] = ClipboardSensitiveFilterEnabled.ToString();
        _originalSettings["ClipboardHistoryMaxCount"] = ClipboardHistoryMaxCount.ToString(CultureInfo.InvariantCulture);
        _originalSettings["Language"] = SelectedLanguage;
        _originalModuleSettings = ModuleSettings.Select(module => module.ToModel().Clone()).ToList();
    }

    [RelayCommand]
    private void SelectColorScheme(string? schemeId)
    {
        if (string.IsNullOrWhiteSpace(schemeId))
        {
            return;
        }

        SelectedColorScheme = AppColorSchemeCatalog.NormalizeId(schemeId);
    }

    [RelayCommand]
    private void ToggleWeatherApiEdit() => IsEditingWeatherApi = !IsEditingWeatherApi;

    [RelayCommand]
    private void SelectShortcutLimit(string? limitText)
    {
        if (!int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit)
            || !ShortcutLimitHelper.AllowedLimits.Contains(limit))
        {
            return;
        }

        ShortcutMaxCount = limit;
    }

    [RelayCommand]
    private void RestoreDefaultHotkey()
    {
        GlobalHotkeyEnabled = true;
        Hotkey = DefaultHotkey;
        HotkeyStatusText = string.Empty;
    }

    [RelayCommand]
    private void SelectLanguage(string? language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return;
        }

        SelectedLanguage = _localizationService.NormalizeLanguage(language);
    }

    [RelayCommand]
    private void SelectClipboardHistoryLimit(string? limitText)
    {
        if (!int.TryParse(limitText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var limit))
        {
            return;
        }

        ClipboardHistoryMaxCount = QuickTextService.NormalizeHistoryLimit(limit);
    }

    [RelayCommand]
    private async Task ClearClipboardHistoryFromSettingsAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickText.ClearHistoryConfirm"), L("QuickText.ClearHistoryTitle")))
        {
            return;
        }

        await _quickTextService.ClearClipboardHistoryAsync();
        await _mainWindowViewModel.ReloadQuickTextAsync();
        _notificationService.ShowSuccessMessage(L("QuickText.HistoryCleared"));
    }

    [RelayCommand]
    private void MoveModuleUp(ModuleSettingOptionViewModel? module)
    {
        if (module == null)
        {
            return;
        }

        var index = ModuleSettings.IndexOf(module);
        if (index <= 0)
        {
            return;
        }

        ModuleSettings.Move(index, index - 1);
        RefreshModuleSortState();
        ApplyModulePreview();
    }

    [RelayCommand]
    private void MoveModuleDown(ModuleSettingOptionViewModel? module)
    {
        if (module == null)
        {
            return;
        }

        var index = ModuleSettings.IndexOf(module);
        if (index < 0 || index >= ModuleSettings.Count - 1)
        {
            return;
        }

        ModuleSettings.Move(index, index + 1);
        RefreshModuleSortState();
        ApplyModulePreview();
    }

    partial void OnSelectedColorSchemeChanged(string value)
    {
        UpdateColorSchemeSelection();
        if (!_isLoading)
        {
            ApplyEffectiveThemePreview();
        }
    }

    partial void OnFollowSystemThemeChanged(bool value)
    {
        if (!_isLoading)
        {
            ApplyEffectiveThemePreview();
        }
    }

    partial void OnColorSchemeLightChanged(string value)
    {
        if (!_isLoading)
        {
            ApplyEffectiveThemePreview();
        }
    }

    partial void OnColorSchemeDarkChanged(string value)
    {
        if (!_isLoading)
        {
            ApplyEffectiveThemePreview();
        }
    }

    partial void OnWindowOpacityChanged(double value) => ApplyWindowPreview();

    partial void OnPanelWidthChanged(double value) => ApplyWindowPreview();

    partial void OnPanelHeightChanged(double value) => ApplyWindowPreview();

    partial void OnFontScaleChanged(double value)
    {
        OnPropertyChanged(nameof(FontScaleLabel));
        ApplyWindowPreview();
    }

    partial void OnDisplayTitleChanged(string value) => ApplyWindowPreview();

    partial void OnClipboardHistoryMaxCountChanged(int value) =>
        OnPropertyChanged(nameof(ClipboardHistoryCurrentText));

    partial void OnSelectedLanguageChanged(string value)
    {
        var normalized = _localizationService.NormalizeLanguage(value);
        if (!string.Equals(value, normalized, StringComparison.Ordinal))
        {
            SelectedLanguage = normalized;
            return;
        }

        if (!_isLoading)
        {
            _localizationService.SetLanguage(normalized);
        }
    }

    partial void OnShortcutMaxCountChanged(int value) =>
        _mainWindowViewModel.SetShortcutLimitPreview(value);

    partial void OnGlobalHotkeyEnabledChanged(bool value)
    {
        HotkeyStatusText = value ? string.Empty : L("Hotkey.Disabled");
    }

    private void LoadModuleSettings(IEnumerable<ModuleSetting> modules)
    {
        foreach (var module in ModuleSettings)
        {
            module.PropertyChanged -= ModuleSetting_OnPropertyChanged;
        }

        ModuleSettings.Clear();
        foreach (var module in DashboardModuleCatalog.Normalize(modules))
        {
            var option = ModuleSettingOptionViewModel.FromModel(module);
            option.DisplayName = GetModuleDisplayName(option.ModuleId, option.DisplayName);
            option.PropertyChanged += ModuleSetting_OnPropertyChanged;
            ModuleSettings.Add(option);
        }

        RefreshModuleSortState();
    }

    private void ModuleSetting_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoading || e.PropertyName != nameof(ModuleSettingOptionViewModel.IsEnabled))
        {
            return;
        }

        ApplyModulePreview();
    }

    private void RefreshModuleSortState()
    {
        for (var i = 0; i < ModuleSettings.Count; i++)
        {
            ModuleSettings[i].SortOrder = i;
            ModuleSettings[i].CanMoveUp = i > 0;
            ModuleSettings[i].CanMoveDown = i < ModuleSettings.Count - 1;
        }
    }

    private List<ModuleSetting> BuildModuleSettings()
    {
        RefreshModuleSortState();
        return DashboardModuleCatalog.Normalize(ModuleSettings.Select(module => module.ToModel()));
    }

    private void ApplyModulePreview()
    {
        if (_isLoading)
        {
            return;
        }

        _mainWindowViewModel.ApplyModuleSettings(BuildModuleSettings(), persist: false);
    }

    private void UpdateColorSchemeSelection()
    {
        foreach (var option in ColorSchemes)
        {
            option.IsSelected = string.Equals(option.Id, SelectedColorScheme, StringComparison.OrdinalIgnoreCase);
        }
    }

    private void SystemThemeService_OnThemeChanged(object? sender, bool isLightTheme) =>
        ApplyEffectiveThemePreview();

    private void ApplyEffectiveThemePreview()
    {
        var effective = SystemThemeSelection.GetEffectiveScheme(
            FollowSystemTheme,
            _systemThemeService?.IsLightTheme ?? true,
            SelectedColorScheme,
            ColorSchemeLight,
            ColorSchemeDark);
        AppColorSchemeCatalog.Apply(effective);
    }

    private void ApplyWindowPreview()
    {
        if (_isLoading)
        {
            return;
        }

        _windowService.SetOpacity(WindowOpacity);
        _windowService.SetWidth(PanelWidth);
        if (!_mainWindowViewModel.IsPanelCollapsed)
        {
            _windowService.SetHeight(PanelHeight);
        }

        _mainWindowViewModel.WindowOpacity = WindowOpacity;
        _mainWindowViewModel.PanelWidth = PanelWidth;
        _mainWindowViewModel.PanelHeight = PanelHeight;
        _mainWindowViewModel.FontScale = FontScale;
        _mainWindowViewModel.DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(DisplayTitle);
    }

    [RelayCommand]
    private async Task Save()
    {
        LastSaveSucceeded = false;
        var weatherSettingsChanged = false;
        var apiKeyToValidate = string.Empty;
        var apiHostToValidate = string.Empty;
        var originalHotkey = _originalSettings.GetValueOrDefault("Hotkey", DefaultHotkey);
        var requestedHotkey = GlobalHotkeyEnabled ? Hotkey : string.Empty;
        var hotkeySettingChanged = !string.Equals(
            originalHotkey,
            requestedHotkey,
            StringComparison.OrdinalIgnoreCase);
        var hotkeyToPersist = originalHotkey;
        var hotkeyWasApplied = false;

        try
        {
            if (hotkeySettingChanged)
            {
                var hotkeyResult = _mainWindowViewModel.ApplyGlobalHotkey(requestedHotkey);
                if (!hotkeyResult.Success)
                {
                    HotkeyStatusText = BuildHotkeyFailureMessage(hotkeyResult, requestedHotkey);
                    _notificationService.ShowWarningMessage(HotkeyStatusText);
                    return;
                }

                hotkeyToPersist = hotkeyResult.NormalizedHotkey;
                hotkeyWasApplied = true;
            }

            apiKeyToValidate = WeatherApiKey.Trim();
            apiHostToValidate = QWeatherApiClient.NormalizeHost(WeatherApiHost.Trim());
            DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(DisplayTitle);
            weatherSettingsChanged =
                _originalSettings.GetValueOrDefault("WeatherApiKey") != apiKeyToValidate ||
                _originalSettings.GetValueOrDefault("WeatherApiHost") != apiHostToValidate;

            _settingsService.SetValue("ColorScheme", SelectedColorScheme);
            _settingsService.SetValue("Theme", SelectedColorScheme);
            _settingsService.SetValue("FollowSystemTheme", FollowSystemTheme.ToString());
            _settingsService.SetValue("ColorSchemeLight", AppColorSchemeCatalog.NormalizeId(ColorSchemeLight));
            _settingsService.SetValue("ColorSchemeDark", AppColorSchemeCatalog.NormalizeId(ColorSchemeDark));
            _settingsService.SetValue("Startup", StartupEnabled.ToString());
            _settingsService.SetValue("WindowOpacity", WindowOpacity.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue("PanelWidth", PanelWidth.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue("PanelHeight", PanelHeight.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue("FontScale", FontScale.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue("DisplayTitle", MainWindowViewModel.NormalizeDisplayTitle(DisplayTitle));
            _settingsService.SetValue("WeatherApiKey", apiKeyToValidate);
            _settingsService.SetValue("WeatherApiHost", apiHostToValidate);
            _settingsService.SetValue("ShortcutMaxCount", ShortcutMaxCount.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue("Hotkey", hotkeyToPersist);
            _settingsService.SetValue(QuickTextService.HistoryEnabledSettingKey, ClipboardHistoryEnabled.ToString());
            _settingsService.SetValue(QuickTextService.SensitiveFilterSettingKey, ClipboardSensitiveFilterEnabled.ToString());
            _settingsService.SetValue(QuickTextService.HistoryMaxCountSettingKey, ClipboardHistoryMaxCount.ToString(CultureInfo.InvariantCulture));
            _settingsService.SetValue(ILocalizationService.LanguageSettingKey, _localizationService.NormalizeLanguage(SelectedLanguage));
            _mainWindowViewModel.ApplyModuleSettings(BuildModuleSettings(), persist: true);

            await _settingsService.FlushPendingSavesAsync();
            await _quickTextService.TrimClipboardHistoryAsync(ClipboardHistoryMaxCount);

            ApplyEffectiveThemePreview();
            ApplyWindowPreview();
            _mainWindowViewModel.SetShortcutLimitPreview(null);
            await _mainWindowViewModel.ReloadShortcutsAsync();
            await _mainWindowViewModel.ReloadQuickTextAsync();
            ApplyStartupSetting();
            SaveOriginalSettings();

            PendingApiKey = apiKeyToValidate;
            PendingApiHost = apiHostToValidate;
            PendingWeatherSettingsChanged = weatherSettingsChanged;
            LastSaveSucceeded = true;
            IsEditingWeatherApi = false;
        }
        catch (Exception ex)
        {
            if (hotkeyWasApplied)
            {
                _mainWindowViewModel.ApplyGlobalHotkey(originalHotkey);
            }
            RevertToOriginalSettings();
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.SaveFailedFormat", ex.Message));
            return;
        }

        RequestClose?.Invoke(this, true);
    }

    public async Task CompleteSaveFollowUpAsync(
        string apiKeyToValidate,
        string apiHostToValidate,
        bool weatherSettingsChanged)
    {
        if (!LastSaveSucceeded)
        {
            return;
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(apiKeyToValidate))
            {
                var validation = await _weatherService.ValidateApiKeyAsync(apiKeyToValidate, apiHostToValidate);
                if (!validation.IsValid)
                {
                    _notificationService.ShowWarningMessage(
                        string.IsNullOrWhiteSpace(validation.Message)
                            ? L("Settings.WeatherCredentialValidationFailed")
                            : validation.Message);
                }
            }

            if (weatherSettingsChanged)
            {
                await _mainWindowViewModel.RefreshWeatherAfterSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.WeatherApplyFailedFormat", ex.Message));
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        RequestClose?.Invoke(this, false);
    }

    [RelayCommand]
    private void ResetToDefaults()
    {
        var result = _notificationService.ShowConfirmDialog(L("Settings.ResetDefaultsConfirm"), L("Settings.ResetConfirmTitle"));
        if (!result) return;

        SelectedColorScheme = AppColorSchemeCatalog.DefaultSchemeId;
        FollowSystemTheme = false;
        ColorSchemeLight = AppColorSchemeCatalog.DefaultSchemeId;
        ColorSchemeDark = "DarkGrey";
        StartupEnabled = false;
        WindowOpacity = 0.70;
        PanelWidth = 320;
        PanelHeight = 702;
        FontScale = 1.0;
        DisplayTitle = "UniDesk";
        WeatherApiKey = "";
        WeatherApiHost = "";
        IsEditingWeatherApi = false;
        ShortcutMaxCount = ShortcutLimitHelper.DefaultLimit;
        GlobalHotkeyEnabled = true;
        Hotkey = DefaultHotkey;
        HotkeyStatusText = string.Empty;
        ClipboardHistoryEnabled = true;
        ClipboardSensitiveFilterEnabled = true;
        ClipboardHistoryMaxCount = QuickTextService.DefaultHistoryLimit;
        SelectedLanguage = ILocalizationService.DefaultLanguage;
        LoadModuleSettings(DashboardModuleCatalog.CreateDefaultModules());
        ApplyModulePreview();

        _notificationService.ShowInfoMessage(L("Settings.DefaultsRestored"));
    }

    [RelayCommand]
    private async Task BackupTodosAsync()
    {
        var includeClipboardHistory = _notificationService.ShowConfirmDialog(
            L("Settings.IncludeClipboardHistoryPrompt"),
            L("Settings.BackupTitle"));
        if (includeClipboardHistory &&
            !_notificationService.ShowConfirmDialog(
                L("Settings.ClipboardPlaintextWarning"),
                L("Settings.BackupTitle")))
        {
            return;
        }

        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Title = L("Settings.BackupTitle"),
            Filter = L("Settings.JsonFilter"),
            FileName = $"UniDesk-data-{DateTime.Now:yyyyMMdd-HHmm}.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            await _todoBackupService.ExportToFileAsync(
                dialog.FileName,
                new BackupExportOptions(includeClipboardHistory));
            _notificationService.ShowSuccessMessage(L("Settings.BackupSuccess"));
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.BackupFailedFormat", ex.Message));
        }
    }

    [RelayCommand]
    private async Task RestoreTodosAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = L("Settings.RestoreTitle"),
            Filter = L("Settings.JsonFilter")
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var plan = await _todoBackupService.PrepareImportAsync(dialog.FileName);
            var previewWindow = new BackupImportPreviewWindow(plan.Preview, _localizationService)
            {
                Owner = Application.Current.Windows
                    .OfType<Window>()
                    .FirstOrDefault(window => ReferenceEquals(window.DataContext, this))
                    ?? App.Current.MainWindow
            };
            if (previewWindow.ShowDialog() != true)
            {
                return;
            }

            var result = await _todoBackupService.ApplyImportAsync(plan);
            if (result.SettingCount > 0)
            {
                var startupText = _settingsService.GetValue("Startup", StartupEnabled.ToString());
                StartupEnabled = bool.TryParse(startupText, out var startupEnabled) && startupEnabled;
                ApplyStartupSetting();
                _mainWindowViewModel.ApplyWindowSettings();
                LoadSettings();
                _localizationService.SetLanguage(SelectedLanguage);
            }

            await _mainWindowViewModel.ReloadShortcutsAsync();
            await _mainWindowViewModel.ReloadTodosAsync();
            await _mainWindowViewModel.ReloadQuickNotesAsync();
            await _mainWindowViewModel.ReloadQuickTextAsync();
            _notificationService.ShowSuccessMessage(_localizationService.Format(
                "Settings.RestoreSuccessFormat",
                result.SettingCount,
                result.ShortcutCount,
                result.TodoCount,
                result.QuickNoteCount,
                result.ClipboardHistoryCount,
                result.TextSnippetCount));
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.RestoreFailedFormat", ex.Message));
        }
    }

    [RelayCommand]
    private void ResetLayout()
    {
        var result = _notificationService.ShowConfirmDialog(L("Settings.ResetLayoutConfirm"), L("Settings.ResetConfirmTitle"));
        if (!result) return;

        try
        {
            _layoutService.ResetToDefault();
            _notificationService.ShowSuccessMessage(L("Settings.ResetLayoutSuccess"));
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(_localizationService.Format("Settings.ResetLayoutFailedFormat", ex.Message));
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (IsCheckingForUpdates)
        {
            return;
        }

        IsCheckingForUpdates = true;
        UpdateStatusMessage = L("Update.Checking");
        try
        {
            var result = await _updateService.CheckForUpdatesAsync();
            UpdateStatusMessage = BuildUpdateStatusMessage(result);

            switch (result.Status)
            {
                case UpdateCheckStatus.UpdateAvailable:
                    if (UpdateResultWindow.Show(result, _localizationService))
                    {
                        OpenReleasePage(result.ReleaseUrl);
                    }
                    break;
                case UpdateCheckStatus.Latest:
                    _notificationService.ShowInfoMessage(L("Update.Latest"));
                    break;
                case UpdateCheckStatus.CurrentNewerThanLatest:
                    _notificationService.ShowInfoMessage(L("Update.CurrentNewer"));
                    break;
                default:
                    _notificationService.ShowWarningMessage(L("Update.Failed"));
                    break;
            }
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    public void RevertChanges()
    {
        try
        {
            IsEditingWeatherApi = false;
            RevertToOriginalSettings();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.RevertChanges");
        }
    }

    private void RevertToOriginalSettings()
    {
        _isLoading = true;
        try
        {
            if (_originalSettings.TryGetValue("ColorScheme", out var scheme))
            {
                SelectedColorScheme = AppColorSchemeCatalog.NormalizeId(scheme);
            }

            if (_originalSettings.TryGetValue("FollowSystemTheme", out var followSystemTheme))
            {
                FollowSystemTheme = bool.Parse(followSystemTheme);
            }

            if (_originalSettings.TryGetValue("ColorSchemeLight", out var lightScheme))
            {
                ColorSchemeLight = AppColorSchemeCatalog.NormalizeId(lightScheme);
            }

            if (_originalSettings.TryGetValue("ColorSchemeDark", out var darkScheme))
            {
                ColorSchemeDark = AppColorSchemeCatalog.NormalizeId(darkScheme);
            }

            if (_originalSettings.TryGetValue("Startup", out var startup))
            {
                StartupEnabled = bool.Parse(startup);
            }

            if (_originalSettings.TryGetValue("WindowOpacity", out var opacity))
            {
                WindowOpacity = double.Parse(opacity, CultureInfo.InvariantCulture);
            }

            if (_originalSettings.TryGetValue("PanelWidth", out var width))
            {
                PanelWidth = double.Parse(width, CultureInfo.InvariantCulture);
            }

            if (_originalSettings.TryGetValue("PanelHeight", out var height))
            {
                PanelHeight = double.Parse(height, CultureInfo.InvariantCulture);
            }

            if (_originalSettings.TryGetValue("FontScale", out var fontScale))
            {
                FontScale = double.Parse(fontScale, CultureInfo.InvariantCulture);
            }

            if (_originalSettings.TryGetValue("DisplayTitle", out var displayTitle))
            {
                DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(displayTitle);
            }

            if (_originalSettings.TryGetValue("WeatherApiKey", out var apiKey))
            {
                WeatherApiKey = apiKey;
            }

            if (_originalSettings.TryGetValue("WeatherApiHost", out var apiHost))
            {
                WeatherApiHost = apiHost;
            }

            if (_originalSettings.TryGetValue("ShortcutMaxCount", out var shortcutMaxCount))
            {
                ShortcutMaxCount = ShortcutLimitHelper.ParseLimit(shortcutMaxCount);
            }

            if (_originalSettings.TryGetValue("Hotkey", out var hotkey))
            {
                GlobalHotkeyEnabled = !string.IsNullOrWhiteSpace(hotkey);
                Hotkey = GlobalHotkeyEnabled ? hotkey : DefaultHotkey;
                HotkeyStatusText = GlobalHotkeyEnabled ? string.Empty : L("Hotkey.Disabled");
            }

            if (_originalSettings.TryGetValue("ClipboardHistoryEnabled", out var clipboardHistoryEnabled))
            {
                ClipboardHistoryEnabled = bool.Parse(clipboardHistoryEnabled);
            }

            if (_originalSettings.TryGetValue("ClipboardSensitiveFilterEnabled", out var clipboardSensitiveFilterEnabled))
            {
                ClipboardSensitiveFilterEnabled = bool.Parse(clipboardSensitiveFilterEnabled);
            }

            if (_originalSettings.TryGetValue("ClipboardHistoryMaxCount", out var clipboardHistoryMaxCount))
            {
                ClipboardHistoryMaxCount = QuickTextService.NormalizeHistoryLimit(
                    int.Parse(clipboardHistoryMaxCount, CultureInfo.InvariantCulture));
            }

            if (_originalSettings.TryGetValue("Language", out var language))
            {
                SelectedLanguage = _localizationService.NormalizeLanguage(language);
                SyncSelectedLanguage();
            }

            LoadModuleSettings(_originalModuleSettings);
        }
        finally
        {
            _isLoading = false;
        }

        ApplyEffectiveThemePreview();
        ApplyWindowPreview();
        _mainWindowViewModel.SetShortcutLimitPreview(null);
        _ = _mainWindowViewModel.ReloadShortcutsAsync();
        _mainWindowViewModel.ApplyModuleSettings(_originalModuleSettings, persist: false);
        _localizationService.SetLanguage(SelectedLanguage);
    }

    private bool ReadStartupSetting()
    {
        var isEnabled = _startupService.IsEnabled;
        _settingsService.SetValue("Startup", isEnabled.ToString());
        return isEnabled;
    }

    private void ApplyStartupSetting()
    {
        var desired = StartupEnabled;
        var wasEnabled = _startupService.IsEnabled;

        _startupService.SyncWithSetting(desired);

        if (desired && !_startupService.IsEnabled)
        {
            StartupEnabled = false;
            _settingsService.SetValue("Startup", "false");
            _notificationService.ShowWarningMessage(L("Settings.StartupEnableFailed"));
        }
        else if (!desired && wasEnabled && _startupService.IsEnabled)
        {
            StartupEnabled = true;
            _settingsService.SetValue("Startup", "true");
            _notificationService.ShowWarningMessage(L("Settings.StartupDisableFailed"));
        }
    }

    private string BuildUpdateStatusMessage(UpdateCheckResult result) => result.Status switch
    {
        UpdateCheckStatus.UpdateAvailable => _localizationService.Format("Update.AvailableFormat", result.LatestVersion),
        UpdateCheckStatus.Latest => L("Update.Latest"),
        UpdateCheckStatus.CurrentNewerThanLatest => L("Update.CurrentNewer"),
        _ => L("Update.Failed")
    };

    private string BuildHotkeyFailureMessage(HotkeyRegistrationResult result, string requestedHotkey)
    {
        if (result.Failure == HotkeyRegistrationFailure.InvalidGesture)
        {
            return _localizationService.Format("Hotkey.InvalidFormat", requestedHotkey);
        }

        return result.ErrorCode == 1409
            ? _localizationService.Format("Hotkey.AlreadyInUse", result.NormalizedHotkey)
            : _localizationService.Format(
                "Hotkey.RegisterFailedFormat",
                result.NormalizedHotkey,
                result.ErrorCode);
    }

    [RelayCommand]
    private async Task ExportSensorDiagnosticsAsync()
    {
        if (_sensorDiagnosticsService == null)
        {
            _notificationService.ShowErrorMessage(L("Settings.HardwareDiagnosticsUnavailable"));
            return;
        }

        try
        {
            var path = await _sensorDiagnosticsService.ExportDiagnosticsAsync();
            _notificationService.ShowSuccessMessage(
                _localizationService.Format("Settings.HardwareDiagnosticsSuccessFormat", path));
            try
            {
                Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
        catch (Exception ex)
        {
            _notificationService.ShowErrorMessage(
                _localizationService.Format("Settings.HardwareDiagnosticsFailedFormat", ex.Message));
        }
    }

    private void OpenReleasePage(string releaseUrl)
    {
        if (string.IsNullOrWhiteSpace(releaseUrl))
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(releaseUrl)
            {
                UseShellExecute = true
            });
        }
        catch
        {
            _notificationService.ShowWarningMessage(_localizationService.Format("Update.OpenFailedFormat", releaseUrl));
        }
    }

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        OnPropertyChanged(nameof(FontScaleLabel));
        OnPropertyChanged(nameof(CurrentVersionText));
        OnPropertyChanged(nameof(ClipboardHistoryCurrentText));
        RefreshModuleDisplayNames();
        UpdateStatusMessage = string.Empty;
    }

    private void RefreshModuleDisplayNames()
    {
        foreach (var module in ModuleSettings)
        {
            module.DisplayName = GetModuleDisplayName(module.ModuleId, module.DisplayName);
        }
    }

    private string GetModuleDisplayName(string moduleId, string fallback) => moduleId switch
    {
        DashboardModuleIds.TimeWeather => L("Module.TimeWeather"),
        DashboardModuleIds.HardwareMonitor => L("Module.HardwareMonitor"),
        DashboardModuleIds.Shortcuts => L("Module.Shortcuts"),
        DashboardModuleIds.Todos => L("Module.Todos"),
        DashboardModuleIds.QuickNotes => L("Module.QuickNotes"),
        DashboardModuleIds.QuickText => L("Module.QuickText"),
        _ => string.IsNullOrWhiteSpace(fallback) ? moduleId : fallback
    };

    private void SyncSelectedLanguage()
    {
        var normalized = _localizationService.NormalizeLanguage(SelectedLanguage);
        if (!string.Equals(SelectedLanguage, normalized, StringComparison.Ordinal))
        {
            SelectedLanguage = normalized;
        }
    }

    private string L(string key) => _localizationService.GetString(key);

    public void Dispose()
    {
        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        if (_systemThemeService != null)
        {
            _systemThemeService.ThemeChanged -= SystemThemeService_OnThemeChanged;
        }
    }

    public event EventHandler<bool>? RequestClose;
}
