using System.Windows.Input;
using UniDesk.Helpers;
using UniDesk.Models;

namespace UniDesk.Tests;

public class HotkeyGestureParserTests
{
    [Theory]
    [InlineData("ctrl+alt+space", "Ctrl+Alt+Space", 0x0003u, 0x20u)]
    [InlineData("Win+Shift+F12", "Shift+Win+F12", 0x000Cu, 0x7Bu)]
    [InlineData("alt+k", "Alt+K", 0x0001u, 0x4Bu)]
    public void TryParse_ValidGesture_Normalizes(
        string input,
        string display,
        uint modifiers,
        uint key)
    {
        Assert.True(HotkeyGestureParser.TryParse(input, out var gesture));
        Assert.Equal(new HotkeyGesture(display, modifiers, key), gesture);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Space")]
    [InlineData("Ctrl+Alt")]
    [InlineData("Ctrl+Mouse1")]
    [InlineData("Ctrl+A+B")]
    public void TryParse_InvalidGesture_ReturnsFalse(string input) =>
        Assert.False(HotkeyGestureParser.TryParse(input, out _));

    [Fact]
    public void TryCreate_SupportedKeyAndModifiers_ReturnsCanonicalGesture()
    {
        Assert.True(HotkeyGestureParser.TryCreate(
            Key.K,
            ModifierKeys.Control | ModifierKeys.Shift,
            out var gesture));

        Assert.Equal(new HotkeyGesture("Ctrl+Shift+K", 0x0006u, 0x4Bu), gesture);
    }

    [Theory]
    [InlineData(Key.K, ModifierKeys.None)]
    [InlineData(Key.LeftCtrl, ModifierKeys.Control)]
    [InlineData(Key.Escape, ModifierKeys.Control)]
    public void TryCreate_UnsupportedCapture_ReturnsFalse(Key key, ModifierKeys modifiers) =>
        Assert.False(HotkeyGestureParser.TryCreate(key, modifiers, out _));
}
