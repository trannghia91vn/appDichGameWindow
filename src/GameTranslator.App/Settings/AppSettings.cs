using GameTranslator.App.Translation;
using GameTranslator.App.Hotkeys;
using System.Windows.Input;

namespace GameTranslator.App.Settings;

public sealed class AppSettings
{
    public string OllamaBaseUrl { get; set; } = OllamaOptions.DefaultBaseUrl;

    public string SelectedModel { get; set; } = OllamaOptions.PreferredModel;

    public double OverlayFontSize { get; set; } = 23;

    public double OverlayBackgroundOpacity { get; set; } = 0.9;

    public double? OverlayLeft { get; set; }

    public double? OverlayTop { get; set; }

    public double OverlayWidth { get; set; } = 520;

    public double OverlayHeight { get; set; } = 210;

    public bool OverlayClickThrough { get; set; }

    public TranslationHotkeySettings TranslationHotkey { get; set; } = new();

    public HotkeyGesture GetTranslationHotkey()
    {
        var configured = TranslationHotkey?.ToGesture();
        return configured?.IsValid == true ? configured : HotkeyGesture.Default;
    }

    public void SetTranslationHotkey(HotkeyGesture gesture) =>
        TranslationHotkey = TranslationHotkeySettings.FromGesture(gesture);

    public void ResetTranslationHotkey() => SetTranslationHotkey(HotkeyGesture.Default);
}

public sealed class TranslationHotkeySettings
{
    public string Key { get; set; } = "F8";

    public bool Ctrl { get; set; }

    public bool Alt { get; set; }

    public bool Shift { get; set; }

    public bool Win { get; set; }

    public HotkeyGesture? ToGesture() =>
        Enum.TryParse<Key>(Key, ignoreCase: true, out var key)
            ? new HotkeyGesture(key, Ctrl, Alt, Shift, Win)
            : null;

    public static TranslationHotkeySettings FromGesture(HotkeyGesture gesture) =>
        new()
        {
            Key = gesture.Key.ToString(),
            Ctrl = gesture.Ctrl,
            Alt = gesture.Alt,
            Shift = gesture.Shift,
            Win = gesture.Win
        };
}
