using System.Runtime.InteropServices;

namespace UniDesk.Services;

public sealed class Win32HotkeyPlatform : IHotkeyPlatform
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr windowHandle, int id);

    public bool Register(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey,
        out int errorCode)
    {
        var registered = RegisterHotKey(windowHandle, id, modifiers, virtualKey);
        errorCode = registered ? 0 : Marshal.GetLastWin32Error();
        return registered;
    }

    public bool Unregister(IntPtr windowHandle, int id) =>
        UnregisterHotKey(windowHandle, id);
}
