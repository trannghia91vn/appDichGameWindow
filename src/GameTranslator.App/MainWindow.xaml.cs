using System.Diagnostics;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using GameTranslator.App.Capture;
using GameTranslator.App.Hotkeys;
using GameTranslator.App.Ocr;
using GameTranslator.App.Overlay;
using GameTranslator.App.RegionSelection;
using GameTranslator.App.Settings;
using GameTranslator.App.Translation;
using GameTranslator.App.ViewModels;
using GameTranslator.Core;

namespace GameTranslator.App;

public partial class MainWindow : Window
{
    private readonly MainWindowViewModel viewModel = new();
    private readonly IRegionSelectionService regionSelectionService;
    private readonly CaptureSession captureSession;
    private readonly TranslationPipeline translationPipeline;
    private readonly RapidOcrService ocrService;
    private readonly OllamaTranslationService translationService;
    private readonly JsonSettingsStore settingsStore = new();
    private readonly AppSettings settings;
    private readonly HttpClient httpClient;
    private readonly TranslationOverlayWindow overlayWindow;
    private readonly OverlayTranslationCoordinator overlayCoordinator;
    private readonly RealtimeOverlayCoordinator realtimeOverlayCoordinator;
    private readonly TranslationCommand translationCommand;
    private readonly RealtimeTranslationController realtimeTranslationController;
    private readonly GlobalHotkeyService globalHotkeyService;
    private readonly HotkeyRegistrationManager hotkeyRegistrationManager;
    private readonly CancellationTokenSource lifetimeCancellation = new();
    private bool isCapturingHotkey;

    public MainWindow()
    {
        settings = settingsStore.Load();
        viewModel.OllamaBaseUrl = settings.OllamaBaseUrl;
        viewModel.SelectedModel = settings.SelectedModel;
        viewModel.OverlayFontSize = settings.OverlayFontSize;
        viewModel.OverlayBackgroundOpacity = settings.OverlayBackgroundOpacity;
        viewModel.OverlayClickThrough = settings.OverlayClickThrough;
        viewModel.HotkeyDisplayText = settings.GetTranslationHotkey().ToDisplayString();

        var screenCaptureService = new WindowsScreenCaptureService();
        ocrService = new RapidOcrService();
        httpClient = new HttpClient { Timeout = OllamaOptions.RequestTimeout };
        translationService = new OllamaTranslationService(
            httpClient,
            () => viewModel.OllamaBaseUrl);
        regionSelectionService = new RegionSelectionService();
        captureSession = new CaptureSession(screenCaptureService);
        translationPipeline = new TranslationPipeline(
            screenCaptureService,
            ocrService,
            translationService,
            new ExactTranslationCache());
        overlayWindow = new TranslationOverlayWindow();
        overlayCoordinator = new OverlayTranslationCoordinator(
            translationPipeline,
            overlayWindow,
            GetOverlayErrorMessage);
        realtimeOverlayCoordinator = new RealtimeOverlayCoordinator(
            translationPipeline,
            overlayWindow,
            () => overlayWindow.CaptureExclusionEnabled,
            GetOverlayErrorMessage);
        translationCommand = new TranslationCommand(
            translationPipeline,
            overlayCoordinator,
            () => captureSession.SelectedRegion,
            () => viewModel.SelectedModel,
            () => overlayWindow.IsVisible);
        realtimeTranslationController = new RealtimeTranslationController(
            realtimeOverlayCoordinator.ExecuteAsync);
        globalHotkeyService = new GlobalHotkeyService();
        hotkeyRegistrationManager = new HotkeyRegistrationManager(globalHotkeyService);
        ApplyOverlaySettings();
        overlayWindow.TranslationRequested += OverlayWindow_TranslationRequested;
        overlayWindow.RealtimeModeChanged += OverlayWindow_RealtimeModeChanged;
        overlayWindow.IsVisibleChanged += OverlayWindow_IsVisibleChanged;
        realtimeTranslationController.ResultAvailable += RealtimeTranslationController_ResultAvailable;
        realtimeTranslationController.StepFailed += RealtimeTranslationController_StepFailed;
        globalHotkeyService.HotkeyPressed += GlobalHotkeyService_HotkeyPressed;

        InitializeComponent();
        DataContext = viewModel;
        Loaded += MainWindow_Loaded;
        PreviewKeyDown += MainWindow_PreviewKeyDown;
        Closing += MainWindow_Closing;
        Closed += async (_, _) =>
        {
            lifetimeCancellation.Cancel();
            await realtimeTranslationController.StopAsync();
            hotkeyRegistrationManager.Unregister();
            globalHotkeyService.Dispose();
            overlayWindow.Shutdown();
            ocrService.Dispose();
            httpClient.Dispose();
        };
    }

