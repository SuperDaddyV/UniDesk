using System.Windows;
using System.Windows.Interop;
using UniDesk.Helpers;

namespace UniDesk.Services;

public class WindowService : IWindowService
{
    private MainWindow? _mainWindow;
    private readonly IMonitorWorkAreaProvider _monitorWorkAreas;

    public WindowService(IMonitorWorkAreaProvider? monitorWorkAreas = null)
    {
        _monitorWorkAreas = monitorWorkAreas ?? Win32MonitorWorkAreaProvider.Instance;
    }

    public void Initialize(MainWindow mainWindow)
    {
        _mainWindow = mainWindow;
    }

    public void SetTopMost(bool topMost)
    {
        if (_mainWindow == null) return;
        _mainWindow.Topmost = topMost;
    }

    public void ShowWindow()
    {
        ActivateWindow();
    }

    public void ActivateWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.WindowState == WindowState.Minimized)
        {
            _mainWindow.WindowState = WindowState.Normal;
        }

        _mainWindow.Show();
        _mainWindow.Activate();
        _mainWindow.Focus();
    }

    public void HideWindow()
    {
        if (_mainWindow == null) return;
        _mainWindow.Hide();
    }

    public void ToggleWindow()
    {
        if (_mainWindow == null) return;

        if (_mainWindow.IsVisible)
        {
            HideWindow();
        }
        else
        {
            ShowWindow();
        }
    }

    public void SetWidth(double width)
    {
        if (_mainWindow == null) return;
        _mainWindow.Width = PanelSizePolicy.ClampActualWidth(
            width,
            GetCurrentMonitor().WorkArea);
    }

    public void SetHeight(double height)
    {
        if (_mainWindow == null) return;
        _mainWindow.Height = PanelSizePolicy.ClampActualHeight(
            height,
            GetCurrentMonitor().WorkArea);
    }

    public void AnimateWidth(double width, Action? onCompleted = null)
    {
        if (_mainWindow == null)
        {
            onCompleted?.Invoke();
            return;
        }

        UiAnimationHelper.AnimateDouble(
            _mainWindow,
            Window.WidthProperty,
            PanelSizePolicy.ClampActualWidth(width, GetCurrentMonitor().WorkArea),
            onCompleted: onCompleted);
    }

    public void SetOpacity(double opacity)
    {
        if (_mainWindow == null) return;

        if (opacity < 0.6) opacity = 0.6;
        if (opacity > 1.0) opacity = 1.0;

        if (_mainWindow.FindName("WindowContainer") is FrameworkElement windowContainer)
        {
            windowContainer.Opacity = opacity;
        }
    }

    public double GetCurrentWidth() => _mainWindow?.ActualWidth ?? IWindowService.MinPanelWidth;

    public void SnapToEdge()
    {
        if (_mainWindow == null) return;

        var workArea = GetCurrentMonitor().WorkArea;
        const double snapThreshold = 20;

        if (_mainWindow.Left < workArea.Left + snapThreshold)
        {
            _mainWindow.Left = workArea.Left;
        }
        else if (_mainWindow.Left + _mainWindow.Width > workArea.Right - snapThreshold)
        {
            _mainWindow.Left = workArea.Right - _mainWindow.Width;
        }

        if (_mainWindow.Top < workArea.Top + snapThreshold)
        {
            _mainWindow.Top = workArea.Top;
        }
        else if (_mainWindow.Top + _mainWindow.Height > workArea.Bottom - snapThreshold)
        {
            _mainWindow.Top = workArea.Bottom - _mainWindow.Height;
        }
    }

    private MonitorWorkArea GetCurrentMonitor() =>
        _monitorWorkAreas.GetForWindow(
            _mainWindow == null ? 0 : new WindowInteropHelper(_mainWindow).Handle);
}
