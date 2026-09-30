using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace GameTranslator.App.Overlay;

public partial class TranslationOverlayWindow : Window, ITranslationOverlayView
{
    private const uint WdaExcludeFromCapture = 0x00000011;
    private const int GwlExStyle = -20;
    private const long WsExLayered = 0x00080000L;
    private const long WsExTransparent = 0x00000020L;

    private bool allowClose;
    private bool clickThroughEnabled;
    private bool addedLayeredStyle;
    private nint windowHandle;

    public TranslationOverlayWindow()
    {
        InitializeComponent();
        SourceInitialized += OnSourceInitialized;
        Closing += OnClosing;
    }

    public event EventHandler? TranslationRequested;

    public bool CaptureExclusionEnabled { get; private set; }

    public bool IsHiddenForCapture { get; private set; }

    public TimeSpan LastHideForCaptureDuration { get; private set; }

    public string DisplayedText => Dispatcher.CheckAccess()
        ? TranslationTextBox.Text
        : Dispatcher.Invoke(() => TranslationTextBox.Text);

    public bool IsClickThroughApplied
    {
        get
        {
            if (windowHandle == nint.Zero)
            {
                return false;
            }

            var style = GetWindowLongPtr(windowHandle, GwlExStyle).ToInt64();
            return (style & WsExLayered) != 0 && (style & WsExTransparent) != 0;
        }
    }

    public void ApplyVisualSettings(double fontSize, double backgroundOpacity)
    {
        var safeFontSize = Math.Clamp(fontSize, 16, 40);
        var safeOpacity = Math.Clamp(backgroundOpacity, 0.45, 1.0);
        TranslationTextBox.FontSize = safeFontSize;
        OverlayShell.Background = new SolidColorBrush(Color.FromArgb(
            (byte)Math.Round(safeOpacity * byte.MaxValue),
            23,
            29,
            39));
    }

