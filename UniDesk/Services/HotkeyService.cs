using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace UniDesk.Services;

public class HotkeyService : IHotkeyService, IDisposable
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RegisterHotKey(IntPtr hWnd, int id, uint fsModifiers, uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

    private const int WM_HOTKEY = 0x0312;

    private const uint MOD_ALT = 0x0001;
    private const uint MOD_CONTROL = 0x0002;
    private const uint MOD_SHIFT = 0x0004;
    private const uint MOD_WIN = 0x0008;

    private readonly Dictionary<int, (string HotkeyString, Action Callback)> _registeredHotkeys = new();
    private readonly INotificationService _notificationService;
    private readonly ILocalizationService _localizationService;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private int _currentId = 1;

    public HotkeyService(
        INotificationService notificationService,
        ILocalizationService localizationService)
    {
        _notificationService = notificationService;
        _localizationService = localizationService;
    }

    public void Initialize(Window window)
    {
        var helper = new WindowInteropHelper(window);
        _windowHandle = helper.Handle;

        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WM_HOTKEY)
        {
            var id = wParam.ToInt32();
            if (_registeredHotkeys.TryGetValue(id, out var hotkeyInfo))
            {
                hotkeyInfo.Callback.Invoke();
                handled = true;
            }
        }

        return IntPtr.Zero;
    }

    public bool RegisterHotkey(string hotkeyString, Action callback)
    {
        if (string.IsNullOrWhiteSpace(hotkeyString))
            return false;

        var (modifiers, key) = ParseHotkeyString(hotkeyString);
        if (key == 0)
        {
            _notificationService.ShowErrorMessage(_localizationService.Format("Hotkey.InvalidFormat", hotkeyString));
            return false;
        }

        var id = _currentId++;

        if (!RegisterHotKey(_windowHandle, id, modifiers, key))
        {
            var error = Marshal.GetLastWin32Error();
            _notificationService.ShowWarningMessage(_localizationService.Format("Hotkey.RegisterFailedFormat", hotkeyString, error));
            return false;
        }

        _registeredHotkeys[id] = (hotkeyString, callback);
        return true;
    }

    public void UnregisterHotkey(string hotkeyString)
    {
        var entry = _registeredHotkeys.FirstOrDefault(x => x.Value.HotkeyString == hotkeyString);
        if (entry.Key != 0)
        {
            UnregisterHotKey(_windowHandle, entry.Key);
            _registeredHotkeys.Remove(entry.Key);
        }
    }

    public void UnregisterAll()
    {
        foreach (var id in _registeredHotkeys.Keys.ToList())
        {
            UnregisterHotKey(_windowHandle, id);
        }
        _registeredHotkeys.Clear();
    }

    private (uint modifiers, uint key) ParseHotkeyString(string hotkeyString)
    {
        uint modifiers = 0;
        uint key = 0;

        var parts = hotkeyString.Split('+', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var upperPart = part.ToUpperInvariant();

            if (upperPart == "CTRL" || upperPart == "CONTROL")
                modifiers |= MOD_CONTROL;
            else if (upperPart == "ALT")
                modifiers |= MOD_ALT;
            else if (upperPart == "SHIFT")
                modifiers |= MOD_SHIFT;
            else if (upperPart == "WIN" || upperPart == "WINDOWS")
                modifiers |= MOD_WIN;
            else
            {
                key = MapKeyToVirtualKey(upperPart);
            }
        }

        return (modifiers, key);
    }

    private uint MapKeyToVirtualKey(string keyName)
    {
        return keyName.ToUpperInvariant() switch
        {
            "SPACE" => 0x20,
            "ENTER" => 0x0D,
            "TAB" => 0x09,
            "ESC" or "ESCAPE" => 0x1B,
            "F1" => 0x70,
            "F2" => 0x71,
            "F3" => 0x72,
            "F4" => 0x73,
            "F5" => 0x74,
            "F6" => 0x75,
            "F7" => 0x76,
            "F8" => 0x77,
            "F9" => 0x78,
            "F10" => 0x79,
            "F11" => 0x7A,
            "F12" => 0x7B,
            "A" => 0x41,
            "B" => 0x42,
            "C" => 0x43,
            "D" => 0x44,
            "E" => 0x45,
            "F" => 0x46,
            "G" => 0x47,
            "H" => 0x48,
            "I" => 0x49,
            "J" => 0x4A,
            "K" => 0x4B,
            "L" => 0x4C,
            "M" => 0x4D,
            "N" => 0x4E,
            "O" => 0x4F,
            "P" => 0x50,
            "Q" => 0x51,
            "R" => 0x52,
            "S" => 0x53,
            "T" => 0x54,
            "U" => 0x55,
            "V" => 0x56,
            "W" => 0x57,
            "X" => 0x58,
            "Y" => 0x59,
            "Z" => 0x5A,
            "0" => 0x30,
            "1" => 0x31,
            "2" => 0x32,
            "3" => 0x33,
            "4" => 0x34,
            "5" => 0x35,
            "6" => 0x36,
            "7" => 0x37,
            "8" => 0x38,
            "9" => 0x39,
            _ => 0
        };
    }

    public void Dispose()
    {
        UnregisterAll();
        _hwndSource?.RemoveHook(WndProc);
    }
}
