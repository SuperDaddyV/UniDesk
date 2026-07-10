using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using UniDesk.Helpers;
using UniDesk.Models;
using UniDesk.Services;
using UniDesk.ViewModels;

namespace UniDesk;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel _viewModel;
    private readonly ISettingsService _settingsService;
    private readonly IClipboardMonitorService _clipboardMonitorService;
    private Point _scrollPanStart;
    private double _scrollPanOffsetStart;
    private bool _scrollPanPending;
    private bool _scrollPanActive;
    private bool _suppressPositionSave;
    private const double DefaultExpandedPanelHeight = 702;
    private const double CollapsedPanelHeight = 196;
    private const double WindowCornerRadius = 16;

    public bool AllowShutdown { get; set; }

    public MainWindow(
        MainWindowViewModel viewModel,
        IWindowService windowService,
        ISettingsService settingsService,
        IClipboardMonitorService clipboardMonitorService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsService = settingsService;
        _clipboardMonitorService = clipboardMonitorService;
        _ = windowService;

        AppIconHelper.ApplyWindowIcon(this);
        DesktopWidgetWindowHelper.Configure(this);

        ApplyInitialWindowBounds();
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsPanelCollapsed))
        {
            ApplyPanelCollapseState();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.PanelHeight) && !_viewModel.IsPanelCollapsed)
        {
            ApplyPanelCollapseState();
        }
        else if (e.PropertyName == nameof(MainWindowViewModel.ModuleLayoutVersion))
        {
            ApplyModuleLayout();
        }
    }

    private void ApplyPanelCollapseState()
    {
        var targetHeight = _viewModel.IsPanelCollapsed
            ? CollapsedPanelHeight
            : Math.Clamp(_viewModel.PanelHeight, IWindowService.MinPanelHeight, IWindowService.MaxPanelHeight);
        MinHeight = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : IWindowService.MinPanelHeight;
        MaxHeight = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : IWindowService.MaxPanelHeight;
        Height = targetHeight;
        ClampToVisibleWorkArea();
        if (MainModulesGrid.RowDefinitions.Count == 0)
        {
            return;
        }

        if (_viewModel.IsPanelCollapsed)
        {
            MainModulesScrollViewer.Margin = new Thickness(12, 0, 12, 6);
            foreach (var row in MainModulesGrid.RowDefinitions)
            {
                row.Height = new GridLength(0);
            }

            MainModulesGrid.RowDefinitions[0].Height = GridLength.Auto;
        }
        else
        {
            MainModulesScrollViewer.Margin = new Thickness(12, 0, 12, 10);
        }

        ApplyModuleLayout();

        if (!_suppressPositionSave)
        {
            SaveWindowPosition();
        }
    }

    private void ApplyInitialWindowBounds()
    {
        _suppressPositionSave = true;
        try
        {
            Height = _viewModel.IsPanelCollapsed
                ? CollapsedPanelHeight
                : Math.Clamp(_viewModel.PanelHeight <= 0 ? DefaultExpandedPanelHeight : _viewModel.PanelHeight,
                    IWindowService.MinPanelHeight,
                    IWindowService.MaxPanelHeight);
            Width = _viewModel.PanelWidth;
            ApplyPanelCollapseState();

            var savedPosition = _viewModel.GetSavedWindowPosition();
            if (savedPosition is { } position)
            {
                Left = position.Left;
                Top = position.Top;
            }
            else
            {
                var workArea = SystemParameters.WorkArea;
                Left = workArea.Right - Width - 20;
                Top = workArea.Top + (workArea.Height - Height) / 2;
            }

            ClampToVisibleWorkArea();
        }
        finally
        {
            _suppressPositionSave = false;
        }
    }

    private void MainWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWindowContainerClip();
        _viewModel.ApplyWindowSettings();
        _clipboardMonitorService.Start(this);
        ApplyModuleLayout();
    }

    private void ApplyModuleLayout()
    {
        if (MainModulesGrid.RowDefinitions.Count == 0)
        {
            return;
        }

        var modules = _viewModel.GetModuleSettingsSnapshot()
            .Where(module => module.IsEnabled)
            .Select(module => new
            {
                module.ModuleId,
                Element = GetModuleElement(module.ModuleId)
            })
            .Where(module => module.Element != null)
            .ToList();

        var visibleModules = _viewModel.IsPanelCollapsed
            ? modules.Take(1).ToList()
            : modules;

        foreach (var element in GetAllModuleElements())
        {
            element.Visibility = Visibility.Collapsed;
        }

        for (var row = 0; row < MainModulesGrid.RowDefinitions.Count; row++)
        {
            MainModulesGrid.RowDefinitions[row].Height = new GridLength(0);
        }

        for (var row = 0; row < visibleModules.Count; row++)
        {
            var module = visibleModules[row];
            var element = module.Element!;
            Grid.SetRow(element, row);
            element.Visibility = Visibility.Visible;
            element.Margin = row == visibleModules.Count - 1
                ? new Thickness(0)
                : new Thickness(0, 0, 0, 6);

            MainModulesGrid.RowDefinitions[row].Height = GridLength.Auto;
        }

        EmptyModulesMessage.Visibility = modules.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (modules.Count == 0)
        {
            MainModulesGrid.RowDefinitions[0].Height = GridLength.Auto;
        }
    }

    private FrameworkElement? GetModuleElement(string moduleId) => moduleId switch
    {
        DashboardModuleIds.TimeWeather => TimeWeatherModule,
        DashboardModuleIds.HardwareMonitor => HardwareMonitorModule,
        DashboardModuleIds.Shortcuts => ShortcutsModule,
        DashboardModuleIds.Todos => TodosModule,
        DashboardModuleIds.QuickNotes => QuickNotesModule,
        DashboardModuleIds.QuickText => QuickTextModule,
        _ => null
    };

    private IEnumerable<FrameworkElement> GetAllModuleElements()
    {
        yield return TimeWeatherModule;
        yield return HardwareMonitorModule;
        yield return ShortcutsModule;
        yield return TodosModule;
        yield return QuickNotesModule;
        yield return QuickTextModule;
    }

    private void WindowContainer_OnSizeChanged(object sender, SizeChangedEventArgs e) =>
        UpdateWindowContainerClip();

    private void UpdateWindowContainerClip()
    {
        if (WindowContainer.ActualWidth <= 0 || WindowContainer.ActualHeight <= 0)
        {
            return;
        }

        WindowContainer.Clip = new RectangleGeometry(
            new Rect(0, 0, WindowContainer.ActualWidth, WindowContainer.ActualHeight),
            WindowCornerRadius,
            WindowCornerRadius);
    }

    private void TitleBar_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsWindowLocked ||
            e.LeftButton != MouseButtonState.Pressed ||
            IsInside<Button>(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
        SaveWindowPosition();
        e.Handled = true;
    }

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        _viewModel.OpenSettingsCommand.Execute(null);
    }

    private void WindowDragSurface_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel.IsWindowLocked ||
            e.LeftButton != MouseButtonState.Pressed ||
            e.GetPosition(this).Y > 34 ||
            IsInside<Button>(e.OriginalSource as DependencyObject))
        {
            return;
        }

        DragMove();
        SaveWindowPosition();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => Hide();

    private void ClockHotspot_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left)
        {
            return;
        }

        _viewModel.ToggleCalendarPopupCommand.Execute(null);
        e.Handled = true;
    }

    private void PreviousCalendarMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.PreviousCalendarMonthCommand.Execute(null);
        e.Handled = true;
    }

    private void NextCalendarMonthButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.NextCalendarMonthCommand.Execute(null);
        e.Handled = true;
    }

    private void CloseCalendarButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.IsCalendarPopupOpen = false;
        e.Handled = true;
    }

    private void CalendarDayButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectCalendarDateCommand.Execute((sender as FrameworkElement)?.DataContext as CalendarDayItem);
        e.Handled = true;
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer viewer || ShouldIgnoreScrollPan(e.OriginalSource as DependencyObject))
        {
            return;
        }

        _scrollPanPending = true;
        _scrollPanActive = false;
        _scrollPanStart = e.GetPosition(viewer);
        _scrollPanOffsetStart = viewer.VerticalOffset;
    }

    private void ScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ScrollViewer viewer || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_scrollPanPending)
        {
            var current = e.GetPosition(viewer);
            var deltaX = current.X - _scrollPanStart.X;
            var deltaY = current.Y - _scrollPanStart.Y;

            if (Math.Abs(deltaX) < 4 && Math.Abs(deltaY) < 4)
            {
                return;
            }

            if (Math.Abs(deltaY) <= Math.Abs(deltaX))
            {
                _scrollPanPending = false;
                return;
            }

            _scrollPanPending = false;
            _scrollPanActive = true;
            viewer.CaptureMouse();
            viewer.Cursor = Cursors.SizeAll;
        }

        if (!_scrollPanActive)
        {
            return;
        }

        var position = e.GetPosition(viewer);
        var offset = _scrollPanOffsetStart - (position.Y - _scrollPanStart.Y);
        viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(viewer.ScrollableHeight, offset)));
        e.Handled = true;
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        EndScrollPan(sender as ScrollViewer);
    }

    private void ScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_scrollPanActive && e.LeftButton != MouseButtonState.Pressed)
        {
            EndScrollPan(sender as ScrollViewer);
        }
    }

    private void EndScrollPan(ScrollViewer? viewer)
    {
        if (!_scrollPanPending && !_scrollPanActive)
        {
            return;
        }

        _scrollPanPending = false;
        _scrollPanActive = false;
        viewer?.ReleaseMouseCapture();
        if (viewer != null)
        {
            viewer.Cursor = null;
        }
    }

    private bool ShouldIgnoreScrollPan(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement { Tag: "TodoCheck" } or Button)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }


    private static bool IsInside<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        SaveWindowPosition();
        _ = _settingsService.FlushPendingSavesAsync();

        if (AllowShutdown)
        {
            _clipboardMonitorService.Stop();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void SaveWindowPosition() => _viewModel.SaveWindowPosition(Left, Top);

    private void ClampToVisibleWorkArea()
    {
        var workLeft = SystemParameters.VirtualScreenLeft;
        var workTop = SystemParameters.VirtualScreenTop;
        var workRight = workLeft + SystemParameters.VirtualScreenWidth;
        var workBottom = workTop + SystemParameters.VirtualScreenHeight;

        var width = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;
        var height = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        Left = Math.Clamp(Left, workLeft, Math.Max(workLeft, workRight - width));
        Top = Math.Clamp(Top, workTop, Math.Max(workTop, workBottom - height));
    }
}
