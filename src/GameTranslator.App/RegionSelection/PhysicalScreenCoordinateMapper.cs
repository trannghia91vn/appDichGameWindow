using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using GameTranslator.Core;

namespace GameTranslator.App.RegionSelection;

public sealed class PhysicalScreenCoordinateMapper
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpShowWindow = 0x0040;

    public PhysicalScreenCoordinateMapper()
    {
        VirtualScreenBounds = new ScreenRegion(
            GetSystemMetrics(SmXVirtualScreen),
            GetSystemMetrics(SmYVirtualScreen),
            GetSystemMetrics(SmCxVirtualScreen),
            GetSystemMetrics(SmCyVirtualScreen));
    }

    public ScreenRegion VirtualScreenBounds { get; }

    public PhysicalScreenPoint GetCursorPosition()
    {
        if (!GetCursorPos(out var point))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }

        return new PhysicalScreenPoint(point.X, point.Y);
    }

    public void CoverVirtualDesktop(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        var bounds = VirtualScreenBounds;

        if (!SetWindowPos(
                handle,
                IntPtr.Zero,
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height,
                SwpNoActivate | SwpShowWindow))
        {
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
    }

    public Point ToWindowDip(Window window, PhysicalScreenPoint point)
    {
        var source = (HwndSource?)PresentationSource.FromVisual(window)
            ?? throw new InvalidOperationException("The selector window has not been initialized.");
        var bounds = VirtualScreenBounds;
        var devicePoint = new Point(point.X - bounds.X, point.Y - bounds.Y);

        return source.CompositionTarget.TransformFromDevice.Transform(devicePoint);
    }

    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int index);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetCursorPos(out NativePoint point);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowPos(
        IntPtr windowHandle,
        IntPtr insertAfter,
        int x,
        int y,
        int width,
        int height,
        uint flags);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativePoint
    {
        public int X;
        public int Y;
    }
}

public readonly record struct PhysicalScreenPoint(int X, int Y);
