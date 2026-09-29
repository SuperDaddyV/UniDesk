using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
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
            var menu = Assert.IsType<ContextMenu>(icon.ContextMenu);
            Assert.Equal(4, menu.Items.Count);
            ((MenuItem)menu.Items[0]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((MenuItem)menu.Items[1]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            ((MenuItem)menu.Items[3]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

            Assert.Equal(2, toggles);
            Assert.Equal(1, settings);
            Assert.Equal(1, exits);
        });
    }

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
