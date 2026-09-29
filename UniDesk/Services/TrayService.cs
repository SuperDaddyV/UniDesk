using System.Windows;
using System.Windows.Controls;
using Hardcodet.Wpf.TaskbarNotification;
using UniDesk.Helpers;

namespace UniDesk.Services;

public class TrayService : ITrayService, IDisposable
{
    private TaskbarIcon? _notifyIcon;
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private bool _disposed;

    public event Action? TrayIconDoubleClick;
    public event Action? SettingsRequested;
    public event Action? ExitRequested;

    public TrayService(
        INotificationService notificationService,
        ILocalizationService localizationService)
    {
        _notificationService = notificationService;
        _localizationService = localizationService;
        _localizationService.LanguageChanged += LocalizationService_OnLanguageChanged;
    }

    public void Initialize()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_notifyIcon != null) return;

        var menu = CreateContextMenu();
        _notifyIcon = new TaskbarIcon();
        // Hardcodet 1.1.0 resolves nonempty ToolTipText to a WPF ToolTip even
        // without TrayToolTip. Suppress opening before assigning the Shell name.
        _notifyIcon.PreviewTrayToolTipOpen += (_, e) => e.Handled = true;
        _notifyIcon.ToolTipText = GetToolTipText();
        _notifyIcon.Icon = AppIconHelper.GetTrayIcon() ?? AppIconHelper.CreateDefaultTrayIcon();
        _notifyIcon.Visibility = Visibility.Visible;

        _notifyIcon.TrayMouseDoubleClick += (_, _) =>
        {
            TrayIconDoubleClick?.Invoke();
        };
        _notifyIcon.ContextMenu = menu;
    }

    private ContextMenu CreateContextMenu()
    {
        var resources = new ResourceDictionary
        {
            Source = new Uri("pack://application:,,,/UniDesk;component/Resources/TrayMenu.xaml")
        };

        var menu = new ContextMenu
        {
            Style = resources["TrayContextMenuStyle"] as Style
        };

        menu.Items.Add(CreateMenuItem(resources, L("Common.ShowHide"), () => TrayIconDoubleClick?.Invoke()));
        menu.Items.Add(CreateMenuItem(resources, L("Common.Settings"), () => SettingsRequested?.Invoke()));
        menu.Items.Add(new Separator { Style = resources["TraySeparatorStyle"] as Style });
        menu.Items.Add(CreateMenuItem(resources, L("Common.Exit"), () => ExitRequested?.Invoke(), "Danger"));

        return menu;
    }

    private static MenuItem CreateMenuItem(
        ResourceDictionary resources,
        string header,
        Action onClick,
        string? tag = null)
    {
        var item = new MenuItem
        {
            Header = header,
            Style = resources["TrayMenuItemStyle"] as Style,
            Tag = tag
        };
        item.Click += (_, _) => onClick();
        return item;
    }

    public void ShowBalloonTip(string title, string message)
    {
        _notifyIcon?.ShowBalloonTip(title, message, BalloonIcon.Info);
    }

    private void LocalizationService_OnLanguageChanged(object? sender, EventArgs e)
    {
        if (_notifyIcon == null)
        {
            return;
        }

        _notifyIcon.ToolTipText = GetToolTipText();
        _notifyIcon.ContextMenu = CreateContextMenu();
    }

    private string GetToolTipText() => $"UniDesk - {L("Common.AppDescription")}";

    private string L(string key) => _localizationService.GetString(key);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _localizationService.LanguageChanged -= LocalizationService_OnLanguageChanged;
        if (_notifyIcon != null)
        {
            _notifyIcon.Visibility = Visibility.Collapsed;
            _notifyIcon.Dispose();
            _notifyIcon = null;
        }
    }
}
