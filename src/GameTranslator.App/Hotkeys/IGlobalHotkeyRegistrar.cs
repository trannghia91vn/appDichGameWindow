namespace GameTranslator.App.Hotkeys;

public interface IGlobalHotkeyRegistrar
{
    HotkeyRegistrationResult Register(HotkeyGesture gesture);

    bool Unregister();
}
