namespace UniDesk.Services;

public interface IHotkeyPlatform
{
    bool Register(
        IntPtr windowHandle,
        int id,
        uint modifiers,
        uint virtualKey,
        out int errorCode);

    bool Unregister(IntPtr windowHandle, int id);
}