    private void SelectRegion_Click(object sender, RoutedEventArgs e)
    {
        viewModel.StatusText = "Đang chọn vùng...";

        try
        {
            var selectedRegion = regionSelectionService.SelectRegion(this);
            if (!captureSession.ApplySelection(selectedRegion))
            {
                viewModel.StatusText = "Đã hủy chọn vùng. Vùng trước đó được giữ nguyên.";
                return;
            }

            var region = captureSession.SelectedRegion!.Value;
            viewModel.SelectedRegionText =
                $"X: {region.X}   Y: {region.Y}   W: {region.Width}   H: {region.Height}";
            viewModel.StatusText = "Sẵn sàng.";
        }
        catch (Exception ex)
        {
            Trace.TraceError("Region selection failed: {0}", ex);
            viewModel.StatusText = "Không thể chọn vùng màn hình.";
        }
    }

    private async void Capture_Click(object sender, RoutedEventArgs e)
    {
        if (captureSession.SelectedRegion is null)
        {
            viewModel.StatusText = "Vui lòng chọn vùng trước.";
            return;
        }

        SetOperationControlsEnabled(false);
        viewModel.StatusText = "Đang chụp...";

        try
        {
            var capturedImage = await captureSession.CaptureOnceAsync(lifetimeCancellation.Token);
            viewModel.PreviewImage = CreatePreviewImage(capturedImage.PngBytes);
            viewModel.TimingText =
                $"Capture: {capturedImage.CaptureDuration.TotalMilliseconds:F0} ms   " +
                "OCR: -   Translation: -   Total: -   Cache: -";
            viewModel.StatusText = "Đã chụp vùng màn hình.";
        }
        catch (CaptureAlreadyInProgressException)
        {
            viewModel.StatusText = "Một lần chụp khác đang chạy.";
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusText = "Đã hủy chụp màn hình.";
        }
        catch (Exception ex)
        {
            Trace.TraceError("Screen capture failed: {0}", ex);
            viewModel.StatusText = "Không thể chụp vùng đã chọn. Hãy chọn lại vùng và thử lại.";
        }
        finally
        {
            SetOperationControlsEnabled(true);
        }
    }

    private async void Translate_Click(object sender, RoutedEventArgs e) =>
        await ExecuteTranslationCommandAsync();

    private async void OverlayWindow_TranslationRequested(object? sender, EventArgs e) =>
        await ExecuteTranslationCommandAsync();

    private async void GlobalHotkeyService_HotkeyPressed(object? sender, EventArgs e)
    {
        if (isCapturingHotkey)
        {
            return;
        }

        if (realtimeTranslationController.IsRunning)
        {
            return;
        }

        if (!overlayWindow.IsVisible)
        {
            overlayWindow.Show();
        }

        await ExecuteTranslationCommandAsync();
    }

    private async Task ExecuteTranslationCommandAsync()
    {
        if (realtimeTranslationController.IsRunning)
        {
            viewModel.StatusText = "Tắt Realtime trước khi dịch thủ công.";
            return;
        }

        if (!translationCommand.TryExecuteAsync(
                lifetimeCancellation.Token,
                out var execution))
        {
            return;
        }

        SetOperationControlsEnabled(false);
        viewModel.StatusText = "Đang đọc và dịch...";
        viewModel.DiagnosticsText = string.Empty;

        try
        {
            var result = await execution!;
            ApplyTranslationResult(
                result.PipelineResult,
                updateOverlay: !result.UsedOverlayCoordinator);
        }
        catch (TranslationCommandException ex)
        {
            viewModel.StatusText = ex.UserMessage;
            if (overlayWindow.IsVisible)
            {
                overlayWindow.ShowError(ex.UserMessage);
            }
        }
        catch (TranslationPipelineBusyException)
        {
            viewModel.StatusText = "Một lần dịch khác đang chạy.";
        }
        catch (OcrInitializationException ex)
        {
            Trace.TraceError("OCR initialization failed: {0}", ex);
            viewModel.StatusText = "Không thể khởi tạo OCR.";
        }
        catch (OllamaException ex)
        {
            Trace.TraceError("Ollama translation failed: {0}", ex);
            viewModel.TranslationText = ex.UserMessage;
            viewModel.StatusText = ex.UserMessage;
        }
        catch (OperationCanceledException)
        {
            viewModel.StatusText = "Đã hủy thao tác dịch.";
        }
        catch (Exception ex)
        {
            Trace.TraceError("Translation operation failed: {0}", ex);
            viewModel.StatusText = "Không thể hoàn tất OCR và dịch.";
        }
        finally
        {
            SetOperationControlsEnabled(true);
        }
    }

