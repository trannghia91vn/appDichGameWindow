using System.Windows.Input;
using GameTranslator.App.Hotkeys;
using Xunit;

namespace GameTranslator.Tests;

public sealed class HotkeyGestureTests
{
    [Fact]
    public void DefaultF8FormatsWithoutModifiers()
    {
        Assert.True(HotkeyGesture.Default.IsValid);
        Assert.Equal("F8", HotkeyGesture.Default.ToDisplayString());
    }

    [Fact]
    public void CtrlShiftTFormatsInReadableOrder()
    {
        var gesture = new HotkeyGesture(Key.T, Ctrl: true, Shift: true);

        Assert.True(gesture.IsValid);
        Assert.Equal("Ctrl + Shift + T", gesture.ToDisplayString());
    }

    [Fact]
    public void ModifierOnlyGestureIsInvalid()
    {
        Assert.False(new HotkeyGesture(Key.LeftCtrl).IsValid);
    }

    [Fact]
    public void AltF4IsRejected()
    {
        Assert.False(new HotkeyGesture(Key.F4, Alt: true).IsValid);
    }
}