    public void SetClickThrough(bool enabled)
    {
        clickThroughEnabled = enabled;
        if (windowHandle == nint.Zero)
        {
            return;
        }

        try
        {
            var currentStyle = GetWindowLongPtr(windowHandle, GwlExStyle);
            if (enabled)
            {
                addedLayeredStyle |= (currentStyle.ToInt64() & WsExLayered) == 0;
                var updated = currentStyle.ToInt64() | WsExLayered | WsExTransparent;
                SetWindowLongPtrChecked(windowHandle, GwlExStyle, new nint(updated));
            }
            else
            {
                var updated = currentStyle.ToInt64() & ~WsExTransparent;
                if (addedLayeredStyle)
                {
                    updated &= ~WsExLayered;
                }

                SetWindowLongPtrChecked(windowHandle, GwlExStyle, new nint(updated));
                addedLayeredStyle = false;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not change overlay click-through style: {0}", ex);
        }
    }

    public void SetTranslationEnabled(bool isEnabled) =>
        RunOnUiThread(() => TranslateButton.IsEnabled = isEnabled);

    public void ShowProcessing(string message) =>
        RunOnUiThread(() =>
        {
            TranslationTextBox.Text = message;
            StatusTextBlock.Text = message;
        });

    public async Task HideForCaptureAsync(CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        await Dispatcher.InvokeAsync(
            () =>
            {
                IsHiddenForCapture = true;
                Hide();
            },
            DispatcherPriority.Send,
            cancellationToken);
        await Dispatcher.InvokeAsync(
            static () => { },
            DispatcherPriority.ApplicationIdle,
            cancellationToken);

        if (OperatingSystem.IsWindows())
        {
            var result = DwmFlush();
            if (result != 0)
            {
                Trace.TraceWarning("DwmFlush failed before capture with HRESULT 0x{0:X8}.", result);
            }
        }

        stopwatch.Stop();
        LastHideForCaptureDuration = stopwatch.Elapsed;
        Trace.TraceInformation(
            "Overlay hidden and composition flushed in {0:F1} ms.",
            stopwatch.Elapsed.TotalMilliseconds);
    }

    public async Task ShowAfterCaptureAsync(
        string message,
        CancellationToken cancellationToken)
    {
        await Dispatcher.InvokeAsync(
            () =>
            {
                IsHiddenForCapture = false;
                TranslationTextBox.Text = message;
                StatusTextBlock.Text = message;
                ShowWithoutActivation();
            },
            DispatcherPriority.Send,
            cancellationToken);
    }

    public void ShowTranslation(string translatedText) =>
        RunOnUiThread(() =>
        {
            IsHiddenForCapture = false;
            TranslationTextBox.Text = translatedText;
            StatusTextBlock.Text = "Đã dịch.";
            ShowWithoutActivation();
        });

    public void ShowError(string message) =>
        RunOnUiThread(() =>
        {
            IsHiddenForCapture = false;
            TranslationTextBox.Text = message;
            StatusTextBlock.Text = "Có lỗi.";
            ShowWithoutActivation();
        });

    public void UpdateTranslationWithoutShowing(string translatedText) =>
        RunOnUiThread(() =>
        {
            TranslationTextBox.Text = translatedText;
            StatusTextBlock.Text = "Đã dịch.";
        });

    public OverlayPlacement GetPlacement() =>
        new(
            Left,
            Top,
            ActualWidth > 0 ? ActualWidth : Width,
            ActualHeight > 0 ? ActualHeight : Height);

    public void Shutdown()
    {
        allowClose = true;
        Close();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        windowHandle = new WindowInteropHelper(this).Handle;
        if (SetWindowDisplayAffinity(windowHandle, WdaExcludeFromCapture))
        {
            CaptureExclusionEnabled = true;
            Trace.TraceInformation("Overlay capture exclusion enabled.");
        }
        else
        {
            Trace.TraceWarning(
                "Could not enable overlay capture exclusion. Win32 error: {0}.",
                Marshal.GetLastWin32Error());
        }

        SetClickThrough(clickThroughEnabled);
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || e.OriginalSource is System.Windows.Controls.Button)
        {
            return;
        }

        try
        {
            DragMove();
        }
        catch (InvalidOperationException)
        {
            // The mouse may have been released while WPF was starting the drag.
        }
    }

    private void TranslateButton_Click(object sender, RoutedEventArgs e) =>
        TranslationRequested?.Invoke(this, EventArgs.Empty);

    private void HideButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Hide();

    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ShowWithoutActivation()
    {
        if (!IsVisible)
        {
            Show();
        }
    }

    private void RunOnUiThread(Action action)
    {
        if (Dispatcher.CheckAccess())
        {
            action();
        }
        else
        {
            Dispatcher.Invoke(action);
        }
    }

    private static void SetWindowLongPtrChecked(nint handle, int index, nint value)
    {
        Marshal.SetLastPInvokeError(0);
        var previous = SetWindowLongPtr(handle, index, value);
        var error = Marshal.GetLastWin32Error();
        if (previous == nint.Zero && error != 0)
        {
            throw new Win32Exception(error);
        }
    }

    private static nint GetWindowLongPtr(nint handle, int index) =>
        IntPtr.Size == 8
            ? GetWindowLongPtr64(handle, index)
            : new nint(GetWindowLong32(handle, index));

    private static nint SetWindowLongPtr(nint handle, int index, nint value) =>
        IntPtr.Size == 8
            ? SetWindowLongPtr64(handle, index, value)
            : new nint(SetWindowLong32(handle, index, value.ToInt32()));

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetWindowDisplayAffinity(nint hWnd, uint dwAffinity);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW", SetLastError = true)]
    private static extern nint GetWindowLongPtr64(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr64(nint hWnd, int nIndex, nint dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetWindowLongW", SetLastError = true)]
    private static extern int GetWindowLong32(nint hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongW", SetLastError = true)]
    private static extern int SetWindowLong32(nint hWnd, int nIndex, int dwNewLong);

    [DllImport("dwmapi.dll")]
    private static extern int DwmFlush();
}
