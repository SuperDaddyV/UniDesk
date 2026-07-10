using System.Windows;

namespace UniDesk.Services;

public interface IHotkeyService : IDisposable
{
    void Initialize(Window window);
    bool RegisterHotkey(string hotkeyString, Action callback);
    void UnregisterHotkey(string hotkeyString);
    void UnregisterAll();
}
