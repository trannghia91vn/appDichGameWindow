using System.Windows.Input;
using GameTranslator.App.Hotkeys;
using Xunit;

namespace GameTranslator.Tests;

public sealed class HotkeyRegistrationManagerTests
{
    [Fact]
    public void SuccessfulChangeUnregistersOldRegistersNewAndPersists()
    {
        var registrar = new FakeRegistrar();
        var manager = new HotkeyRegistrationManager(registrar);
        Assert.Equal(
            HotkeyRegistrationResult.Success,
            manager.RegisterInitial(HotkeyGesture.Default));
        registrar.Calls.Clear();
        HotkeyGesture? persisted = null;
        var replacement = new HotkeyGesture(Key.T, Ctrl: true);

        var result = manager.ChangeHotkey(replacement, gesture => persisted = gesture);

        Assert.Equal(HotkeyRegistrationResult.Success, result);
        Assert.Equal(["unregister", "register:Ctrl + T"], registrar.Calls);
        Assert.Equal(replacement, manager.CurrentGesture);
        Assert.Equal(replacement, persisted);
    }

    [Fact]
    public void FailedReplacementRestoresOldAndDoesNotPersist()
    {
        var registrar = new FakeRegistrar();
        var manager = new HotkeyRegistrationManager(registrar);
        manager.RegisterInitial(HotkeyGesture.Default);
        registrar.Calls.Clear();
        registrar.Results.Enqueue(HotkeyRegistrationResult.AlreadyInUse);
        registrar.Results.Enqueue(HotkeyRegistrationResult.Success);
        var persistCount = 0;

        var result = manager.ChangeHotkey(
            new HotkeyGesture(Key.T, Ctrl: true),
            _ => persistCount++);

        Assert.Equal(HotkeyRegistrationResult.AlreadyInUse, result);
        Assert.Equal(
            ["unregister", "register:Ctrl + T", "register:F8"],
            registrar.Calls);
        Assert.Equal(HotkeyGesture.Default, manager.CurrentGesture);
        Assert.Equal(0, persistCount);
    }

    [Fact]
    public void FailedUnregisterKeepsPreviousHotkeyState()
    {
        var registrar = new FakeRegistrar();
        var manager = new HotkeyRegistrationManager(registrar);
        manager.RegisterInitial(HotkeyGesture.Default);
        registrar.Calls.Clear();
        registrar.UnregisterSucceeds = false;
        var persistCount = 0;

        var result = manager.ChangeHotkey(
            new HotkeyGesture(Key.T, Ctrl: true),
            _ => persistCount++);

        Assert.Equal(HotkeyRegistrationResult.Failed, result);
        Assert.Equal(["unregister"], registrar.Calls);
        Assert.Equal(HotkeyGesture.Default, manager.CurrentGesture);
        Assert.Equal(0, persistCount);
    }

    private sealed class FakeRegistrar : IGlobalHotkeyRegistrar
    {
        public Queue<HotkeyRegistrationResult> Results { get; } = [];

        public List<string> Calls { get; } = [];

        public bool UnregisterSucceeds { get; set; } = true;

        public HotkeyRegistrationResult Register(HotkeyGesture gesture)
        {
            Calls.Add($"register:{gesture.ToDisplayString()}");
            return Results.Count == 0
                ? HotkeyRegistrationResult.Success
                : Results.Dequeue();
        }

        public bool Unregister()
        {
            Calls.Add("unregister");
            return UnregisterSucceeds;
        }
    }
}
