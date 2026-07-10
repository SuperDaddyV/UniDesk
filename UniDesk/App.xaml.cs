using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using UniDesk.Services;
using UniDesk.ViewModels;
using UniDesk.Helpers;

namespace UniDesk;

public partial class App : Application
{
    public ServiceProvider Services { get; private set; } = null!;
    private readonly FatalExceptionCoordinator _fatalExceptionCoordinator = new();
    private SingleInstanceHelper? _singleInstanceHelper;
    private MainWindow? _mainWindow;
    private TrayService? _trayService;
    private HotkeyService? _hotkeyService;
    private int _activationPending;

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

        var localization = Services?.GetService<ILocalizationService>();
        var message = localization?.Format("App.FatalErrorFormat", DirectoryHelper.LogsDirectory)
            ?? $"UniDesk 遇到无法恢复的错误，即将退出。\n日志：{DirectoryHelper.LogsDirectory}";
        MessageBox.Show(message, "UniDesk", MessageBoxButton.OK, MessageBoxImage.Error);
        Shutdown(-1);
    }

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        DirectoryHelper.EnsureDirectoriesExist();
        SetupExceptionHandling();

        try
        {
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
            ConfigureServices(services);
            Services = services.BuildServiceProvider();

            var settingsService = Services.GetRequiredService<ISettingsService>();
            await settingsService.InitializeAsync();
            Services.GetRequiredService<ILocalizationService>().Initialize(settingsService);

            SyncStartupWithSettings();

            _mainWindow = Services.GetRequiredService<MainWindow>();
            var windowService = Services.GetRequiredService<IWindowService>() as WindowService;
            windowService?.SetMainWindow(_mainWindow);

            _hotkeyService = Services.GetRequiredService<IHotkeyService>() as HotkeyService;

            _mainWindow.Loaded += (_, _) =>
            {
                try
                {
                    _hotkeyService?.Initialize(_mainWindow);
                    RegisterGlobalHotkey();
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
                windowService?.ActivateWindow();
            }

            _trayService = Services.GetRequiredService<ITrayService>() as TrayService;
            _trayService?.Initialize();
            if (_trayService != null)
            {
                _trayService.TrayIconDoubleClick += OnTrayToggleWindow;
                _trayService.SettingsRequested += OnTrayOpenSettings;
                _trayService.ExitRequested += OnExitRequested;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "App.OnStartup");
            MessageBox.Show(
                $"启动失败：{ex.Message}\n请查看：{DirectoryHelper.LogsDirectory}",
                "UniDesk 启动失败",
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

    private void RegisterGlobalHotkey()
    {
        var settingsService = Services.GetRequiredService<ISettingsService>();
        var windowService = Services.GetRequiredService<IWindowService>();
        var hotkey = settingsService.GetValue("Hotkey", "Ctrl+Alt+Space");

        _hotkeyService?.UnregisterAll();
        if (!string.IsNullOrWhiteSpace(hotkey))
        {
            _hotkeyService?.RegisterHotkey(hotkey, () =>
            {
                Current.Dispatcher.BeginInvoke(
                    DispatcherPriority.Send,
                    () => windowService.ToggleWindow());
            });
        }
    }

    private void ApplyInitialSettings()
    {
        if (_mainWindow == null) return;

        var settingsService = Services.GetRequiredService<ISettingsService>();
        var scheme = AppColorSchemeCatalog.NormalizeId(
            settingsService.GetValue("ColorScheme", settingsService.GetValue("Theme", AppColorSchemeCatalog.DefaultSchemeId)));
        AppColorSchemeCatalog.Apply(scheme);

        Services.GetRequiredService<MainWindowViewModel>().ApplyWindowSettings();
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
        _trayService?.Dispose();
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

    private void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<MainWindow>();
        services.AddSingleton<MainWindowViewModel>();

        services.AddSingleton<ISettingsService, SettingsService>();
        services.AddSingleton<ILocalizationService, LocalizationService>();
        services.AddSingleton<IDatabaseService, DatabaseService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IUpdateService, GitHubUpdateService>();
        services.AddSingleton<IWindowService, WindowService>();
        services.AddSingleton<ITrayService, TrayService>();
        services.AddSingleton<IHotkeyService, HotkeyService>();
        services.AddSingleton<IStartupService, StartupService>();
        services.AddSingleton<ILayoutService, LayoutService>();
        services.AddSingleton<QWeatherApiClient>();
        services.AddSingleton<ILocationProvider, LocationProvider>();
        services.AddSingleton<IWeatherService, WeatherService>();
        services.AddSingleton<IClockService, ClockService>();
        services.AddSingleton<INoteService, NoteService>();
        services.AddSingleton<IQuickNoteService, QuickNoteService>();
        services.AddSingleton<IQuickTextService, QuickTextService>();
        services.AddSingleton<IClipboardMonitorService, ClipboardMonitorService>();
        services.AddSingleton<ITodoService, TodoService>();
        services.AddSingleton<ITodoDeletionHandler, TodoDeletionHandler>();
        services.AddSingleton<ITodoBackupService, TodoBackupService>();
        services.AddSingleton<IShortcutService, ShortcutService>();
        services.AddSingleton<ISystemMetricsMonitor>(_ =>
            new SystemMetricsMonitor(
                new SystemMetricsService(),
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(2),
                ownsReader: true));
    }

    protected override void OnExit(ExitEventArgs e)
    {
        var services = Services;
        if (services?.GetService(typeof(MainWindowViewModel)) is IDisposable disposableVm)
        {
            disposableVm.Dispose();
        }

        if (services?.GetService(typeof(ISettingsService)) is SettingsService settingsService)
        {
            settingsService.FlushPendingSaves();
        }

        _hotkeyService?.Dispose();
        _trayService?.Dispose();
        if (_singleInstanceHelper != null)
        {
            _singleInstanceHelper.ActivationRequested -= OnActivationRequested;
        }
        _singleInstanceHelper?.Dispose();
        services?.Dispose();
        base.OnExit(e);
    }
}
