using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

namespace GameTranslator.App.Hotkeys;

public sealed class GlobalHotkeyService : IGlobalHotkeyRegistrar, IDisposable
{
    private const int HotkeyId = 0x4754;
    private const int WmHotkey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModShift = 0x0004;
    private const uint ModWin = 0x0008;
    private const uint ModNoRepeat = 0x4000;
    private const int ErrorHotkeyAlreadyRegistered = 1409;

    private HwndSource? source;
    private nint windowHandle;
    private bool isRegistered;
    private bool isDisposed;

    public event EventHandler? HotkeyPressed;

    public void Attach(Window owner)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (source is not null)
        {
            return;
        }

        windowHandle = new WindowInteropHelper(owner).Handle;
        source = HwndSource.FromHwnd(windowHandle)
            ?? throw new InvalidOperationException("Could not access the MainWindow message source.");
        source.AddHook(WindowProcedure);
    }

    public HotkeyRegistrationResult Register(HotkeyGesture gesture)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        if (!gesture.IsValid)
        {
            return HotkeyRegistrationResult.Invalid;
        }

        if (windowHandle == nint.Zero)
        {
            throw new InvalidOperationException("The global hotkey service has not been attached.");
        }

        var modifiers = ModNoRepeat |
            (gesture.Ctrl ? ModControl : 0) |
            (gesture.Alt ? ModAlt : 0) |
            (gesture.Shift ? ModShift : 0) |
            (gesture.Win ? ModWin : 0);
        var virtualKey = (uint)KeyInterop.VirtualKeyFromKey(gesture.Key);

        if (RegisterHotKey(windowHandle, HotkeyId, modifiers, virtualKey))
        {
            isRegistered = true;
            Trace.TraceInformation("Registered global translation hotkey: {0}.", gesture.ToDisplayString());
            return HotkeyRegistrationResult.Success;
        }

        var error = Marshal.GetLastWin32Error();
        Trace.TraceWarning(
            "RegisterHotKey failed for {0}. Win32 error: {1}.",
            gesture.ToDisplayString(),
            error);
        return error == ErrorHotkeyAlreadyRegistered
            ? HotkeyRegistrationResult.AlreadyInUse
            : HotkeyRegistrationResult.Failed;
    }

    public bool Unregister()
    {
        if (!isRegistered || windowHandle == nint.Zero)
        {
            return true;
        }

        if (!UnregisterHotKey(windowHandle, HotkeyId))
        {
            Trace.TraceWarning(
                "UnregisterHotKey failed. Win32 error: {0}.",
                Marshal.GetLastWin32Error());
            return false;
        }

        isRegistered = false;
        return true;
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        _ = Unregister();
        source?.RemoveHook(WindowProcedure);
        source = null;
        windowHandle = nint.Zero;
        isDisposed = true;
    }

    private nint WindowProcedure(
        nint hwnd,
        int message,
        nint wParam,
        nint lParam,
        ref bool handled)
    {
        if (message == WmHotkey && wParam.ToInt32() == HotkeyId)
        {
            handled = true;
            HotkeyPressed?.Invoke(this, EventArgs.Empty);
        }

        return nint.Zero;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(
        nint hWnd,
        int id,
        uint fsModifiers,
        uint vk);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint hWnd, int id);
}
