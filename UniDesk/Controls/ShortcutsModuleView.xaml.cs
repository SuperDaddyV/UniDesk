using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using UniDesk.Models;
using UniDesk.ViewModels;

namespace UniDesk.Controls;

public partial class ShortcutsModuleView : UserControl
{
    private Point _shortcutDragStart;
    private ShortcutItem? _draggedShortcut;
    private FrameworkElement? _shortcutDragSourceElement;
    private FrameworkElement? _shortcutDragTargetElement;
    private bool _isShortcutDragActive;
    private Point _scrollPanStart;
    private double _scrollPanOffsetStart;
    private bool _scrollPanPending;
    private bool _scrollPanActive;

    private ShortcutsViewModel? ViewModel => DataContext as ShortcutsViewModel;

    public ShortcutsModuleView()
    {
        InitializeComponent();
    }

    private void ShortcutItem_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel?.IsEditingShortcuts != true || IsInside<Button>(e.OriginalSource as DependencyObject)) return;
        _shortcutDragStart = e.GetPosition(null);
        _draggedShortcut = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        _shortcutDragSourceElement = sender as FrameworkElement;
        _isShortcutDragActive = false;
        if (sender is UIElement element) element.CaptureMouse();
    }

    private async void ShortcutItem_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        var source = _draggedShortcut;
        var target = _isShortcutDragActive ? GetShortcutItemAt(e.GetPosition(ShortcutItemsControl)) : null;
        if (sender is UIElement element && element.IsMouseCaptured) element.ReleaseMouseCapture();
        _draggedShortcut = null;
        _isShortcutDragActive = false;
        ClearShortcutDragVisuals();
        if (source != null && target != null && source.Id != target.Id && ViewModel != null)
        {
            await ViewModel.MoveShortcutAsync(source, target);
            e.Handled = true;
        }
        else if (target != null)
        {
            e.Handled = true;
        }
    }

    private void ShortcutItem_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (ViewModel?.IsEditingShortcuts != true || e.LeftButton != MouseButtonState.Pressed || _draggedShortcut == null) return;
        var current = e.GetPosition(null);
        if (Math.Abs(current.X - _shortcutDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(current.Y - _shortcutDragStart.Y) < SystemParameters.MinimumVerticalDragDistance) return;

        if (!_isShortcutDragActive)
        {
            _isShortcutDragActive = true;
            _shortcutDragSourceElement = sender as FrameworkElement;
            if (_shortcutDragSourceElement != null) _shortcutDragSourceElement.Opacity = 0.55;
        }

        SetShortcutDragTarget(GetShortcutItemElementAt(e.GetPosition(ShortcutItemsControl)));
        e.Handled = true;
    }

    private ShortcutItem? GetShortcutItemAt(Point position) =>
        GetShortcutItemElementAt(position)?.DataContext as ShortcutItem;

    private FrameworkElement? GetShortcutItemElementAt(Point position)
    {
        if (position.X < 0 || position.Y < 0 ||
            position.X > ShortcutItemsControl.ActualWidth || position.Y > ShortcutItemsControl.ActualHeight) return null;
        return FindShortcutItemElement(VisualTreeHelper.HitTest(ShortcutItemsControl, position)?.VisualHit);
    }

    private static FrameworkElement? FindShortcutItemElement(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement { Name: "ShortcutItemRoot", DataContext: ShortcutItem } element) return element;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }

    private void ShortcutItem_OnDragEnter(object sender, DragEventArgs e)
    {
        if (ViewModel?.IsEditingShortcuts == true && e.Data.GetDataPresent(typeof(ShortcutItem)))
            SetShortcutDragTarget(sender as FrameworkElement);
    }

    private void ShortcutItem_OnDragOver(object sender, DragEventArgs e)
    {
        if (ViewModel?.IsEditingShortcuts != true || !e.Data.GetDataPresent(typeof(ShortcutItem))) return;
        e.Effects = DragDropEffects.Move;
        SetShortcutDragTarget(sender as FrameworkElement);
        e.Handled = true;
    }

    private void ShortcutItem_OnDragLeave(object sender, DragEventArgs e)
    {
        if (ReferenceEquals(sender, _shortcutDragTargetElement)) SetShortcutDragTarget(null);
    }

    private async void ShortcutItem_OnDrop(object sender, DragEventArgs e)
    {
        if (ViewModel?.IsEditingShortcuts != true) return;
        var source = e.Data.GetData(typeof(ShortcutItem)) as ShortcutItem;
        var target = (sender as FrameworkElement)?.DataContext as ShortcutItem;
        await ViewModel.MoveShortcutAsync(source, target);
        e.Handled = true;
    }

    private void SetShortcutDragTarget(FrameworkElement? target)
    {
        if (_shortcutDragTargetElement != null && !ReferenceEquals(_shortcutDragTargetElement, _shortcutDragSourceElement))
            _shortcutDragTargetElement.Opacity = 1;
        _shortcutDragTargetElement = target;
        if (_shortcutDragTargetElement != null && !ReferenceEquals(_shortcutDragTargetElement, _shortcutDragSourceElement))
            _shortcutDragTargetElement.Opacity = 0.78;
    }

    private void ClearShortcutDragVisuals()
    {
        if (_shortcutDragSourceElement != null) _shortcutDragSourceElement.Opacity = 1;
        if (_shortcutDragTargetElement != null) _shortcutDragTargetElement.Opacity = 1;
        _shortcutDragSourceElement = null;
        _shortcutDragTargetElement = null;
        _isShortcutDragActive = false;
    }

    private void ShortcutModule_OnPreviewDragEnter(object sender, DragEventArgs e) => UpdateFileDropFeedback(e);
    private void ShortcutModule_OnPreviewDragOver(object sender, DragEventArgs e) => UpdateFileDropFeedback(e);

    private void ShortcutModule_OnPreviewDragLeave(object sender, DragEventArgs e)
    {
        if (!IsShortcutFileDrop(e) || ViewModel == null) return;
        ViewModel.IsShortcutDropTargetActive = false;
        e.Handled = true;
    }

    private async void ShortcutModule_OnPreviewDrop(object sender, DragEventArgs e)
    {
        if (!IsShortcutFileDrop(e) || ViewModel == null) return;
        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
        ViewModel.IsShortcutDropTargetActive = false;
        await ViewModel.AddFromPathsAsync(GetFileDropPaths(e));
    }

    private void UpdateFileDropFeedback(DragEventArgs e)
    {
        if (!IsShortcutFileDrop(e) || ViewModel == null) return;
        e.Effects = DragDropEffects.Copy;
        ViewModel.IsShortcutDropTargetActive = true;
        e.Handled = true;
    }

    private static bool IsShortcutFileDrop(DragEventArgs e) =>
        e.Data.GetDataPresent(DataFormats.FileDrop) && GetFileDropPaths(e).Count > 0;

    private static IReadOnlyList<string> GetFileDropPaths(DragEventArgs e) =>
        e.Data.GetData(DataFormats.FileDrop) is string[] paths
            ? paths.Where(path => !string.IsNullOrWhiteSpace(path)).ToList()
            : [];

    private void ShortcutAddPopup_OnClosed(object? sender, EventArgs e) =>
        ViewModel?.CloseShortcutAddMenusCommand.Execute(null);

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer viewer ||
            ViewModel?.IsEditingShortcuts == true && FindShortcutItemElement(e.OriginalSource as DependencyObject) != null ||
            IsInside<Button>(e.OriginalSource as DependencyObject)) return;
        _scrollPanPending = true;
        _scrollPanActive = false;
        _scrollPanStart = e.GetPosition(viewer);
        _scrollPanOffsetStart = viewer.VerticalOffset;
    }

    private void ScrollViewer_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (sender is not ScrollViewer viewer || e.LeftButton != MouseButtonState.Pressed) return;
        if (_scrollPanPending)
        {
            var current = e.GetPosition(viewer);
            var dx = current.X - _scrollPanStart.X;
            var dy = current.Y - _scrollPanStart.Y;
            if (Math.Abs(dx) < 4 && Math.Abs(dy) < 4) return;
            if (Math.Abs(dy) <= Math.Abs(dx)) { _scrollPanPending = false; return; }
            _scrollPanPending = false;
            _scrollPanActive = true;
            viewer.CaptureMouse();
            viewer.Cursor = Cursors.SizeAll;
        }
        if (!_scrollPanActive) return;
        var position = e.GetPosition(viewer);
        viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(viewer.ScrollableHeight, _scrollPanOffsetStart - (position.Y - _scrollPanStart.Y))));
        e.Handled = true;
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) => EndScrollPan(sender as ScrollViewer);
    private void ScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_scrollPanActive && e.LeftButton != MouseButtonState.Pressed) EndScrollPan(sender as ScrollViewer);
    }
    private void EndScrollPan(ScrollViewer? viewer)
    {
        if (!_scrollPanPending && !_scrollPanActive) return;
        _scrollPanPending = false;
        _scrollPanActive = false;
        viewer?.ReleaseMouseCapture();
        if (viewer != null) viewer.Cursor = null;
    }

    private static bool IsInside<T>(DependencyObject? source) where T : DependencyObject
    {
        var current = source;
        while (current != null)
        {
            if (current is T) return true;
            current = VisualTreeHelper.GetParent(current);
        }
        return false;
    }
}
