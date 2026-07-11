using UniDesk.Models;
using UniDesk.Services;

namespace UniDesk.Tests;

public class HotkeyServiceTests
{
    [Fact]
    public void ReplaceHotkey_ValidGesture_RegistersCanonicalValue()
    {
        var platform = new FakeHotkeyPlatform();
        var service = new HotkeyService(platform);

        var result = service.ReplaceHotkey("ctrl+shift+k", () => { });

        Assert.True(result.Success);
        Assert.Equal("Ctrl+Shift+K", result.NormalizedHotkey);
        Assert.Equal("Ctrl+Shift+K", service.ActiveHotkey);
        Assert.Single(platform.Registrations);
    }

    [Fact]
    public void ReplaceHotkey_Conflict_RestoresPreviousRegistration()
    {
        var platform = new FakeHotkeyPlatform();
        var service = new HotkeyService(platform);
        Assert.True(service.ReplaceHotkey("Ctrl+Alt+Space", () => { }).Success);
        platform.FailNextRegistrationWith(1409);

        var result = service.ReplaceHotkey("Ctrl+Shift+K", () => { });

        Assert.False(result.Success);
        Assert.Equal(HotkeyRegistrationFailure.NativeFailure, result.Failure);
        Assert.Equal(1409, result.ErrorCode);
        Assert.True(result.PreviousHotkeyRestored);
        Assert.Equal("Ctrl+Alt+Space", service.ActiveHotkey);
        Assert.Single(platform.Registrations);
    }

    [Fact]
    public void ReplaceHotkey_Empty_DisablesRegistration()
    {
        var platform = new FakeHotkeyPlatform();
        var service = new HotkeyService(platform);
        Assert.True(service.ReplaceHotkey("Ctrl+Alt+Space", () => { }).Success);

        var result = service.ReplaceHotkey(string.Empty, () => { });

        Assert.True(result.Success);
        Assert.Equal(string.Empty, result.NormalizedHotkey);
        Assert.Equal(string.Empty, service.ActiveHotkey);
        Assert.Empty(platform.Registrations);
    }

    [Fact]
    public void ReplaceHotkey_InvalidGesture_PreservesPreviousRegistration()
    {
        var platform = new FakeHotkeyPlatform();
        var service = new HotkeyService(platform);
        Assert.True(service.ReplaceHotkey("Ctrl+Alt+Space", () => { }).Success);

        var result = service.ReplaceHotkey("Space", () => { });

        Assert.False(result.Success);
        Assert.Equal(HotkeyRegistrationFailure.InvalidGesture, result.Failure);
        Assert.True(result.PreviousHotkeyRestored);
        Assert.Equal("Ctrl+Alt+Space", service.ActiveHotkey);
        Assert.Single(platform.Registrations);
    }

    private sealed class FakeHotkeyPlatform : IHotkeyPlatform
    {
        private int _nextError;

        public Dictionary<int, (uint Modifiers, uint VirtualKey)> Registrations { get; } = new();

        public void FailNextRegistrationWith(int errorCode) => _nextError = errorCode;

        public bool Register(
            IntPtr windowHandle,
            int id,
            uint modifiers,
            uint virtualKey,
            out int errorCode)
        {
            _ = windowHandle;
            if (_nextError != 0)
            {
                errorCode = _nextError;
                _nextError = 0;
                return false;
            }

            Registrations[id] = (modifiers, virtualKey);
            errorCode = 0;
            return true;
        }

        public bool Unregister(IntPtr windowHandle, int id)
        {
            _ = windowHandle;
            Registrations.Remove(id);
            return true;
        }
    }
}
