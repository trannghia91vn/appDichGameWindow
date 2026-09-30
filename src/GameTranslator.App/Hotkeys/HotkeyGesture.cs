using System.Windows.Input;

namespace GameTranslator.App.Hotkeys;

public sealed record HotkeyGesture(
    Key Key,
    bool Ctrl = false,
    bool Alt = false,
    bool Shift = false,
    bool Win = false)
{
    public static HotkeyGesture Default { get; } = new(Key.F8);

    public bool IsValid => GetValidationError() is null;

    public string? GetValidationError()
    {
        if (Key == Key.None || IsModifierKey(Key) || KeyInterop.VirtualKeyFromKey(Key) == 0)
        {
            return "Hãy chọn một phím chính, không chỉ phím bổ trợ.";
        }

        if (Alt && Key is Key.F4 or Key.Tab or Key.Escape)
        {
            return "Tổ hợp này được Windows sử dụng. Hãy chọn phím khác.";
        }

        if (Ctrl && Alt && Key == Key.Delete)
        {
            return "Tổ hợp này được Windows sử dụng. Hãy chọn phím khác.";
        }

        if (Win && Key is Key.L or Key.D or Key.R or Key.E or Key.Tab)
        {
            return "Tổ hợp này được Windows sử dụng. Hãy chọn phím khác.";
        }

        return null;
    }

    public string ToDisplayString()
    {
        var parts = new List<string>(5);
        if (Ctrl)
        {
            parts.Add("Ctrl");
        }

        if (Alt)
        {
            parts.Add("Alt");
        }

        if (Shift)
        {
            parts.Add("Shift");
        }

        if (Win)
        {
            parts.Add("Win");
        }

        parts.Add(FormatPrimaryKey(Key));
        return string.Join(" + ", parts);
    }

    public static bool IsModifierKey(Key key) =>
        key is Key.LeftCtrl or Key.RightCtrl or
            Key.LeftAlt or Key.RightAlt or
            Key.LeftShift or Key.RightShift or
            Key.LWin or Key.RWin;

    private static string FormatPrimaryKey(Key key)
    {
        if (key is >= Key.D0 and <= Key.D9)
        {
            return ((int)(key - Key.D0)).ToString();
        }

        return key.ToString();
    }
}
