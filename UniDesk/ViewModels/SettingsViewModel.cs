using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using UniDesk.Hardware.Contracts;
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
    private readonly IHardwareMonitoringMaintenanceService? _hardwareMonitoringMaintenanceService;
    private readonly IMonitorWorkAreaProvider _monitorWorkAreas;

    private readonly Dictionary<string, string> _originalSettings = new();
    private bool _isLoading;
    private bool _isUpdatingPanelSlider;

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
    private double _panelWidthSliderValue;

    [ObservableProperty]
    private double _panelHeightSliderValue;

    [ObservableProperty]
    private double _fontScale = 1.0;

    [ObservableProperty]
    private string _displayTitle = "UniDesk";

    [ObservableProperty]
    private string _city = string.Empty;

    [ObservableProperty]
    private bool _autoLocation;

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

    [ObservableProperty]
    private string _hardwareMonitoringStatusText = string.Empty;

    [ObservableProperty]
    private bool _isHardwareMonitoringRepairVisible;

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

    public double PanelWidthMinimum => GetCurrentPanelSizeBounds().MinWidth;

    public double PanelWidthMaximum => GetCurrentPanelSizeBounds().MaxWidth;

    public double PanelHeightMinimum => GetCurrentPanelSizeBounds().MinHeight;

    public double PanelHeightMaximum => GetCurrentPanelSizeBounds().MaxHeight;

    public ObservableCollection<ColorSchemeOptionViewModel> ColorSchemes { get; } = new();

    public bool LastSaveSucceeded { get; private set; }

    public bool PendingWeatherSettingsChanged { get; private set; }

    public bool PendingLocationSettingsChanged { get; private set; }

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
        ISensorDiagnosticsService? sensorDiagnosticsService = null,
        IHardwareMonitoringMaintenanceService? hardwareMonitoringMaintenanceService = null,
        IMonitorWorkAreaProvider? monitorWorkAreas = null)
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
        _hardwareMonitoringMaintenanceService = hardwareMonitoringMaintenanceService;
        _monitorWorkAreas = monitorWorkAreas ?? Win32MonitorWorkAreaProvider.Instance;

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
        _ = RefreshHardwareMonitoringStatusAsync();
    }

    private void LoadSettings()
    {
        _isLoading = true;
        try
        {
            SelectedColorScheme = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorScheme", _settingsService.GetValue("Theme", AppColorSchemeCatalog.DefaultSchemeId)));
            FollowSystemTheme = _settingsService.GetSetting("FollowSystemTheme", true);
            ColorSchemeLight = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorSchemeLight", AppColorSchemeCatalog.DefaultSchemeId));
            ColorSchemeDark = AppColorSchemeCatalog.NormalizeId(
                _settingsService.GetValue("ColorSchemeDark", "DarkGrey"));

            var recommendedSize = PanelSizePolicy.GetRecommendedSize(GetCurrentWorkArea().WorkArea);
            StartupEnabled = ReadStartupSetting();
            WindowOpacity = _settingsService.GetSetting("WindowOpacity", 0.70);
            PanelWidth = _settingsService.GetSetting("PanelWidth", recommendedSize.Width);
            PanelHeight = _settingsService.GetSetting("PanelHeight", recommendedSize.Height);
            FontScale = _settingsService.GetSetting("FontScale", 1.0);
            DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(_settingsService.GetValue("DisplayTitle", "UniDesk"));
            City = WeatherCityNormalizer.Normalize(
                _settingsService.GetValue("City", "")) ?? string.Empty;
            AutoLocation = _settingsService.GetSetting("AutoLocation", false);
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

        RefreshPanelSliderValues();
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
        _originalSettings["City"] = City;
        _originalSettings["AutoLocation"] = AutoLocation.ToString();
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
    private void FitCurrentScreen()
    {
        var recommendedSize = PanelSizePolicy.GetRecommendedSize(GetCurrentWorkArea().WorkArea);
        PanelWidth = PanelSizePolicy.ClampPreferredWidth(recommendedSize.Width);
        PanelHeight = PanelSizePolicy.ClampPreferredHeight(recommendedSize.Height);
        ApplyWindowPreview();
    }

    [RelayCommand]
    private void OpenLocationSettings()
    {
        try
        {
            var process = Process.Start(new ProcessStartInfo("ms-settings:privacy-location")
            {
                UseShellExecute = true
            });
            if (process == null)
            {
                _notificationService.ShowWarningMessage(L("Settings.OpenLocationSettingsFailed"));
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.OpenLocationSettings");
            _notificationService.ShowWarningMessage(L("Settings.OpenLocationSettingsFailed"));
        }
    }

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
    private async Task ClearClipboardHistoryFromSettingsAsync()
    {
        if (!_notificationService.ShowConfirmDialog(L("QuickText.ClearHistoryConfirm"), L("QuickText.ClearHistoryTitle")))
        {
            return;
        }

        try
        {
            await _quickTextService.ClearClipboardHistoryAsync();
            await _mainWindowViewModel.ReloadQuickTextAsync();
            _notificationService.ShowSuccessMessage(L("QuickText.HistoryCleared"));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.ClearClipboardHistory");
            _notificationService.ShowErrorMessage(L("QuickText.ClearHistoryFailed"));
        }
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

    partial void OnPanelWidthChanged(double value)
    {
        if (!_isLoading)
        {
            SetPanelWidthSliderValue(
                PanelSizePolicy.ClampActualWidth(value, GetCurrentWorkArea().WorkArea));
        }

        ApplyWindowPreview();
    }

    partial void OnPanelHeightChanged(double value)
    {
        if (!_isLoading)
        {
            SetPanelHeightSliderValue(
                PanelSizePolicy.ClampActualHeight(value, GetCurrentWorkArea().WorkArea));
        }

        ApplyWindowPreview();
    }

    partial void OnPanelWidthSliderValueChanged(double value)
    {
        if (!_isLoading && !_isUpdatingPanelSlider)
        {
            PanelWidth = PanelSizePolicy.ClampPreferredWidth(value);
        }
    }

    partial void OnPanelHeightSliderValueChanged(double value)
    {
        if (!_isLoading && !_isUpdatingPanelSlider)
        {
            PanelHeight = PanelSizePolicy.ClampPreferredHeight(value);
        }
    }

    partial void OnFontScaleChanged(double value)
    {
        OnPropertyChanged(nameof(FontScaleLabel));
        ApplyWindowPreview();
    }

    partial void OnDisplayTitleChanged(string value) => ApplyWindowPreview();

    partial void OnCityChanged(string value)
    {
        if (!_isLoading && WeatherCityNormalizer.Normalize(value) != null)
        {
            AutoLocation = false;
        }
    }

    partial void OnAutoLocationChanged(bool value)
    {
        if (!_isLoading && value)
        {
            City = string.Empty;
        }
    }

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
        var isSystemLight = _systemThemeService?.IsLightTheme ?? true;
        var effective = SystemThemeSelection.GetEffectiveScheme(
            FollowSystemTheme,
            isSystemLight,
            SelectedColorScheme,
            ColorSchemeLight,
            ColorSchemeDark);
        AppThemeManager.Apply(
            SystemThemeSelection.ShouldUseLightSurface(
                FollowSystemTheme,
                isSystemLight,
                SelectedColorScheme),
            effective);
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

    private void RefreshPanelSliderValues()
    {
        var workArea = GetCurrentWorkArea();
        var actualSize = PanelSizePolicy.ClampActualSize(PanelWidth, PanelHeight, workArea.WorkArea);
        SetPanelWidthSliderValue(actualSize.Width);
        SetPanelHeightSliderValue(actualSize.Height);
    }

    private void SetPanelWidthSliderValue(double value)
    {
        if (PanelWidthSliderValue.Equals(value))
        {
            return;
        }

        _isUpdatingPanelSlider = true;
        try
        {
            PanelWidthSliderValue = value;
        }
        finally
        {
            _isUpdatingPanelSlider = false;
        }
    }

    private void SetPanelHeightSliderValue(double value)
    {
        if (PanelHeightSliderValue.Equals(value))
        {
            return;
        }

        _isUpdatingPanelSlider = true;
        try
        {
            PanelHeightSliderValue = value;
        }
        finally
        {
            _isUpdatingPanelSlider = false;
        }
    }

    private MonitorWorkArea GetCurrentWorkArea()
    {
        var owner = Application.Current?.MainWindow;
        var handle = owner == null ? 0 : new WindowInteropHelper(owner).Handle;
        return _monitorWorkAreas.GetForWindow(handle);
    }

    private PanelSizeBounds GetCurrentPanelSizeBounds() =>
        PanelSizePolicy.GetBounds(GetCurrentWorkArea().WorkArea);

    [RelayCommand]
    private async Task Save()
    {
        LastSaveSucceeded = false;
        var weatherSettingsChanged = false;
        var weatherCredentialsChanged = false;
        var locationSettingsChanged = false;
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
        List<ModuleSetting>? savedModuleSettings = null;

        try
        {
            City = WeatherCityNormalizer.Normalize(City) ?? string.Empty;
            apiKeyToValidate = WeatherApiKey.Trim();
            var rawApiHost = WeatherApiHost.Trim();
            if (string.IsNullOrEmpty(apiKeyToValidate) != string.IsNullOrEmpty(rawApiHost))
            {
                _notificationService.ShowWarningMessage(L("Settings.WeatherCredentialsRequired"));
                return;
            }

            if (!QWeatherApiClient.TryNormalizeHost(rawApiHost, out apiHostToValidate))
            {
                _notificationService.ShowWarningMessage(L("Settings.WeatherApiHostInvalid"));
                return;
            }

            locationSettingsChanged =
                _originalSettings.GetValueOrDefault("City") != City ||
                _originalSettings.GetValueOrDefault("AutoLocation") != AutoLocation.ToString();
            weatherCredentialsChanged =
                _originalSettings.GetValueOrDefault("WeatherApiKey") != apiKeyToValidate ||
                _originalSettings.GetValueOrDefault("WeatherApiHost") != apiHostToValidate;
            weatherSettingsChanged = locationSettingsChanged || weatherCredentialsChanged;
            if (weatherCredentialsChanged && !string.IsNullOrEmpty(apiKeyToValidate))
            {
                var validation = await _weatherService.ValidateApiKeyAsync(apiKeyToValidate, apiHostToValidate);
                if (!validation.IsValid)
                {
                    if (!string.IsNullOrWhiteSpace(validation.Message))
                    {
                        Logger.LogWarning(
                            validation.Message,
                            "SettingsViewModel.Save.ValidateWeatherApi");
                    }
                    _notificationService.ShowWarningMessage(
                        L("Settings.WeatherCredentialValidationFailed"));
                    return;
                }
            }

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

            DisplayTitle = MainWindowViewModel.NormalizeDisplayTitle(DisplayTitle);
            savedModuleSettings = BuildModuleSettings();
            var settingsBatch = new Dictionary<string, string?>
            {
                ["ColorScheme"] = SelectedColorScheme,
                ["Theme"] = SelectedColorScheme,
                ["FollowSystemTheme"] = FollowSystemTheme.ToString(),
                ["ColorSchemeLight"] = AppColorSchemeCatalog.NormalizeId(ColorSchemeLight),
                ["ColorSchemeDark"] = AppColorSchemeCatalog.NormalizeId(ColorSchemeDark),
                ["Startup"] = StartupEnabled.ToString(),
                ["WindowOpacity"] = WindowOpacity.ToString(CultureInfo.InvariantCulture),
                ["PanelWidth"] = PanelWidth.ToString(CultureInfo.InvariantCulture),
                ["PanelHeight"] = PanelHeight.ToString(CultureInfo.InvariantCulture),
                ["FontScale"] = FontScale.ToString(CultureInfo.InvariantCulture),
                ["DisplayTitle"] = DisplayTitle,
                ["City"] = City,
                ["AutoLocation"] = AutoLocation.ToString(),
                ["WeatherApiKey"] = apiKeyToValidate,
                ["WeatherApiHost"] = apiHostToValidate,
                ["ShortcutMaxCount"] = ShortcutMaxCount.ToString(CultureInfo.InvariantCulture),
                ["Hotkey"] = hotkeyToPersist,
                [QuickTextService.HistoryEnabledSettingKey] = ClipboardHistoryEnabled.ToString(),
                [QuickTextService.SensitiveFilterSettingKey] = ClipboardSensitiveFilterEnabled.ToString(),
                [QuickTextService.HistoryMaxCountSettingKey] = ClipboardHistoryMaxCount.ToString(CultureInfo.InvariantCulture),
                [ILocalizationService.LanguageSettingKey] = _localizationService.NormalizeLanguage(SelectedLanguage),
                [DashboardModuleCatalog.SettingsKey] = MainWindowViewModel.SerializeModuleSettings(savedModuleSettings)
            };

            await _settingsService.SaveBatchAsync(settingsBatch);
            SaveOriginalSettings();

            PendingWeatherSettingsChanged = weatherSettingsChanged;
            PendingLocationSettingsChanged = locationSettingsChanged;
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
            Logger.LogError(ex, "SettingsViewModel.Save");
            _notificationService.ShowErrorMessage(L("Settings.SaveFailed"));
            return;
        }

        try
        {
            _mainWindowViewModel.ApplyModuleSettings(savedModuleSettings!, persist: false);
            ApplyEffectiveThemePreview();
            ApplyWindowPreview();
            _mainWindowViewModel.SetShortcutLimitPreview(null);
            await _mainWindowViewModel.ReloadShortcutsAsync();
            await _mainWindowViewModel.ReloadQuickTextAsync();
            ApplyStartupSetting();
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.Save.ApplyDerivedState");
            _notificationService.ShowWarningMessage(L("Settings.ApplyAfterSaveFailed"));
        }

        RequestClose?.Invoke(this, true);
    }

    public async Task CompleteSaveFollowUpAsync(bool weatherSettingsChanged)
    {
        if (!LastSaveSucceeded)
        {
            return;
        }

        try
        {
            if (PendingLocationSettingsChanged)
            {
                await _weatherService.SetCityAsync(City);
            }

            if (weatherSettingsChanged)
            {
                await _mainWindowViewModel.RefreshWeatherAfterSettingsAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.CompleteSaveFollowUp.Weather");
            _notificationService.ShowErrorMessage(L("Settings.WeatherApplyFailed"));
        }

        try
        {
            await _quickTextService.TrimClipboardHistoryAsync(ClipboardHistoryMaxCount);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.CompleteSaveFollowUp.ClipboardTrim");
            _notificationService.ShowWarningMessage(L("Settings.ClipboardTrimFailed"));
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
        FollowSystemTheme = true;
        ColorSchemeLight = AppColorSchemeCatalog.DefaultSchemeId;
        ColorSchemeDark = "DarkGrey";
        StartupEnabled = true;
        WindowOpacity = 0.70;
        var recommendedSize = PanelSizePolicy.GetRecommendedSize(GetCurrentWorkArea().WorkArea);
        PanelWidth = PanelSizePolicy.ClampPreferredWidth(recommendedSize.Width);
        PanelHeight = PanelSizePolicy.ClampPreferredHeight(recommendedSize.Height);
        FontScale = 1.0;
        DisplayTitle = "UniDesk";
        City = "";
        AutoLocation = true;
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
            Logger.LogError(ex, "SettingsViewModel.Backup");
            _notificationService.ShowErrorMessage(L("Settings.BackupFailed"));
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

        TodoBackupImportResult result;
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

            result = await _todoBackupService.ApplyImportAsync(plan);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.Restore.ApplyImport");
            _notificationService.ShowErrorMessage(L("Settings.RestoreFailed"));
            return;
        }

        try
        {
            await _settingsService.ReloadCacheAsync();
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
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.Restore.RefreshAfterCommit");
            _notificationService.ShowWarningMessage(L("Settings.RestoreAppliedRefreshFailed"));
            return;
        }

        _notificationService.ShowSuccessMessage(_localizationService.Format(
            "Settings.RestoreSuccessFormat",
            result.SettingCount,
            result.ShortcutCount,
            result.TodoCount,
            result.QuickNoteCount,
            result.ClipboardHistoryCount,
            result.TextSnippetCount));
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
            Logger.LogError(ex, "SettingsViewModel.ResetLayout");
            _notificationService.ShowErrorMessage(L("Settings.ResetLayoutFailed"));
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

            if (_originalSettings.TryGetValue("City", out var city))
            {
                City = city;
            }

            if (_originalSettings.TryGetValue("AutoLocation", out var autoLocation))
            {
                AutoLocation = bool.Parse(autoLocation);
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
            Logger.LogError(ex, "SettingsViewModel.ExportHardwareDiagnostics");
            _notificationService.ShowErrorMessage(L("Settings.HardwareDiagnosticsFailed"));
        }
    }

    [RelayCommand]
    private async Task RepairHardwareMonitoringAsync()
    {
        if (_hardwareMonitoringMaintenanceService == null)
        {
            _notificationService.ShowErrorMessage(L("Settings.HardwareMonitoringRepairUnavailable"));
            return;
        }

        var result = await _hardwareMonitoringMaintenanceService.RepairAsync();
        switch (result.Status)
        {
            case HardwareRepairLaunchStatus.Succeeded:
                _notificationService.ShowSuccessMessage(L("Settings.HardwareMonitoringRepairSucceeded"));
                await RefreshHardwareMonitoringStatusAsync();
                break;
            case HardwareRepairLaunchStatus.Cancelled:
                _notificationService.ShowInfoMessage(L("Settings.HardwareMonitoringRepairCancelled"));
                break;
            case HardwareRepairLaunchStatus.HelperMissing:
                _notificationService.ShowErrorMessage(L("Settings.HardwareMonitoringRepairHelperMissing"));
                break;
            default:
                _notificationService.ShowErrorMessage(
                    _localizationService.Format(
                        "Settings.HardwareMonitoringRepairFailedFormat",
                        result.ExitCode?.ToString() ?? result.Error ?? "unknown"));
                await RefreshHardwareMonitoringStatusAsync();
                break;
        }
    }

    private async Task RefreshHardwareMonitoringStatusAsync()
    {
        if (_hardwareMonitoringMaintenanceService == null)
        {
            HardwareMonitoringStatusText = L("Settings.HardwareMonitoringServiceUnavailable");
            IsHardwareMonitoringRepairVisible = true;
            return;
        }

        try
        {
            var status = await _hardwareMonitoringMaintenanceService.GetStatusAsync();
            HardwareMonitoringStatusText = L(status.Availability switch
            {
                HardwareServiceAvailability.Available => "Settings.HardwareMonitoringReady",
                HardwareServiceAvailability.ServiceNotInstalled => "Settings.HardwareMonitoringNotInstalled",
                HardwareServiceAvailability.ServiceStopped => "Settings.HardwareMonitoringStopped",
                HardwareServiceAvailability.DriverUnavailable => "Settings.HardwareMonitoringDriverUnavailable",
                HardwareServiceAvailability.ProtocolMismatch => "Settings.HardwareMonitoringProtocolMismatch",
                HardwareServiceAvailability.TimedOut => "Settings.HardwareMonitoringTimedOut",
                HardwareServiceAvailability.ServiceUnavailable => "Settings.HardwareMonitoringServiceUnavailable",
                _ => "Settings.HardwareMonitoringError"
            });
            IsHardwareMonitoringRepairVisible =
                status.Availability != HardwareServiceAvailability.Available;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SettingsViewModel.HardwareMonitoring");
            HardwareMonitoringStatusText = L("Settings.HardwareMonitoringError");
            IsHardwareMonitoringRepairVisible = true;
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
        _ = RefreshHardwareMonitoringStatusAsync();
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
        DashboardModuleIds.ModelRadar => L("Module.ModelRadar"),
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
