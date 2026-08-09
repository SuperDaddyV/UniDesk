using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using UniDesk.Helpers;
using UniDesk.ViewModels;

namespace UniDesk;

public partial class SettingsWindow : Window
{
    private readonly SettingsViewModel _viewModel;
    private readonly EventHandler<bool> _requestCloseHandler;
    private readonly IMonitorWorkAreaProvider _monitorWorkAreas;
    private bool _isClosing;
    private bool _initialBoundsApplied;

    private bool _isWindowDragging;
    private Point _windowDragScreenStart;

    private const double DragChromeHeight = 56;
    private const double DefaultMinimumHeight = 420;
    private const double WorkAreaMargin = 24;

    public SettingsWindow(
        SettingsViewModel viewModel,
        double ownerWidth,
        double ownerHeight,
        IMonitorWorkAreaProvider? monitorWorkAreas = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _requestCloseHandler = OnRequestClose;
        _monitorWorkAreas = monitorWorkAreas ?? Win32MonitorWorkAreaProvider.Instance;

        AppIconHelper.ApplyWindowIcon(this);
        DesktopWidgetWindowHelper.Configure(this);

        _ = ownerWidth;
        _ = ownerHeight;
        SourceInitialized += SettingsWindow_OnSourceInitialized;

        _viewModel.RequestClose += _requestCloseHandler;
    }

    private void SettingsWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_initialBoundsApplied)
        {
            return;
        }

        _initialBoundsApplied = true;
        var owner = Owner ?? Application.Current.MainWindow;
        var targetHandle = owner != null
            ? new WindowInteropHelper(owner).Handle
            : new WindowInteropHelper(this).Handle;
        var workArea = _monitorWorkAreas.GetForWindow(targetHandle).WorkArea;
        ApplySizeFromWorkArea(workArea);
        SetDefaultPosition(owner, workArea);
    }

    private void OnRequestClose(object? sender, bool saved)
    {
        if (_isClosing)
        {
            return;
        }

        if (!saved)
        {
            _viewModel.RevertChanges();
        }

        _isClosing = true;
        _viewModel.RequestClose -= _requestCloseHandler;
        Close();
    }

    private void ApplySizeFromWorkArea(LogicalRect workArea)
    {
        var usableHeight = Math.Max(320, workArea.Height - WorkAreaMargin);
        var usableWidth = Math.Max(320, workArea.Width - 32);
        MinWidth = Math.Min(680, usableWidth);
        Width = Math.Min(720, usableWidth);
        MinHeight = Math.Min(DefaultMinimumHeight, usableHeight);
        Height = Math.Min(620, usableHeight);
    }

    private void SetDefaultPosition(Window? owner, LogicalRect workArea)
    {
        if (owner != null)
        {
            Left = owner.Left + (owner.Width - Width) / 2;
            Top = owner.Top + (owner.Height - Height) / 2;
        }
        else
        {
            Left = workArea.Left + (workArea.Width - Width) / 2;
            Top = workArea.Top + (workArea.Height - Height) / 2;
        }

        var clamped = MonitorWorkAreaGeometry.Clamp(
            new LogicalRect(Left, Top, Width, Height),
            workArea);
        Left = clamped.Left;
        Top = clamped.Top;
        Width = clamped.Width;
        Height = clamped.Height;
    }

    private void Window_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!CanStartWindowDrag(e.OriginalSource as DependencyObject, e.GetPosition(this)))
        {
            return;
        }

        _isWindowDragging = true;
        _windowDragScreenStart = PointToScreen(e.GetPosition(this));
        CaptureMouse();
        e.Handled = true;
    }

    private void Window_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        UpdateDragChromeCursor(e);

        if (!_isWindowDragging || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var current = PointToScreen(e.GetPosition(this));
        var monitor = _monitorWorkAreas.GetForWindow(new WindowInteropHelper(this).Handle);
        Left += (current.X - _windowDragScreenStart.X) * 96 / monitor.DpiX;
        Top += (current.Y - _windowDragScreenStart.Y) * 96 / monitor.DpiY;
        _windowDragScreenStart = current;
    }

    private void UpdateDragChromeCursor(MouseEventArgs e)
    {
        if (_isWindowDragging)
        {
            Cursor = Cursors.SizeAll;
            return;
        }

        var position = e.GetPosition(this);
        Cursor = CanStartWindowDrag(e.OriginalSource as DependencyObject, position)
            ? Cursors.SizeAll
            : null;
    }

    private void Window_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndWindowDrag();
    }

    private bool CanStartWindowDrag(DependencyObject? source, Point positionInWindow)
    {
        if (positionInWindow.Y > DragChromeHeight)
        {
            return false;
        }

        if (IsInsideElement(source, CloseButton))
        {
            return false;
        }

        if (IsInsideInteractiveControl(source))
        {
            return false;
        }

        return true;
    }

    private static bool IsInsideInteractiveControl(DependencyObject? source)
    {
        while (source != null)
        {
            if (source is Button or TextBoxBase or Slider or CheckBox or ComboBox or ComboBoxItem or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void EndWindowDrag()
    {
        if (!_isWindowDragging)
        {
            return;
        }

        _isWindowDragging = false;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        Cursor = null;
    }

    private void Window_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (!_isWindowDragging)
        {
            Cursor = null;
        }
    }

    private static bool IsInsideElement(DependencyObject? source, DependencyObject target)
    {
        while (source != null)
        {
            if (source == target)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void CloseButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _viewModel.CancelCommand.Execute(null);
    }

    private void SettingsWindow_OnClosed(object? sender, EventArgs e)
    {
        _viewModel.RequestClose -= _requestCloseHandler;
        _viewModel.Dispose();
        EndWindowDrag();
    }

    private void SettingsWindow_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            _viewModel.CancelCommand.Execute(null);
        }
    }

}
