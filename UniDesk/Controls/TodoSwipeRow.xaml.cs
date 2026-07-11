using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace UniDesk.Controls;

[ContentProperty(nameof(RowContent))]
public partial class TodoSwipeRow : UserControl
{
    public const double RevealWidth = 72;
    private const double DeleteThreshold = 48;

    private const double GestureThreshold = 4;

    private Point _dragStart;
    private double _offsetAtDragStart;
    private bool _swipePending;
    private bool _swipeActive;

    public static readonly DependencyProperty RowContentProperty =
        DependencyProperty.Register(
            nameof(RowContent),
            typeof(object),
            typeof(TodoSwipeRow),
            new PropertyMetadata(null));

    public static readonly DependencyProperty DeleteCommandProperty =
        DependencyProperty.Register(
            nameof(DeleteCommand),
            typeof(ICommand),
            typeof(TodoSwipeRow),
            new PropertyMetadata(null));

    public static readonly DependencyProperty EditCommandProperty =
        DependencyProperty.Register(
            nameof(EditCommand),
            typeof(ICommand),
            typeof(TodoSwipeRow),
            new PropertyMetadata(null));

    public static readonly DependencyProperty IsSearchHighlightedProperty =
        DependencyProperty.Register(
            nameof(IsSearchHighlighted),
            typeof(bool),
            typeof(TodoSwipeRow),
            new PropertyMetadata(false));

    public object? RowContent
    {
        get => GetValue(RowContentProperty);
        set => SetValue(RowContentProperty, value);
    }

    public ICommand? DeleteCommand
    {
        get => (ICommand?)GetValue(DeleteCommandProperty);
        set => SetValue(DeleteCommandProperty, value);
    }

    public ICommand? EditCommand
    {
        get => (ICommand?)GetValue(EditCommandProperty);
        set => SetValue(EditCommandProperty, value);
    }

    public bool IsSearchHighlighted
    {
        get => (bool)GetValue(IsSearchHighlightedProperty);
        set => SetValue(IsSearchHighlightedProperty, value);
    }

    public TodoSwipeRow()
    {
        InitializeComponent();
    }

    private void SwipeSurface_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsInsideCheckArea(e.OriginalSource as DependencyObject))
        {
            return;
        }

        if (e.ClickCount >= 2)
        {
            TryExecuteEdit();
            e.Handled = true;
            return;
        }

        _dragStart = e.GetPosition(RootGrid);
        _offsetAtDragStart = SwipeTransform.X;
        _swipePending = true;
        _swipeActive = false;
    }

    private void SwipeSurface_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (_swipePending)
        {
            var current = e.GetPosition(RootGrid);
            var deltaX = current.X - _dragStart.X;
            var deltaY = current.Y - _dragStart.Y;

            if (Math.Abs(deltaX) < GestureThreshold && Math.Abs(deltaY) < GestureThreshold)
            {
                return;
            }

            if (Math.Abs(deltaY) >= Math.Abs(deltaX))
            {
                _swipePending = false;
                return;
            }

            _swipePending = false;
            _swipeActive = true;
            SwipeSurface.CaptureMouse();
        }

        if (!_swipeActive)
        {
            return;
        }

        var position = e.GetPosition(RootGrid);
        var deltaXActive = position.X - _dragStart.X;

        if (deltaXActive > 0)
        {
            SetOffset(0);
            return;
        }

        var next = Math.Max(-RevealWidth, _offsetAtDragStart + deltaXActive);
        SetOffset(next);
    }

    private void SwipeSurface_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_swipePending)
        {
            _swipePending = false;
            return;
        }

        if (!_swipeActive)
        {
            return;
        }

        _swipeActive = false;
        SwipeSurface.ReleaseMouseCapture();

        if (SwipeTransform.X <= -DeleteThreshold)
        {
            ExecuteDelete();
            return;
        }

        if (SwipeTransform.X <= -RevealWidth * 0.45)
        {
            AnimateTo(-RevealWidth);
            return;
        }

        AnimateTo(0);
    }

    private void SwipeSurface_OnMouseLeave(object sender, MouseEventArgs e)
    {
        if (_swipePending)
        {
            _swipePending = false;
            return;
        }

        if (!_swipeActive || e.LeftButton == MouseButtonState.Pressed)
        {
            return;
        }

        _swipeActive = false;
        SwipeSurface.ReleaseMouseCapture();
        AnimateTo(SwipeTransform.X <= -RevealWidth * 0.45 ? -RevealWidth : 0);
    }

    private void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        e.Handled = true;
        ExecuteDelete();
    }

    private void TryExecuteEdit()
    {
        if (EditCommand?.CanExecute(DataContext) != true)
        {
            return;
        }

        EditCommand.Execute(DataContext);
        SetOffset(0);
    }

    private void ExecuteDelete()
    {
        AnimateTo(-ActualWidth, () =>
        {
            if (DeleteCommand?.CanExecute(DataContext) == true)
            {
                DeleteCommand.Execute(DataContext);
            }

            SetOffset(0);
        });
    }

    private void SetOffset(double x)
    {
        SwipeTransform.BeginAnimation(TranslateTransform.XProperty, null);
        SwipeTransform.X = x;
        UpdateDeleteStripVisibility(x);
    }

    private void UpdateDeleteStripVisibility(double offset)
    {
        DeleteStrip.Visibility = offset < -0.5 ? Visibility.Visible : Visibility.Collapsed;
    }

    private void AnimateTo(double target, Action? onCompleted = null)
    {
        UpdateDeleteStripVisibility(target);
        var animation = new DoubleAnimation
        {
            To = target,
            Duration = TimeSpan.FromMilliseconds(160),
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        };

        if (onCompleted != null)
        {
            animation.Completed += (_, _) => onCompleted();
        }

        SwipeTransform.BeginAnimation(TranslateTransform.XProperty, animation);
    }

    private static bool IsInsideCheckArea(DependencyObject? source)
    {
        var current = source;
        while (current != null)
        {
            if (current is FrameworkElement { Tag: "TodoCheck" })
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
