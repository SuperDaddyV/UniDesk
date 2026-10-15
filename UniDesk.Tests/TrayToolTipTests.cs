using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Hardcodet.Wpf.TaskbarNotification;
using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public sealed class TrayToolTipTests
{
    [Fact]
    public void NonEmptyToolTipTextAlone_StillCreatesWpfToolTipInPinnedDependency()
    {
        ClockLayoutTests.RunSta(() =>
        {
            using var icon = new TaskbarIcon { ToolTipText = "UniDesk - Desktop assistant" };
            Assert.Null(icon.TrayToolTip);
            Assert.NotNull(icon.TrayToolTipResolved);
            Assert.Equal(icon.ToolTipText, icon.TrayToolTipResolved.Content);
        });
    }

    [Fact]
    public void TrayService_PreventsResolvedWpfToolTipOpeningAcrossLanguageAndLifecycle()
    {
        ClockLayoutTests.RunSta(() =>
        {
            EnsureWpfResources();
            var localization = new TestLocalizationService();
            for (var i = 0; i < 2; i++)
            {
                using var service = new TrayService(new NoOpNotificationService(), localization);
                service.Initialize();
                var icon = GetIcon(service);
                service.Initialize();
                Assert.Same(icon, GetIcon(service));
                Assert.Equal("UniDesk - Desktop assistant", icon.ToolTipText);
                Assert.Equal(icon.ToolTipText, GetShellTipText(icon));
                Assert.NotNull(icon.TrayToolTipResolved); // The pinned package still resolves ToolTipText to WPF.
                AssertResolvedToolTipCannotOpen(icon);
                Assert.Null(icon.TrayToolTip);

                localization.SetLanguage("zh-CN");
                Assert.Equal("UniDesk - 桌面侧边助手", icon.ToolTipText);
                Assert.Equal(icon.ToolTipText, GetShellTipText(icon));
                Assert.NotNull(icon.TrayToolTipResolved);
                AssertResolvedToolTipCannotOpen(icon);

                service.Dispose();
                service.Dispose();
                Assert.True(icon.IsDisposed);
                Assert.Equal(0, localization.ListenerCount);
                localization.SetLanguage("en-US");
                Assert.Throws<ObjectDisposedException>(service.Initialize);
            }
        });
    }

    [Fact]
    public void TrayService_PreservesMenuAndDoubleClickCallbacks()
    {
        ClockLayoutTests.RunSta(() =>
        {
            EnsureWpfResources();
            using var service = new TrayService(new NoOpNotificationService(), new TestLocalizationService());
            service.Initialize();
            var icon = GetIcon(service);
            var toggles = 0;
            var settings = 0;
            var exits = 0;
            service.TrayIconDoubleClick += () => toggles++;
            service.SettingsRequested += () => settings++;
            service.ExitRequested += () => exits++;

            icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayMouseDoubleClickEvent));
            var menu = Assert.IsAssignableFrom<ContextMenu>(icon.ContextMenu);
            Assert.Equal(4, menu.Items.Count);
            ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((MenuItem)menu.Items[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(2, toggles);
            Assert.Equal(1, settings);
            Assert.Equal(1, exits);
        });
    }

    [Theory]
    [InlineData(3000, 1800)]
    [InlineData(-1600, 600)]
    public void ContextMenu_ShouldNotTreatNativeScreenCoordinatesAsWpfOffsets(int x, int y)
    {
        ClockLayoutTests.RunSta(() =>
        {
            EnsureWpfResources();
            var localization = new TestLocalizationService();
            using var service = new TrayService(new NoOpNotificationService(), localization);
            service.Initialize();
            var icon = GetIcon(service);
            foreach (var language in new[] { "en-US", "zh-CN" })
            {
                localization.SetLanguage(language);
                var menu = icon.ContextMenu!;
                // Reproduce the assignments made by Hardcodet 1.1.0 ShowContextMenu.
                // Opening the native popup is separately covered in the isolated WPF harness.
                menu.Placement = PlacementMode.AbsolutePoint;
                menu.HorizontalOffset = x;
                menu.VerticalOffset = y;
                Assert.Equal(PlacementMode.AbsolutePoint, menu.Placement);
                Assert.Equal(0, menu.HorizontalOffset);
                Assert.Equal(0, menu.VerticalOffset);
            }
        });
    }

    [Theory]
    [InlineData(1d, 2200, 1280)]
    [InlineData(1.25d, 2200, 1280)]
    [InlineData(1.5d, 2200, 1280)]
    [InlineData(1.5d, -1600, 600)]
    public void ContextMenu_ShouldCapturePhysicalCoordinatesAtRightClick_BeforeDependencyDpiConversion(double dpi, double x, double y)
    {
        ClockLayoutTests.RunSta(() =>
        {
            EnsureWpfResources();
            var reads = 0;
            Point? point = new Point(x, y);
            using var service = CreateWithPosition(() => { reads++; return point; });
            service.Initialize();
            var icon = GetIcon(service);
            foreach (var anchor in new[] { new Point(x, y), new Point(x - 100, y - 100) })
            {
                point = anchor;
                var before = reads;
                icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayRightMouseUpEvent));
                Assert.Equal(before + 1, reads);
                point = new Point(1, 1); // The pointer can move before popup creation.
                var preview = new RoutedEventArgs(TaskbarIcon.PreviewTrayContextMenuOpenEvent);
                icon.RaiseEvent(preview);
                Assert.False(preview.Handled);
                var menu = icon.ContextMenu!;
                // Actual Hardcodet path divides physical cursor coordinates before these assignments.
                menu.HorizontalOffset = (int)(anchor.X / dpi);
                menu.VerticalOffset = (int)(anchor.Y / dpi);
                Assert.Equal(new Rect(anchor, new Size()), menu.PlacementRectangle);
                Assert.Equal(PlacementMode.AbsolutePoint, menu.Placement);
                Assert.Equal(before + 1, reads);
                menu.HorizontalOffset = 9999;
                menu.VerticalOffset = 8888;
                Assert.Equal(new Rect(anchor, new Size()), menu.PlacementRectangle);
                Assert.Equal(0, menu.HorizontalOffset);
                Assert.Equal(0, menu.VerticalOffset);
            }
        });
    }

    [Fact]
    public void ContextMenu_ShouldCancelInvalidCapture_AndNotReusePreviousOpening()
    {
        ClockLayoutTests.RunSta(() =>
        {
            EnsureWpfResources();
            Point? point = new Point(2200, 1280);
            using var service = CreateWithPosition(() => point);
            service.Initialize();
            var icon = GetIcon(service);
            foreach (var position in new Point?[] { point, null, new(double.NaN, 10), new(20, double.PositiveInfinity), new(-600, 700) })
            {
                point = position;
                icon.RaiseEvent(new RoutedEventArgs(TaskbarIcon.TrayRightMouseUpEvent));
                var preview = new RoutedEventArgs(TaskbarIcon.PreviewTrayContextMenuOpenEvent);
                icon.RaiseEvent(preview);
                var valid = position is { } p && double.IsFinite(p.X) && double.IsFinite(p.Y);
                Assert.Equal(!valid, preview.Handled);
                if (valid) Assert.Equal(new Rect(position!.Value, new Size()), icon.ContextMenu!.PlacementRectangle);
            }
        });
    }

    private static TrayService CreateWithPosition(Func<Point?> position) =>
        new(new NoOpNotificationService(), new TestLocalizationService(), position);
    private static void AssertResolvedToolTipCannotOpen(TaskbarIcon icon)
    {
        var toolTip = icon.TrayToolTipResolved;
        Assert.NotNull(toolTip);
        toolTip.Opacity = 0; // Keep the regression invisible if it fails on the old implementation.
        try
        {
            var onToolTipChange = typeof(TaskbarIcon).GetMethod("OnToolTipChange", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(onToolTipChange);
            onToolTipChange.Invoke(icon, [true]);
            Assert.False(toolTip.IsOpen, "Hardcodet opened a WPF ToolTip from the tray hover notification.");
        }
        finally
        {
            toolTip.IsOpen = false;
        }
    }

    private static TaskbarIcon GetIcon(TrayService service)
    {
        var field = typeof(TrayService).GetField("_notifyIcon", BindingFlags.Instance | BindingFlags.NonPublic);
        return Assert.IsType<TaskbarIcon>(field?.GetValue(service));
    }

    private static string GetShellTipText(TaskbarIcon icon)
    {
        var data = typeof(TaskbarIcon).GetField("iconData", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(icon);
        Assert.NotNull(data);
        var tip = data.GetType().GetField("ToolTipText", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return Assert.IsType<string>(tip?.GetValue(data));
    }

    private static void EnsureWpfResources()
    {
        Application.ResourceAssembly ??= typeof(TrayService).Assembly;
    }

    private sealed class NoOpNotificationService : INotificationService
    {
        public void ShowInfoMessage(string message) { }
        public void ShowWarningMessage(string message) { }
        public void ShowErrorMessage(string message) { }
        public void ShowSuccessMessage(string message) { }
        public bool ShowConfirmDialog(string message, string? title = null) => true;
    }

    private sealed class TestLocalizationService : ILocalizationService
    {
        private EventHandler? _languageChanged;
        private string _language = "en-US";
        public event EventHandler? LanguageChanged
        {
            add => _languageChanged += value;
            remove => _languageChanged -= value;
        }
        public int ListenerCount => _languageChanged?.GetInvocationList().Length ?? 0;
        public string CurrentLanguage => _language;
        public CultureInfo CurrentCulture => CultureInfo.GetCultureInfo(_language);
        public IReadOnlyList<LanguageOption> SupportedLanguages => [];
        public void Initialize(ISettingsService settingsService) { }
        public string NormalizeLanguage(string? language) => language ?? "en-US";
        public void SetLanguage(string? language)
        {
            _language = NormalizeLanguage(language);
            _languageChanged?.Invoke(this, EventArgs.Empty);
        }
        public string GetString(string key) => key == "Common.AppDescription"
            ? _language == "zh-CN" ? "桌面侧边助手" : "Desktop assistant"
            : key;
        public string Format(string key, params object?[] args) => GetString(key);
    }
}
