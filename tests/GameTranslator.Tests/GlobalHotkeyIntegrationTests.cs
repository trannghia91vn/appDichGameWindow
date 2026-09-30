using System.Threading;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using GameTranslator.App.Hotkeys;
using Xunit;
using Xunit.Abstractions;

namespace GameTranslator.Tests;

public sealed class GlobalHotkeyIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task RegisterConflictChangeAndReleaseWorkWithRealWin32Api()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GAMETRANSLATOR_HOTKEY_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var completion = new TaskCompletionSource<SmokeResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunSmoke(completion))
        {
            IsBackground = true,
            Name = "GameTranslator global hotkey smoke test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(15));
        thread.Join(TimeSpan.FromSeconds(3));

        output.WriteLine(
            "initial={0}; conflict={1}; change={2}; released F8={3}; persisted={4}; events={5}; focus unchanged={6}",
            result.Initial,
            result.Conflict,
            result.Change,
            result.ReleasedF8,
            result.Persisted?.ToDisplayString(),
            result.HotkeyEventCount,
            result.ForegroundUnchanged);
        Assert.Equal(HotkeyRegistrationResult.Success, result.Initial);
        Assert.Equal(HotkeyRegistrationResult.AlreadyInUse, result.Conflict);
        Assert.Equal(HotkeyRegistrationResult.Success, result.Change);
        Assert.Equal(HotkeyRegistrationResult.Success, result.ReleasedF8);
        Assert.Equal(
            new HotkeyGesture(System.Windows.Input.Key.T, Ctrl: true, Shift: true),
            result.Persisted);
        Assert.Equal(1, result.HotkeyEventCount);
        Assert.True(result.ForegroundUnchanged);
    }

    private static void RunSmoke(TaskCompletionSource<SmokeResult> completion)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.InvokeAsync(() =>
        {
            Window? firstWindow = null;
            Window? secondWindow = null;
            GlobalHotkeyService? firstService = null;
            GlobalHotkeyService? secondService = null;

            try
            {
                firstWindow = CreateHiddenOwner();
                secondWindow = CreateHiddenOwner();
                firstWindow.Show();
                secondWindow.Show();

                firstService = new GlobalHotkeyService();
                secondService = new GlobalHotkeyService();
                firstService.Attach(firstWindow);
                secondService.Attach(secondWindow);
                var hotkeyEventCount = 0;
                firstService.HotkeyPressed += (_, _) => hotkeyEventCount++;
                var firstManager = new HotkeyRegistrationManager(firstService);
                var secondManager = new HotkeyRegistrationManager(secondService);

                var initial = firstManager.RegisterInitial(HotkeyGesture.Default);
                var conflict = secondManager.RegisterInitial(HotkeyGesture.Default);
                var foregroundBefore = GetForegroundWindow();
                _ = SendMessage(
                    new WindowInteropHelper(firstWindow).Handle,
                    0x0312,
                    new nint(0x4754),
                    nint.Zero);
                var foregroundUnchanged = foregroundBefore == GetForegroundWindow();
                HotkeyGesture? persisted = null;
                var replacement = new HotkeyGesture(
                    System.Windows.Input.Key.T,
                    Ctrl: true,
                    Shift: true);
                var change = firstManager.ChangeHotkey(
                    replacement,
                    gesture => persisted = gesture);
                var releasedF8 = secondManager.RegisterInitial(HotkeyGesture.Default);

                completion.SetResult(new SmokeResult(
                    initial,
                    conflict,
                    change,
                    releasedF8,
                    persisted,
                    hotkeyEventCount,
                    foregroundUnchanged));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                firstService?.Dispose();
                secondService?.Dispose();
                firstWindow?.Close();
                secondWindow?.Close();
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        });

        Dispatcher.Run();
    }

    private static Window CreateHiddenOwner() =>
        new()
        {
            Width = 1,
            Height = 1,
            Left = -10000,
            Top = -10000,
            ShowActivated = false,
            ShowInTaskbar = false,
            WindowStyle = WindowStyle.None
        };

    private sealed record SmokeResult(
        HotkeyRegistrationResult Initial,
        HotkeyRegistrationResult Conflict,
        HotkeyRegistrationResult Change,
        HotkeyRegistrationResult ReleasedF8,
        HotkeyGesture? Persisted,
        int HotkeyEventCount,
        bool ForegroundUnchanged);

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    [DllImport("user32.dll")]
    private static extern nint GetForegroundWindow();
}
