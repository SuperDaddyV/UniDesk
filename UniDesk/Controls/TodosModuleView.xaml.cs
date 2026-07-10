using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace UniDesk.Controls;

public partial class TodosModuleView : UserControl
{
    private Point _scrollPanStart;
    private double _scrollPanOffsetStart;
    private bool _scrollPanPending;
    private bool _scrollPanActive;

    public TodosModuleView()
    {
        InitializeComponent();
    }

    private void OnScrollViewerPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer viewer) return;
        viewer.ScrollToVerticalOffset(viewer.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not ScrollViewer viewer || ShouldIgnoreScrollPan(e.OriginalSource as DependencyObject)) return;
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
            var deltaX = current.X - _scrollPanStart.X;
            var deltaY = current.Y - _scrollPanStart.Y;
            if (Math.Abs(deltaX) < 4 && Math.Abs(deltaY) < 4) return;
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

        if (!_scrollPanActive) return;
        var position = e.GetPosition(viewer);
        var offset = _scrollPanOffsetStart - (position.Y - _scrollPanStart.Y);
        viewer.ScrollToVerticalOffset(Math.Max(0, Math.Min(viewer.ScrollableHeight, offset)));
        e.Handled = true;
    }

    private void ScrollViewer_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e) =>
        EndScrollPan(sender as ScrollViewer);

    private void ScrollViewer_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_scrollPanActive && e.LeftButton != MouseButtonState.Pressed)
        {
            EndScrollPan(sender as ScrollViewer);
        }
    }

    private void EndScrollPan(ScrollViewer? viewer)
    {
        if (!_scrollPanPending && !_scrollPanActive) return;
        _scrollPanPending = false;
        _scrollPanActive = false;
        viewer?.ReleaseMouseCapture();
        if (viewer != null) viewer.Cursor = null;
    }

    private static bool ShouldIgnoreScrollPan(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement { Tag: "TodoCheck" } or Button) return true;
            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
