using System.Windows;
using System.Windows.Interop;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Services;

public sealed class HotkeyService : IHotkeyService
{
    private const int WmHotkey = 0x0312;
    private const int GlobalHotkeyId = 1;

    private readonly IHotkeyPlatform _platform;
    private IntPtr _windowHandle;
    private HwndSource? _hwndSource;
    private HotkeyGesture? _activeGesture;
    private Action? _activeCallback;
    private bool _disposed;

    public HotkeyService(IHotkeyPlatform platform)
    {
        _platform = platform;
    }

    public string ActiveHotkey => _activeGesture?.DisplayText ?? string.Empty;

    public void Initialize(Window window)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _windowHandle = new WindowInteropHelper(window).Handle;
        _hwndSource = HwndSource.FromHwnd(_windowHandle);
        _hwndSource?.AddHook(WndProc);
    }

    public HotkeyRegistrationResult ReplaceHotkey(string? hotkeyString, Action callback)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentNullException.ThrowIfNull(callback);

        if (string.IsNullOrWhiteSpace(hotkeyString))
        {
            UnregisterActive();
            return HotkeyRegistrationResult.Succeeded(string.Empty);
        }

        if (!HotkeyGestureParser.TryParse(hotkeyString, out var candidate))
        {
            return HotkeyRegistrationResult.Invalid(previousHotkeyRestored: _activeGesture.HasValue);
        }

        if (_activeGesture is { } current && current == candidate)
        {
            _activeCallback = callback;
            return HotkeyRegistrationResult.Succeeded(candidate.DisplayText);
        }

        var previousGesture = _activeGesture;
        var previousCallback = _activeCallback;
        UnregisterActive();

        if (_platform.Register(
                _windowHandle,
                GlobalHotkeyId,
                candidate.Modifiers,
                candidate.VirtualKey,
                out var errorCode))
        {
            _activeGesture = candidate;
            _activeCallback = callback;
            return HotkeyRegistrationResult.Succeeded(candidate.DisplayText);
        }

        var restored = RestorePrevious(previousGesture, previousCallback);
        return HotkeyRegistrationResult.NativeFailure(
            candidate.DisplayText,
            errorCode,
            restored);
    }

    public bool RegisterHotkey(string hotkeyString, Action callback) =>
        ReplaceHotkey(hotkeyString, callback).Success;

    public void UnregisterHotkey(string hotkeyString)
    {
        if (_activeGesture is { } active &&
            HotkeyGestureParser.TryParse(hotkeyString, out var requested) &&
            active == requested)
        {
            UnregisterActive();
        }
    }

    public void UnregisterAll() => UnregisterActive();

    private bool RestorePrevious(HotkeyGesture? gesture, Action? callback)
    {
        if (gesture is not { } previous || callback == null)
        {
            return true;
        }

        if (!_platform.Register(
                _windowHandle,
                GlobalHotkeyId,
                previous.Modifiers,
                previous.VirtualKey,
                out _))
        {
            return false;
        }

        _activeGesture = previous;
        _activeCallback = callback;
        return true;
    }

    private void UnregisterActive()
    {
        if (_activeGesture.HasValue)
        {
            _platform.Unregister(_windowHandle, GlobalHotkeyId);
        }

        _activeGesture = null;
        _activeCallback = null;
    }

    private IntPtr WndProc(
        IntPtr hwnd,
        int message,
        IntPtr wParam,
        IntPtr lParam,
        ref bool handled)
    {
        _ = hwnd;
        _ = lParam;
        if (message == WmHotkey && wParam.ToInt32() == GlobalHotkeyId)
        {
            _activeCallback?.Invoke();
            handled = true;
        }

        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        UnregisterActive();
        _hwndSource?.RemoveHook(WndProc);
        _hwndSource = null;
    }
}
