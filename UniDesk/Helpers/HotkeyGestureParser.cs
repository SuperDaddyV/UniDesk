using System.Windows.Input;
using UniDesk.Models;

namespace UniDesk.Helpers;

public static class HotkeyGestureParser
{
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;

    public static bool TryParse(string? value, out HotkeyGesture gesture)
    {
        gesture = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        uint modifiers = 0;
        uint virtualKey = 0;
        string? keyText = null;

        foreach (var part in value.Split(
                     '+',
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            switch (part.ToUpperInvariant())
            {
                case "CTRL":
                case "CONTROL":
                    modifiers |= ModControl;
                    break;
                case "ALT":
                    modifiers |= ModAlt;
                    break;
                case "SHIFT":
                    modifiers |= ModShift;
                    break;
                case "WIN":
                case "WINDOWS":
                    modifiers |= ModWin;
                    break;
                default:
                    if (virtualKey != 0 || !TryMapKeyName(part, out virtualKey, out keyText))
                    {
                        return false;
                    }
                    break;
            }
        }

        if (modifiers == 0 || virtualKey == 0 || keyText == null)
        {
            return false;
        }

        gesture = new HotkeyGesture(BuildDisplayText(modifiers, keyText), modifiers, virtualKey);
        return true;
    }

    public static bool TryCreate(Key key, ModifierKeys modifiers, out HotkeyGesture gesture)
    {
        gesture = default;
        var nativeModifiers = ToNativeModifiers(modifiers);
        if (nativeModifiers == 0 || !TryMapWpfKey(key, out var virtualKey, out var keyText))
        {
            return false;
        }

        gesture = new HotkeyGesture(
            BuildDisplayText(nativeModifiers, keyText),
            nativeModifiers,
            virtualKey);
        return true;
    }

    private static uint ToNativeModifiers(ModifierKeys modifiers)
    {
        uint result = 0;
        if (modifiers.HasFlag(ModifierKeys.Control)) result |= ModControl;
        if (modifiers.HasFlag(ModifierKeys.Alt)) result |= ModAlt;
        if (modifiers.HasFlag(ModifierKeys.Shift)) result |= ModShift;
        if (modifiers.HasFlag(ModifierKeys.Windows)) result |= ModWin;
        return result;
    }

    private static bool TryMapKeyName(
        string value,
        out uint virtualKey,
        out string? displayText)
    {
        var upper = value.Trim().ToUpperInvariant();
        if (upper == "SPACE")
        {
            virtualKey = 0x20;
            displayText = "Space";
            return true;
        }

        if (upper.Length == 1 && upper[0] is >= 'A' and <= 'Z')
        {
            virtualKey = upper[0];
            displayText = upper;
            return true;
        }

        if (upper.Length == 1 && upper[0] is >= '0' and <= '9')
        {
            virtualKey = upper[0];
            displayText = upper;
            return true;
        }

        if (upper.StartsWith('F') &&
            int.TryParse(upper.AsSpan(1), out var functionKey) &&
            functionKey is >= 1 and <= 12)
        {
            virtualKey = (uint)(0x70 + functionKey - 1);
            displayText = $"F{functionKey}";
            return true;
        }

        virtualKey = 0;
        displayText = null;
        return false;
    }

    private static bool TryMapWpfKey(
        Key key,
        out uint virtualKey,
        out string displayText)
    {
        if (key is >= Key.A and <= Key.Z)
        {
            var offset = key - Key.A;
            virtualKey = (uint)(0x41 + offset);
            displayText = ((char)('A' + offset)).ToString();
            return true;
        }

        if (key is >= Key.D0 and <= Key.D9)
        {
            var offset = key - Key.D0;
            virtualKey = (uint)(0x30 + offset);
            displayText = ((char)('0' + offset)).ToString();
            return true;
        }

        if (key is >= Key.F1 and <= Key.F12)
        {
            var offset = key - Key.F1;
            virtualKey = (uint)(0x70 + offset);
            displayText = $"F{offset + 1}";
            return true;
        }

        if (key == Key.Space)
        {
            virtualKey = 0x20;
            displayText = "Space";
            return true;
        }

        virtualKey = 0;
        displayText = string.Empty;
        return false;
    }

    private static string BuildDisplayText(uint modifiers, string keyText)
    {
        var parts = new List<string>(5);
        if ((modifiers & ModControl) != 0) parts.Add("Ctrl");
        if ((modifiers & ModAlt) != 0) parts.Add("Alt");
        if ((modifiers & ModShift) != 0) parts.Add("Shift");
        if ((modifiers & ModWin) != 0) parts.Add("Win");
        parts.Add(keyText);
        return string.Join('+', parts);
    }
}
