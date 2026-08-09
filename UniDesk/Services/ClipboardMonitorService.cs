using System.Runtime.InteropServices;
using System.ComponentModel;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using UniDesk.Helpers;

namespace UniDesk.Services;

public sealed class ClipboardMonitorService : IClipboardMonitorService
{
    private const int WmClipboardUpdate = 0x031D;
    private const int ClipboardWriteRetryCount = 15;
    private const int ClipboardWriteRetryDelayMilliseconds = 80;
    private const int ClipboardSelfChangeSuppressSeconds = 2;
    private const uint CfUnicodeText = 13;
    private const uint GmemMoveable = 0x0002;
    private const uint GmemZeroinit = 0x0040;

    private readonly IQuickTextService _quickTextService;
    private HwndSource? _source;
    private nint _handle;
    private bool _isStarted;
    private string? _pendingSelfClipboardText;
    private DateTime _pendingSelfClipboardTextExpiresAtUtc;

    public event Action? ClipboardHistoryChanged;

    public ClipboardMonitorService(IQuickTextService quickTextService)
    {
        _quickTextService = quickTextService;
    }

    public void Start(Window window)
    {
        if (_isStarted)
        {
            return;
        }

        try
        {
            _handle = new WindowInteropHelper(window).Handle;
            if (_handle == 0)
            {
                return;
            }

            _source = HwndSource.FromHwnd(_handle);
            _source?.AddHook(WndProc);
            if (AddClipboardFormatListener(_handle))
            {
                _isStarted = true;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ClipboardMonitorService.Start");
        }
    }

    public void Stop()
    {
        try
        {
            if (_isStarted && _handle != 0)
            {
                RemoveClipboardFormatListener(_handle);
            }

            _source?.RemoveHook(WndProc);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ClipboardMonitorService.Stop");
        }
        finally
        {
            _isStarted = false;
            _source = null;
            _handle = 0;
        }
    }

    public async Task<bool> TrySetTextAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        Exception? lastException = null;
        SetPendingSelfClipboardText(text);

        for (var attempt = 0; attempt < ClipboardWriteRetryCount; attempt++)
        {
            if (TrySetUnicodeText(text, out lastException))
            {
                return true;
            }

            if (attempt < ClipboardWriteRetryCount - 1)
            {
                await Task.Delay(ClipboardWriteRetryDelayMilliseconds);
            }
        }

        ClearPendingSelfClipboardText(text);
        if (lastException != null)
        {
            Logger.LogError(lastException, "ClipboardMonitorService.TrySetTextAsync");
        }

        return false;
    }

    private bool TrySetUnicodeText(string text, out Exception? exception)
    {
        exception = null;
        var ownerHandle = GetClipboardOwnerHandle();
        if (ownerHandle == 0)
        {
            exception = new InvalidOperationException("Clipboard owner window handle is not available.");
            return false;
        }

        var bytes = Encoding.Unicode.GetBytes(text + '\0');
        var memoryHandle = GlobalAlloc(GmemMoveable | GmemZeroinit, new UIntPtr((uint)bytes.Length));
        if (memoryHandle == 0)
        {
            exception = CreateClipboardException("GlobalAlloc");
            return false;
        }

        var isLocked = false;
        try
        {
            var memoryPointer = GlobalLock(memoryHandle);
            if (memoryPointer == 0)
            {
                exception = CreateClipboardException("GlobalLock");
                return false;
            }

            isLocked = true;
            Marshal.Copy(bytes, 0, memoryPointer, bytes.Length);
            GlobalUnlock(memoryHandle);
            isLocked = false;

            if (!OpenClipboard(ownerHandle))
            {
                exception = CreateClipboardException("OpenClipboard");
                return false;
            }

            try
            {
                if (!EmptyClipboard())
                {
                    exception = CreateClipboardException("EmptyClipboard");
                    return false;
                }

                if (SetClipboardData(CfUnicodeText, memoryHandle) == 0)
                {
                    exception = CreateClipboardException("SetClipboardData");
                    return false;
                }

                memoryHandle = 0;
                return true;
            }
            finally
            {
                CloseClipboard();
            }
        }
        finally
        {
            if (isLocked)
            {
                GlobalUnlock(memoryHandle);
            }

            if (memoryHandle != 0)
            {
                GlobalFree(memoryHandle);
            }
        }
    }

    private nint GetClipboardOwnerHandle()
    {
        if (_handle != 0)
        {
            return _handle;
        }

        return Application.Current?.MainWindow is { } mainWindow
            ? new WindowInteropHelper(mainWindow).Handle
            : 0;
    }

    private void SetPendingSelfClipboardText(string text)
    {
        _pendingSelfClipboardText = text;
        _pendingSelfClipboardTextExpiresAtUtc = DateTime.UtcNow.AddSeconds(ClipboardSelfChangeSuppressSeconds);
    }

    private void ClearPendingSelfClipboardText(string text)
    {
        if (string.Equals(_pendingSelfClipboardText, text, StringComparison.Ordinal))
        {
            _pendingSelfClipboardText = null;
        }
    }

    private bool ShouldSuppressSelfClipboardText(string text)
    {
        var pendingText = _pendingSelfClipboardText;
        if (pendingText == null)
        {
            return false;
        }

        _pendingSelfClipboardText = null;
        return DateTime.UtcNow <= _pendingSelfClipboardTextExpiresAtUtc &&
               string.Equals(pendingText, text, StringComparison.Ordinal);
    }

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WmClipboardUpdate)
        {
            handled = false;
            _ = HandleClipboardUpdateAsync();
        }

        return 0;
    }

    private async Task HandleClipboardUpdateAsync()
    {
        string? text;
        try
        {
            if (!Clipboard.ContainsText(TextDataFormat.UnicodeText))
            {
                return;
            }

            text = Clipboard.GetText(TextDataFormat.UnicodeText);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"剪贴板读取失败：{ex.Message}", "ClipboardMonitorService.HandleClipboardUpdate");
            return;
        }

        if (ShouldSuppressSelfClipboardText(text))
        {
            return;
        }

        await TryRecordClipboardTextAsync(text);
    }

    internal async Task<bool> TryRecordClipboardTextAsync(string text)
    {
        try
        {
            var recorded = await _quickTextService.RecordClipboardTextAsync(text);
            if (recorded)
            {
                ClipboardHistoryChanged?.Invoke();
            }

            return recorded;
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "ClipboardMonitorService.RecordClipboardText");
            return false;
        }
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool AddClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool RemoveClipboardFormatListener(nint hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(nint hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetClipboardData(uint uFormat, nint hMem);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint GetOpenClipboardWindow();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalLock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(nint hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint GlobalFree(nint hMem);

    private static Exception CreateClipboardException(string operation)
    {
        var error = Marshal.GetLastWin32Error();
        var openClipboardWindow = GetOpenClipboardWindow();
        return new Win32Exception(
            error,
            $"{operation} failed. OpenClipboardWindow=0x{openClipboardWindow.ToInt64():X}");
    }
}
