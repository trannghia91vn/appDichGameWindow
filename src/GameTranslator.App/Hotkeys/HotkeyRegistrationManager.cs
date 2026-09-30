namespace GameTranslator.App.Hotkeys;

public sealed class HotkeyRegistrationManager
{
    private readonly IGlobalHotkeyRegistrar registrar;

    public HotkeyRegistrationManager(IGlobalHotkeyRegistrar registrar)
    {
        this.registrar = registrar;
    }

    public HotkeyGesture? CurrentGesture { get; private set; }

    public HotkeyRegistrationResult RegisterInitial(HotkeyGesture gesture)
    {
        if (!gesture.IsValid)
        {
            return HotkeyRegistrationResult.Invalid;
        }

        var result = registrar.Register(gesture);
        if (result == HotkeyRegistrationResult.Success)
        {
            CurrentGesture = gesture;
        }

        return result;
    }

    public HotkeyRegistrationResult ChangeHotkey(
        HotkeyGesture gesture,
        Action<HotkeyGesture> persist)
    {
        if (!gesture.IsValid)
        {
            return HotkeyRegistrationResult.Invalid;
        }

        if (gesture == CurrentGesture)
        {
            persist(gesture);
            return HotkeyRegistrationResult.Success;
        }

        var previous = CurrentGesture;
        if (previous is not null && !registrar.Unregister())
        {
            return HotkeyRegistrationResult.Failed;
        }

        var result = registrar.Register(gesture);
        if (result == HotkeyRegistrationResult.Success)
        {
            CurrentGesture = gesture;
            persist(gesture);
            return result;
        }

        if (previous is not null)
        {
            var restoreResult = registrar.Register(previous);
            if (restoreResult != HotkeyRegistrationResult.Success)
            {
                CurrentGesture = null;
                return HotkeyRegistrationResult.Failed;
            }
        }

        return result;
    }

    public void Unregister()
    {
        if (CurrentGesture is null)
        {
            return;
        }

        if (registrar.Unregister())
        {
            CurrentGesture = null;
        }
    }
}
