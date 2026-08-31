using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using UniDesk.Helpers;

namespace UniDesk.Windows;

public enum ToastKind
{
    Info,
    Success,
    Warning,
    Error
}

public partial class ToastWindow : Window
{
    private DispatcherTimer? _closeTimer;

    public ToastWindow(string message, ToastKind kind)
    {
        InitializeComponent();
        MessageText.Text = message;
        ApplyKindStyle(kind);
        DesktopWidgetWindowHelper.Configure(this);
    }

    public void ShowWithAutoClose(int durationMs, double stackIndex)
    {
        Opacity = 0;
        Show();
        ToastPlacementHelper.PositionNearAnchor(this, stackIndex: stackIndex);

        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(160))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
        });

        _closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(durationMs) };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            CloseWithFade();
        };
        _closeTimer.Start();
    }

    private void ApplyKindStyle(ToastKind kind)
    {
        var (resourceKey, fallback) = kind switch
        {
            ToastKind.Success => ("SuccessBrush", Brushes.ForestGreen),
            ToastKind.Warning => ("WarningBrush", Brushes.DarkGoldenrod),
            ToastKind.Error => ("DangerBrush", Brushes.Crimson),
            _ => ("AccentBrush", Brushes.DodgerBlue)
        };

        AccentBar.Background = Application.Current?.TryFindResource(resourceKey) as Brush ?? fallback;
    }

    private void CloseWithFade()
    {
        var fade = new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => Close();
        BeginAnimation(OpacityProperty, fade);
    }

    protected override void OnClosed(EventArgs e)
    {
        _closeTimer?.Stop();
        base.OnClosed(e);
    }
}
