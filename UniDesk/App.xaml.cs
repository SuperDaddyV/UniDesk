using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using UniDesk.Services;
using UniDesk.ViewModels;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk;

public partial class App : Application
{
    public ServiceProvider Services { get; private set; } = null!;
    private readonly FatalExceptionCoordinator _fatalExceptionCoordinator = new();
    private SingleInstanceHelper? _singleInstanceHelper;
    private MainWindow? _mainWindow;
    private ITrayService? _trayService;
    private IHotkeyService? _hotkeyService;
    private ISystemThemeService? _systemThemeService;
    private int _activationPending;
    private string _currentErrorLanguage = ILocalizationService.DefaultLanguage;

    public App()
    {
#if DEBUG
        RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
#endif

        DispatcherUnhandledException += OnDispatcherUnhandledException;
    }

    private void OnDispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs args)
    {
        Logger.LogError(args.Exception, "DispatcherUnhandledException");
        args.Handled = true;
        if (!_fatalExceptionCoordinator.TryBeginShutdown()) return;

        var error = StartupErrorMessageProvider.GetFatalFailure(
            _currentErrorLanguage,
            DirectoryHelper.LogsDirectory);
        MessageBox.Show(error.Message, error.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(-1);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        _currentErrorLanguage = InitialLanguageResolver.Resolve(
            e.Args,
            CultureInfo.CurrentUICulture);

        SetupExceptionHandling();

        try
        {
            DirectoryHelper.EnsureDirectoriesExist();
            _singleInstanceHelper = new SingleInstanceHelper();
            if (!_singleInstanceHelper.TryAcquire())
            {
                if (!await _singleInstanceHelper.SignalExistingInstanceAsync())
                {
                    Logger.LogWarning("无法连接首实例的激活管道。", "App.OnStartup");
                }

                Shutdown();
                return;
            }

            LogRetentionService.DeleteExpiredLogs(
                DirectoryHelper.LogsDirectory,
                DateOnly.FromDateTime(DateTime.Today));
            _singleInstanceHelper.ActivationRequested += OnActivationRequested;
            _singleInstanceHelper.StartListening();

            var services = new ServiceCollection();
            ConfigureServices(services, _currentErrorLanguage);
            Services = services.BuildServiceProvider();

            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.InitializeAsync();
            await Services.GetRequiredService<IPrivacyMigrationService>().MigrateAsync();
            var localizationService = Services.GetRequiredService<ILocalizationService>();
            localizationService.Initialize(settingsService);
            _currentErrorLanguage = localizationService.CurrentLanguage;
            _systemThemeService = Services.GetRequiredService<ISystemThemeService>();
            _systemThemeService.Initialize();
            _systemThemeService.ThemeChanged += OnSystemThemeChanged;
            ApplyThemeFromSettings();

            SyncStartupWithSettings();

            _mainWindow = Services.GetRequiredService<MainWindow>();
            var windowService = Services.GetRequiredService<IWindowService>();
            windowService.Initialize(_mainWindow);

            _hotkeyService = Services.GetRequiredService<IHotkeyService>();

            _mainWindow.Loaded += (_, _) =>
            {
                try
                {
                    _hotkeyService?.Initialize(_mainWindow);
                    var hotkey = settingsService.GetValue("Hotkey", "Ctrl+Alt+Space");
                    var hotkeyResult = Services
                        .GetRequiredService<MainWindowViewModel>()
                        .ApplyGlobalHotkey(hotkey);
                    if (!hotkeyResult.Success)
                    {
                        var localization = Services.GetRequiredService<ILocalizationService>();
                        var message = hotkeyResult.Failure == HotkeyRegistrationFailure.InvalidGesture
                            ? localization.Format("Hotkey.InvalidFormat", hotkey)
                            : localization.Format(
                                "Hotkey.RegisterFailedFormat",
                                hotkeyResult.NormalizedHotkey,
                                hotkeyResult.ErrorCode);
                        Services.GetRequiredService<INotificationService>().ShowWarningMessage(message);
                    }
                    ApplyInitialSettings();
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "MainWindow.Loaded");
                }
            };

            _mainWindow.Show();
            if (Interlocked.Exchange(ref _activationPending, 0) == 1)
            {
                windowService.ActivateWindow();
            }

            _trayService = Services.GetRequiredService<ITrayService>();
            _trayService.Initialize();
            _trayService.TrayIconDoubleClick += OnTrayToggleWindow;
            _trayService.SettingsRequested += OnTrayOpenSettings;
            _trayService.ExitRequested += OnExitRequested;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "App.OnStartup");
            var error = StartupErrorMessageProvider.GetStartupFailure(
                _currentErrorLanguage,
                DirectoryHelper.LogsDirectory);
            MessageBox.Show(
                error.Message,
                error.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    private void OnActivationRequested()
    {
        _ = Dispatcher.BeginInvoke(
            DispatcherPriority.Send,
            new Action(() =>
            {
                if (_mainWindow == null || Services == null)
                {
                    Interlocked.Exchange(ref _activationPending, 1);
                    return;
                }

                Services.GetService<IWindowService>()?.ActivateWindow();
            }));
    }

    private void SyncStartupWithSettings()
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        var startupService = Services.GetRequiredService<IStartupService>();
        var enabled = string.Equals(settingsService.GetValue("Startup", "false"), "true", StringComparison.OrdinalIgnoreCase);
        if (!enabled && startupService.IsEnabled)
        {
            settingsService.SetValue("Startup", "true");
            return;
        }

        startupService.SyncWithSetting(enabled);
    }

    private void ApplyInitialSettings()
    {
        if (_mainWindow == null) return;

        ApplyThemeFromSettings();

        Services.GetRequiredService<MainWindowViewModel>().ApplyWindowSettings();
    }

    private void OnSystemThemeChanged(object? sender, bool isLightTheme) =>
        ApplyThemeFromSettings();

    private void ApplyThemeFromSettings()
    {
        if (Services == null)
        {
            return;
        }

        var settings = Services.GetRequiredService<ISettingsService>();
        var manual = settings.GetValue(
            "ColorScheme",
            settings.GetValue("Theme", AppColorSchemeCatalog.DefaultSchemeId));
        var followSystem = settings.GetSetting("FollowSystemTheme", true);
        var isSystemLight = _systemThemeService?.IsLightTheme ?? true;
        var effective = SystemThemeSelection.GetEffectiveScheme(
            followSystem,
            isSystemLight,
            manual,
            settings.GetValue("ColorSchemeLight", AppColorSchemeCatalog.DefaultSchemeId),
            settings.GetValue("ColorSchemeDark", "DarkGrey"));
        AppThemeManager.Apply(
            SystemThemeSelection.ShouldUseLightSurface(followSystem, isSystemLight, manual),
            effective);
    }

    private void OnTrayToggleWindow()
    {
        Services.GetRequiredService<IWindowService>().ToggleWindow();
    }

    private void OnTrayOpenSettings()
    {
        var windowService = Services.GetRequiredService<IWindowService>();
        if (_mainWindow != null && !_mainWindow.IsVisible)
        {
            windowService.ShowWindow();
        }

        Services.GetRequiredService<MainWindowViewModel>().OpenSettingsCommand.Execute(null);
    }

    private void OnExitRequested()
    {
        _hotkeyService?.UnregisterAll();
        _singleInstanceHelper?.Release();

        if (_mainWindow != null)
        {
            _mainWindow.AllowShutdown = true;
            _mainWindow.Close();
        }

        Shutdown();
    }

    private void SetupExceptionHandling()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            if (args.ExceptionObject is Exception ex)
            {
                Logger.LogError(ex, "AppDomain.UnhandledException");
            }
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.LogError(args.Exception, "UnobservedTaskException");
            args.SetObserved();
        };
    }

    private void ConfigureServices(
        IServiceCollection services,
        string initialLanguage)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<IUserDataProtector, DpapiUserDataProtector>();
        services.AddSingleton<IPrivacyMigrationService, PrivacyMigrationService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IDatabaseService>(_ => new DatabaseService(initialLanguage: initialLanguage));
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IHotkeyPlatform, Win32HotkeyPlatform>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<ISystemThemeService, SystemThemeService>();
        services.AddSingleton<ISearchService, SearchService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<QWeatherApiClient>();
        services.AddSingleton<ILocationProvider, LocationProvider>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IModelRadarService, ModelRadarService>();
        services.AddSingleton<IClockService, ClockService>();
        services.AddSingleton<INoteService, NoteService>();
        services.AddSingleton<IQuickNoteService, QuickNoteService>();
        services.AddSingleton<IQuickTextService, QuickTextService>();
        services.AddSingleton<IClipboardMonitorService, ClipboardMonitorService>();
        services.AddSingleton<ITodoService, TodoService>();
        services.AddSingleton<ITodoDeletionHandler, TodoDeletionHandler>();
        services.AddSingleton<ITodoBackupService, TodoBackupService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<SystemMetricsService>();
        services.AddSingleton<IHardwareMetricsDiagnosticsSource>(provider =>
            provider.GetRequiredService<SystemMetricsService>());
        services.AddSingleton<ISensorDiagnosticsService, SensorDiagnosticReporter>();
        services.AddSingleton<IHardwareMonitoringMaintenanceService, HardwareMonitoringMaintenanceService>();
        services.AddSingleton<ISystemMetricsMonitor>(provider =>
            new SystemMetricsMonitor(
                provider.GetRequiredService<SystemMetricsService>(),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                ownsReader: false));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var services = Services;
        if (services?.GetService(typeof(ISettingsService)) is SettingsService settingsService)
        {
            settingsService.FlushPendingSaves();
        }

        if (_systemThemeService != null)
        {
            _systemThemeService.ThemeChanged -= OnSystemThemeChanged;
        }
        if (_singleInstanceHelper != null)
        {
            _singleInstanceHelper.ActivationRequested -= OnActivationRequested;
        }
        _singleInstanceHelper?.Dispose();
        services?.Dispose();
        base.OnExit(e);
    }
}
