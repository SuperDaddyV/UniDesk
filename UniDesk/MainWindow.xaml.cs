using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
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
    private readonly IMonitorWorkAreaProvider _monitorWorkAreas;
    private bool _suppressPositionSave;
    private bool _initialBoundsApplied;
    private bool _isApplyingMonitorBounds;
    private bool _isDragging;
    private MonitorWorkArea? _currentMonitor;
    private const double CollapsedPanelHeight = 178;
    private const double MinimumCompactHeight = CollapsedPanelHeight;
    private const double WorkAreaMargin = 16;
    private const double WindowCornerRadius = 16;

    public bool AllowShutdown { get; set; }

    public MainWindow(
        MainWindowViewModel viewModel,
        IWindowService windowService,
        ISettingsService settingsService,
        IClipboardMonitorService clipboardMonitorService,
        IMonitorWorkAreaProvider? monitorWorkAreas = null)
    {
        InitializeComponent();
        DataContext = viewModel;
        _viewModel = viewModel;
        _settingsService = settingsService;
        _clipboardMonitorService = clipboardMonitorService;
        _monitorWorkAreas = monitorWorkAreas ?? Win32MonitorWorkAreaProvider.Instance;
        _ = windowService;

        AppIconHelper.ApplyWindowIcon(this);
        DesktopWidgetWindowHelper.Configure(this);

        SourceInitialized += MainWindow_OnSourceInitialized;
        LocationChanged += MainWindow_OnLocationChanged;
        _viewModel.PropertyChanged += ViewModel_OnPropertyChanged;
        _viewModel.Search.FocusRequested += Search_OnFocusRequested;
        _viewModel.TodoSearchResultActivated += ViewModel_OnTodoSearchResultActivated;
    }

    private void MainWindow_OnSourceInitialized(object? sender, EventArgs e)
    {
        if (_initialBoundsApplied)
        {
            return;
        }

        _initialBoundsApplied = true;
        ApplyInitialWindowBounds();
    }

    private void ViewModel_OnPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.IsPanelCollapsed) ||
            e.PropertyName == nameof(MainWindowViewModel.PanelWidth))
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
        ApplyPanelCollapseState(_monitorWorkAreas.GetForWindow(
            new WindowInteropHelper(this).Handle));
    }

    private void ApplyPanelCollapseState(MonitorWorkArea monitor, bool clampPosition = true)
    {
        if (_isApplyingMonitorBounds)
        {
            return;
        }

        _currentMonitor = monitor;
        _isApplyingMonitorBounds = true;
        try
        {
            var actualSize = PanelSizePolicy.ClampActualSize(
                _viewModel.PanelWidth,
                _viewModel.PanelHeight,
                monitor.WorkArea);
            ApplyPanelSizeConstraints(monitor);
            Width = actualSize.Width;
            Height = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : actualSize.Height;
            if (clampPosition)
            {
                ClampToVisibleWorkArea(monitor);
            }
            if (MainModulesGrid.RowDefinitions.Count == 0)
            {
                return;
            }

            if (_viewModel.IsPanelCollapsed)
            {
                MainModulesScrollViewer.Margin = new Thickness(16, 0, 16, 8);
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

            if (clampPosition && !_suppressPositionSave && !_isDragging)
            {
                SaveWindowPosition();
            }
        }
        finally
        {
            _isApplyingMonitorBounds = false;
        }
    }

    private void ApplyInitialWindowBounds()
    {
        _suppressPositionSave = true;
        try
        {
            var savedPosition = _viewModel.GetSavedWindowPosition();
            var savedPixelPosition = _viewModel.GetSavedWindowPixelPosition();
            var requestedHeight = _viewModel.IsPanelCollapsed
                ? CollapsedPanelHeight
                : _viewModel.PanelHeight;
            MonitorWorkArea targetMonitor;
            if (savedPixelPosition is { } pixelPosition)
            {
                targetMonitor = _monitorWorkAreas.GetForPixelPoint(
                    new PixelPoint(pixelPosition.Left, pixelPosition.Top));
                Left = pixelPosition.Left * 96 / targetMonitor.DpiX;
                Top = pixelPosition.Top * 96 / targetMonitor.DpiY;
            }
            else if (savedPosition is { } position)
            {
                Left = position.Left;
                Top = position.Top;
                targetMonitor = _monitorWorkAreas.GetForPixelPoint(
                    new PixelPoint(position.Left, position.Top));
            }
            else
            {
                targetMonitor = _monitorWorkAreas.GetForWindow(new WindowInteropHelper(this).Handle);
                Left = targetMonitor.WorkArea.Right - Width - 20;
            }

            ApplyPanelSizeConstraints(targetMonitor);
            var actualSize = PanelSizePolicy.ClampActualSize(
                _viewModel.PanelWidth,
                requestedHeight,
                targetMonitor.WorkArea);
            Width = actualSize.Width;
            Height = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : actualSize.Height;
            if (savedPosition == null && savedPixelPosition == null)
            {
                Top = targetMonitor.WorkArea.Top + (targetMonitor.WorkArea.Height - Height) / 2;
            }

            ClampToVisibleWorkArea(targetMonitor);
            ApplyPanelCollapseState(targetMonitor);
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

    private void MainWindow_OnLocationChanged(object? sender, EventArgs e)
    {
        if (!_initialBoundsApplied || _suppressPositionSave)
        {
            return;
        }

        var monitor = _monitorWorkAreas.GetForWindow(new WindowInteropHelper(this).Handle);
        if (_currentMonitor is { } currentMonitor && currentMonitor == monitor)
        {
            return;
        }

        ApplyPanelCollapseState(monitor, clampPosition: !_isDragging);
    }

    private void SearchButton_OnClick(object sender, RoutedEventArgs e) => OpenSearch();

    private void CompactMoreButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { ContextMenu: { } menu } button)
        {
            return;
        }

        menu.PlacementTarget = button;
        menu.Placement = PlacementMode.Bottom;
        menu.HorizontalOffset = -96;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            OpenSearch();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape && _viewModel.Search.IsOpen)
        {
            _viewModel.Search.CloseCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OpenSearch()
    {
        if (_viewModel.IsPanelCollapsed)
        {
            _viewModel.TogglePanelCollapseCommand.Execute(null);
        }

        _viewModel.Search.OpenCommand.Execute(null);
    }

    private void Search_OnFocusRequested(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() =>
        {
            GlobalSearchBox.Focus();
            Keyboard.Focus(GlobalSearchBox);
            GlobalSearchBox.SelectAll();
        });

    private void ViewModel_OnTodoSearchResultActivated(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(() => TodosModule.BringIntoView());

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

        if (_viewModel.IsPanelCollapsed)
        {
            modules =
            [
                new
                {
                    ModuleId = DashboardModuleIds.TimeWeather,
                    Element = (FrameworkElement?)TimeWeatherModule
                }
            ];
        }

        var visibleModules = modules;

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
        DashboardModuleIds.ModelRadar => ModelRadarModule,
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
        yield return ModelRadarModule;
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

        DragMoveAndFinalize();
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

        DragMoveAndFinalize();
    }

    private void MinimizeButton_OnClick(object sender, RoutedEventArgs e) => Hide();

    private void DragMoveAndFinalize()
    {
        _isDragging = true;
        try
        {
            DragMove();
        }
        finally
        {
            _isDragging = false;
            var monitor = _monitorWorkAreas.GetForWindow(new WindowInteropHelper(this).Handle);
            ApplyPanelCollapseState(monitor);
        }
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer scrollViewer)
        {
            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset - e.Delta);
            e.Handled = true;
        }
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
            _viewModel.Search.FocusRequested -= Search_OnFocusRequested;
            _viewModel.TodoSearchResultActivated -= ViewModel_OnTodoSearchResultActivated;
            _clipboardMonitorService.Stop();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void SaveWindowPosition()
    {
        _viewModel.SaveWindowPosition(Left, Top);
        if (PresentationSource.FromVisual(this) == null)
        {
            return;
        }

        var pixelPosition = PointToScreen(new Point(0, 0));
        _viewModel.SaveWindowPixelPosition(pixelPosition.X, pixelPosition.Y);
    }

    private void ClampToVisibleWorkArea(MonitorWorkArea? selectedMonitor = null)
    {
        var width = double.IsNaN(Width) || Width <= 0 ? ActualWidth : Width;
        var height = double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var requested = new LogicalRect(Left, Top, width, height);
        var monitor = selectedMonitor ?? _monitorWorkAreas.GetForWindow(
            new WindowInteropHelper(this).Handle);
        var clamped = MonitorWorkAreaGeometry.Clamp(requested, monitor.WorkArea);
        Left = clamped.Left;
        Top = clamped.Top;
        Width = clamped.Width;
        Height = clamped.Height;
    }

    private void ApplyPanelSizeConstraints(MonitorWorkArea monitor)
    {
        var bounds = PanelSizePolicy.GetBounds(monitor.WorkArea);
        MaxWidth = double.PositiveInfinity;
        MinWidth = bounds.MinWidth;
        MaxWidth = bounds.MaxWidth;

        var usableWorkAreaHeight = GetUsableWorkAreaHeight(monitor);
        var expandedMinimumHeight = Math.Min(
            IWindowService.MinPanelHeight,
            Math.Min(usableWorkAreaHeight, bounds.MaxHeight));
        var expandedMaximumHeight = Math.Max(
            expandedMinimumHeight,
            Math.Min(IWindowService.MaxPanelHeight, bounds.MaxHeight));
        MaxHeight = double.PositiveInfinity;
        MinHeight = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : expandedMinimumHeight;
        MaxHeight = _viewModel.IsPanelCollapsed ? CollapsedPanelHeight : expandedMaximumHeight;
    }

    private double GetUsableWorkAreaHeight() =>
        GetUsableWorkAreaHeight(_monitorWorkAreas.GetForWindow(
            new WindowInteropHelper(this).Handle));

    private static double GetUsableWorkAreaHeight(MonitorWorkArea monitor) =>
        Math.Max(MinimumCompactHeight, monitor.WorkArea.Height - WorkAreaMargin);

}
