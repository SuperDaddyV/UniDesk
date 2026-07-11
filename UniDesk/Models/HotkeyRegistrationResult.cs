namespace UniDesk.Models;

public enum HotkeyRegistrationFailure
{
    None,
    InvalidGesture,
    NativeFailure
}

public readonly record struct HotkeyRegistrationResult(
    bool Success,
    string NormalizedHotkey,
    HotkeyRegistrationFailure Failure,
    int ErrorCode,
    bool PreviousHotkeyRestored)
{
    public static HotkeyRegistrationResult Succeeded(string normalizedHotkey) =>
        new(true, normalizedHotkey, HotkeyRegistrationFailure.None, 0, true);

    public static HotkeyRegistrationResult Invalid(bool previousHotkeyRestored) =>
        new(false, string.Empty, HotkeyRegistrationFailure.InvalidGesture, 0, previousHotkeyRestored);

    public static HotkeyRegistrationResult NativeFailure(
        string normalizedHotkey,
        int errorCode,
        bool previousHotkeyRestored) =>
        new(
            false,
            normalizedHotkey,
            HotkeyRegistrationFailure.NativeFailure,
            errorCode,
            previousHotkeyRestored);
}
