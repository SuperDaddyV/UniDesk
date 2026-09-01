using Microsoft.Win32;
using System.Windows;
using UniDesk.Helpers;

namespace UniDesk.Services;

public sealed class SystemThemeService : ISystemThemeService
{
    private const string PersonalizeKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightThemeValue = "AppsUseLightTheme";
    private bool _initialized;

    public bool IsLightTheme { get; private set; } = true;

    public event EventHandler<bool>? ThemeChanged;

    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;
        IsLightTheme = ReadIsLightTheme();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null)
        {
            RefreshTheme();
            return;
        }

        dispatcher.BeginInvoke(RefreshTheme);
    }

    private void RefreshTheme()
    {
        var isLight = ReadIsLightTheme();
        if (isLight == IsLightTheme)
        {
            return;
        }

        IsLightTheme = isLight;
        ThemeChanged?.Invoke(this, isLight);
    }

    private static bool ReadIsLightTheme()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKeyPath);
            return SystemThemeSelection.IsLightTheme(key?.GetValue(AppsUseLightThemeValue));
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "SystemThemeService.ReadIsLightTheme");
            return true;
        }
    }

    public void Dispose()
    {
        if (!_initialized)
        {
            return;
        }

        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _initialized = false;
    }
}
