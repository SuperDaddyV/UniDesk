using System.Windows;
using UniDesk.Models;

namespace UniDesk.Services;

public interface IHotkeyService : IDisposable
{
    string ActiveHotkey { get; }

    void Initialize(Window window);
    HotkeyRegistrationResult ReplaceHotkey(string? hotkeyString, Action callback);
    bool RegisterHotkey(string hotkeyString, Action callback);
    void UnregisterHotkey(string hotkeyString);
    void UnregisterAll();
}
