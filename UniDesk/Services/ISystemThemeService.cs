namespace UniDesk.Services;

public interface ISystemThemeService : IDisposable
{
    bool IsLightTheme { get; }
    event EventHandler<bool>? ThemeChanged;
    void Initialize();
}