    private async void OverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (overlayWindow.IsVisible)
        {
            CaptureOverlaySettings();
            overlayWindow.Hide();
            await SaveSettingsAsync();
        }
        else
        {
            overlayWindow.Show();
        }
    }

    private void OverlayAppearance_Changed(
        object sender,
        RoutedPropertyChangedEventArgs<double> e)
    {
        settings.OverlayFontSize = viewModel.OverlayFontSize;
        settings.OverlayBackgroundOpacity = viewModel.OverlayBackgroundOpacity;
        overlayWindow.ApplyVisualSettings(
            viewModel.OverlayFontSize,
            viewModel.OverlayBackgroundOpacity);
    }

    private async void OverlayClickThrough_Changed(object sender, RoutedEventArgs e)
    {
        settings.OverlayClickThrough = viewModel.OverlayClickThrough;
        overlayWindow.SetClickThrough(viewModel.OverlayClickThrough);
        viewModel.StatusText = viewModel.OverlayClickThrough
            ? "Click-through đang bật. Dùng MainWindow để tắt hoặc chạy DỊCH."
            : "Click-through đã tắt.";
        await SaveSettingsAsync();
    }

    private async void OverlayWindow_IsVisibleChanged(
        object sender,
        DependencyPropertyChangedEventArgs e)
    {
        OverlayToggleButton.Content = overlayWindow.IsVisible ? "Ẩn Overlay" : "Hiện Overlay";
        if (!overlayWindow.IsVisible && !overlayWindow.IsHiddenForCapture)
        {
            if (realtimeTranslationController.IsRunning)
            {
                await StopRealtimeAsync(keepOverlayHidden: true);
            }

            CaptureOverlaySettings();
            await SaveSettingsAsync();
        }
    }

    private async void OverlayWindow_RealtimeModeChanged(
        object? sender,
        RealtimeModeChangedEventArgs e)
    {
        if (!e.Enabled)
        {
            await StopRealtimeAsync();
            return;
        }

        var region = captureSession.SelectedRegion;
        if (region is null)
        {
            overlayWindow.SetRealtimeEnabled(false);
            overlayWindow.ShowError("Vui lòng chọn vùng trước.");
            viewModel.StatusText = "Vui lòng chọn vùng trước.";
            return;
        }

        var model = viewModel.SelectedModel;
        if (string.IsNullOrWhiteSpace(model))
        {
            overlayWindow.SetRealtimeEnabled(false);
            overlayWindow.ShowError("Vui lòng chọn một model Ollama.");
            viewModel.StatusText = "Vui lòng chọn một model Ollama.";
            return;
        }

        if (realtimeTranslationController.Start(
                region.Value,
                model,
                lifetimeCancellation.Token))
        {
            SetOperationControlsEnabled(false);
            viewModel.StatusText = "Realtime đang theo dõi vùng đã chọn.";
        }
    }

    private async Task StopRealtimeAsync(bool keepOverlayHidden = false)
    {
        await realtimeTranslationController.StopAsync();
        overlayWindow.SetRealtimeEnabled(false);
        if (keepOverlayHidden && overlayWindow.IsVisible)
        {
            overlayWindow.Hide();
        }

        if (!lifetimeCancellation.IsCancellationRequested)
        {
            SetOperationControlsEnabled(true);
            viewModel.StatusText = "Realtime đã tắt. Có thể dịch thủ công.";
        }
    }

    private void RealtimeTranslationController_ResultAvailable(
        RealtimeTranslationPipelineResult result)
    {
        if (!result.TextChanged || result.PipelineResult.Translation is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            ApplyTranslationResult(result.PipelineResult, updateOverlay: false);
            viewModel.StatusText = "Realtime: đã cập nhật bản dịch.";
        });
    }

    private void RealtimeTranslationController_StepFailed(Exception exception) =>
        Dispatcher.BeginInvoke(() =>
        {
            viewModel.StatusText = GetOverlayErrorMessage(exception);
        });

    private async void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            globalHotkeyService.Attach(this);
            var gesture = settings.GetTranslationHotkey();
            var result = hotkeyRegistrationManager.RegisterInitial(gesture);
            viewModel.HotkeyDisplayText = gesture.ToDisplayString();
            viewModel.HotkeyStatusText = GetHotkeyRegistrationMessage(result, gesture);
        }
        catch (Exception ex)
        {
            Trace.TraceError("Global hotkey initialization failed: {0}", ex);
            viewModel.HotkeyStatusText = "Không thể đăng ký phím tắt.";
        }

        await RefreshModelsAsync();
    }

    private void ChangeHotkey_Click(object sender, RoutedEventArgs e)
    {
        isCapturingHotkey = true;
        ChangeHotkeyButton.Content = "Đang chờ phím...";
        viewModel.HotkeyStatusText = "Nhấn tổ hợp mới. Nhấn Escape để hủy.";
        ChangeHotkeyButton.Focus();
    }

    private void ResetHotkey_Click(object sender, RoutedEventArgs e)
    {
        isCapturingHotkey = false;
        ChangeHotkeyButton.Content = "Đổi phím";
        ApplyHotkeyChange(HotkeyGesture.Default);
    }

    private void MainWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!isCapturingHotkey)
        {
            return;
        }

        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape)
        {
            EndHotkeyCapture("Đã hủy đổi phím tắt.");
            return;
        }

        if (HotkeyGesture.IsModifierKey(key))
        {
            viewModel.HotkeyStatusText = "Giữ phím bổ trợ và nhấn thêm một phím chính.";
            return;
        }

        var modifiers = Keyboard.Modifiers;
        var gesture = new HotkeyGesture(
            key,
            Ctrl: modifiers.HasFlag(ModifierKeys.Control),
            Alt: modifiers.HasFlag(ModifierKeys.Alt),
            Shift: modifiers.HasFlag(ModifierKeys.Shift),
            Win: modifiers.HasFlag(ModifierKeys.Windows));
        var validationError = gesture.GetValidationError();
        if (validationError is not null)
        {
            viewModel.HotkeyStatusText = validationError;
            return;
        }

        ApplyHotkeyChange(gesture);
    }

    private void ApplyHotkeyChange(HotkeyGesture gesture)
    {
        try
        {
            var result = hotkeyRegistrationManager.ChangeHotkey(
                gesture,
                settings.SetTranslationHotkey);
            if (result == HotkeyRegistrationResult.Success)
            {
                settingsStore.Save(settings);
                viewModel.HotkeyDisplayText = gesture.ToDisplayString();
                EndHotkeyCapture($"Đã đăng ký: {gesture.ToDisplayString()}.");
                return;
            }

            viewModel.HotkeyStatusText = GetHotkeyRegistrationMessage(result, gesture);
            if (result == HotkeyRegistrationResult.Invalid)
            {
                isCapturingHotkey = true;
            }
        }
        catch (Exception ex)
        {
            Trace.TraceError("Could not change global hotkey: {0}", ex);
            viewModel.HotkeyStatusText = "Không thể đăng ký phím tắt.";
        }
    }

    private void EndHotkeyCapture(string statusMessage)
    {
        isCapturingHotkey = false;
        ChangeHotkeyButton.Content = "Đổi phím";
        viewModel.HotkeyStatusText = statusMessage;
    }

    private static string GetHotkeyRegistrationMessage(
        HotkeyRegistrationResult result,
        HotkeyGesture gesture) =>
        result switch
        {
            HotkeyRegistrationResult.Success => $"Đã đăng ký: {gesture.ToDisplayString()}.",
            HotkeyRegistrationResult.AlreadyInUse =>
                "Phím tắt này đang được ứng dụng khác sử dụng. Vui lòng chọn phím khác.",
            HotkeyRegistrationResult.Invalid =>
                gesture.GetValidationError() ?? "Phím tắt không hợp lệ.",
            _ => "Không thể đăng ký phím tắt."
        };

    private async void RefreshModels_Click(object sender, RoutedEventArgs e) =>
        await RefreshModelsAsync();

    private async void OllamaBaseUrl_LostFocus(object sender, RoutedEventArgs e)
    {
        settings.OllamaBaseUrl = viewModel.OllamaBaseUrl.Trim();
        viewModel.OllamaBaseUrl = settings.OllamaBaseUrl;
        await SaveSettingsAsync();
        await RefreshModelsAsync();
    }

    private async void Model_SelectionChanged(
        object sender,
        System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(viewModel.SelectedModel))
        {
            return;
        }

        settings.SelectedModel = viewModel.SelectedModel;
        await SaveSettingsAsync();
    }

    private async Task RefreshModelsAsync()
    {
        RefreshModelsButton.IsEnabled = false;
        viewModel.OllamaStatusText = "Ollama: Đang kiểm tra...";
        var desiredModel = settings.SelectedModel;

        try
        {
            var models = await translationService.GetInstalledModelsAsync(lifetimeCancellation.Token);
            viewModel.AvailableModels.Clear();
            foreach (var model in models)
            {
                viewModel.AvailableModels.Add(model);
            }

            viewModel.SelectedModel = models.Contains(desiredModel, StringComparer.Ordinal)
                ? desiredModel
                : models.Contains(OllamaOptions.PreferredModel, StringComparer.Ordinal)
                    ? OllamaOptions.PreferredModel
                    : null;

            viewModel.OllamaStatusText = models.Count == 0
                ? "Ollama: Đã kết nối, chưa có model"
                : "Ollama: Đã kết nối";
            viewModel.StatusText = models.Count == 0
                ? "Hãy cài ít nhất một model Ollama rồi bấm Làm mới."
                : viewModel.SelectedModel is null
                    ? "Hãy chọn một model Ollama đã cài."
                    : "Sẵn sàng.";
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
        catch (OllamaException ex)
        {
            Trace.TraceWarning("Ollama model discovery failed: {0}", ex);
            viewModel.AvailableModels.Clear();
            viewModel.SelectedModel = null;
            viewModel.OllamaStatusText = "Ollama: Không kết nối được";
            viewModel.StatusText = ex.UserMessage;
        }
        finally
        {
            if (!lifetimeCancellation.IsCancellationRequested)
            {
                RefreshModelsButton.IsEnabled = true;
            }
        }
    }

    private async Task SaveSettingsAsync()
    {
        try
        {
            await settingsStore.SaveAsync(settings, lifetimeCancellation.Token);
        }
        catch (OperationCanceledException)
        {
            // The app is closing.
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not save settings: {0}", ex);
            viewModel.StatusText = "Không thể lưu cài đặt Ollama.";
        }
    }

    private void ApplyOverlaySettings()
    {
        var virtualDesktop = new DesktopBounds(
            SystemParameters.VirtualScreenLeft,
            SystemParameters.VirtualScreenTop,
            SystemParameters.VirtualScreenWidth,
            SystemParameters.VirtualScreenHeight);
        var workArea = SystemParameters.WorkArea;
        var fallbackDesktop = new DesktopBounds(
            workArea.Left,
            workArea.Top,
            workArea.Width,
            workArea.Height);
        var requested = new OverlayPlacement(
            settings.OverlayLeft ?? double.NaN,
            settings.OverlayTop ?? double.NaN,
            settings.OverlayWidth,
            settings.OverlayHeight);
        var placement = OverlayPlacementValidator.EnsureVisible(
            requested,
            virtualDesktop,
            fallbackDesktop);

        overlayWindow.WindowStartupLocation = WindowStartupLocation.Manual;
        overlayWindow.Left = placement.Left;
        overlayWindow.Top = placement.Top;
        overlayWindow.Width = placement.Width;
        overlayWindow.Height = placement.Height;
        overlayWindow.ApplyVisualSettings(
            settings.OverlayFontSize,
            settings.OverlayBackgroundOpacity);
        overlayWindow.SetClickThrough(settings.OverlayClickThrough);
    }

    private void CaptureOverlaySettings()
    {
        var placement = overlayWindow.GetPlacement();
        settings.OverlayLeft = placement.Left;
        settings.OverlayTop = placement.Top;
        settings.OverlayWidth = placement.Width;
        settings.OverlayHeight = placement.Height;
        settings.OverlayFontSize = viewModel.OverlayFontSize;
        settings.OverlayBackgroundOpacity = viewModel.OverlayBackgroundOpacity;
        settings.OverlayClickThrough = viewModel.OverlayClickThrough;
    }

    private void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        CaptureOverlaySettings();
        try
        {
            settingsStore.Save(settings);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not save settings while closing: {0}", ex);
        }
    }

    private void ApplyTranslationResult(
        TranslationPipelineResult result,
        bool updateOverlay)
    {
        viewModel.PreviewImage = CreatePreviewImage(result.CapturedImage.PngBytes);
        viewModel.OcrText = result.Ocr.HasText
            ? result.Ocr.Text
            : "Không nhận diện được chữ tiếng Anh.";

        if (result.Translation is null)
        {
            const string noTextMessage = "Không nhận diện được chữ.";
            viewModel.TranslationText = "Không có nội dung để dịch.";
            viewModel.TimingText =
                $"Capture: {result.CapturedImage.CaptureDuration.TotalMilliseconds:F0} ms   " +
                $"OCR: {result.Ocr.ProcessingDuration.TotalMilliseconds:F0} ms   " +
                $"Translation: -   Total: {result.TotalDuration.TotalMilliseconds:F0} ms   Cache: -";
            viewModel.StatusText = "Không nhận diện được chữ tiếng Anh.";
            if (updateOverlay)
            {
                overlayWindow.UpdateTranslationWithoutShowing(noTextMessage);
            }

            return;
        }

        var translation = result.Translation;
        viewModel.TranslationText = translation.TranslatedText;
        viewModel.TimingText =
            $"Capture: {result.CapturedImage.CaptureDuration.TotalMilliseconds:F0} ms   " +
            $"OCR: {result.Ocr.ProcessingDuration.TotalMilliseconds:F0} ms   " +
            $"Translation: {translation.Duration.TotalMilliseconds:F0} ms   " +
            $"Total: {result.TotalDuration.TotalMilliseconds:F0} ms   " +
            $"Cache: {(translation.FromCache ? "HIT" : "MISS")}";
        viewModel.DiagnosticsText = FormatDiagnostics(translation);
        viewModel.StatusText = "Đã dịch.";
        if (updateOverlay)
        {
            overlayWindow.UpdateTranslationWithoutShowing(translation.TranslatedText);
        }
    }

    private static string GetOverlayErrorMessage(Exception exception) =>
        exception switch
        {
            OllamaException ollama => ollama.UserMessage,
            TranslationPipelineBusyException => "Một lần dịch khác đang chạy.",
            OcrInitializationException => "Không thể khởi tạo OCR.",
            OperationCanceledException => "Đã hủy thao tác dịch.",
            _ => "Không thể dịch nội dung này."
        };

    private void SetOperationControlsEnabled(bool isEnabled)
    {
        SelectRegionButton.IsEnabled = isEnabled;
        CaptureButton.IsEnabled = isEnabled;
        TranslateButton.IsEnabled = isEnabled;
        ModelComboBox.IsEnabled = isEnabled;
        RefreshModelsButton.IsEnabled = isEnabled;
        ChangeHotkeyButton.IsEnabled = isEnabled;
        ResetHotkeyButton.IsEnabled = isEnabled;
    }

    private static string FormatDiagnostics(TranslationResult result)
    {
        if (result.FromCache)
        {
            return "Bản dịch lấy từ exact cache trong bộ nhớ.";
        }

        var parts = new List<string>();
        if (result.LoadDuration is not null)
        {
            parts.Add($"Load: {result.LoadDuration.Value.TotalMilliseconds:F0} ms");
        }

        if (result.EvalDuration is not null)
        {
            parts.Add($"Eval: {result.EvalDuration.Value.TotalMilliseconds:F0} ms");
        }

        if (result.PromptEvalCount is not null)
        {
            parts.Add($"Prompt tokens: {result.PromptEvalCount}");
        }

        if (result.EvalCount is not null)
        {
            parts.Add($"Output tokens: {result.EvalCount}");
        }

        return string.Join("   ", parts);
    }

    private static BitmapImage CreatePreviewImage(byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }
}
