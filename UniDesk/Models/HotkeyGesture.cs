namespace UniDesk.Models;

public readonly record struct HotkeyGesture(
    string DisplayText,
    uint Modifiers,
    uint VirtualKey);
