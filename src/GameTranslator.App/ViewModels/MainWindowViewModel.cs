using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using GameTranslator.App.Translation;

namespace GameTranslator.App.ViewModels;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private string statusText = "Sẵn sàng.";
    private string selectedRegionText = "Chưa chọn vùng";
    private string timingText = "Capture: -   OCR: -   Translation: -   Total: -   Cache: -";
    private string ocrText = "Chưa có văn bản nhận diện.";
    private string translationText = "Chưa có bản dịch.";
    private string ollamaStatusText = "Ollama: Đang kiểm tra...";
    private string ollamaBaseUrl = OllamaOptions.DefaultBaseUrl;
    private string? selectedModel;
    private string diagnosticsText = string.Empty;
    private double overlayFontSize = 23;
    private double overlayBackgroundOpacity = 0.9;
    private bool overlayClickThrough;
    private string hotkeyDisplayText = "F8";
    private string hotkeyStatusText = "Phím tắt chưa được đăng ký.";
    private ImageSource? previewImage;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<string> AvailableModels { get; } = [];

    public string VersionText { get; } =
        $"v{typeof(MainWindowViewModel).Assembly.GetName().Version?.ToString(3) ?? "1.1.1"}";

    public string OllamaBaseUrl
    {
        get => ollamaBaseUrl;
        set => SetField(ref ollamaBaseUrl, value);
    }

    public string? SelectedModel
    {
        get => selectedModel;
        set => SetField(ref selectedModel, value);
    }

    public string OllamaStatusText
    {
        get => ollamaStatusText;
        set => SetField(ref ollamaStatusText, value);
    }

    public string SelectedRegionText
    {
        get => selectedRegionText;
        set => SetField(ref selectedRegionText, value);
    }

    public string TimingText
    {
        get => timingText;
        set => SetField(ref timingText, value);
    }

    public string OcrText
    {
        get => ocrText;
        set => SetField(ref ocrText, value);
    }

    public string TranslationText
    {
        get => translationText;
        set => SetField(ref translationText, value);
    }

    public string DiagnosticsText
    {
        get => diagnosticsText;
        set => SetField(ref diagnosticsText, value);
    }

    public double OverlayFontSize
    {
        get => overlayFontSize;
        set => SetField(ref overlayFontSize, value);
    }

    public double OverlayBackgroundOpacity
    {
        get => overlayBackgroundOpacity;
        set => SetField(ref overlayBackgroundOpacity, value);
    }

    public bool OverlayClickThrough
    {
        get => overlayClickThrough;
        set => SetField(ref overlayClickThrough, value);
    }

    public string HotkeyDisplayText
    {
        get => hotkeyDisplayText;
        set => SetField(ref hotkeyDisplayText, value);
    }

    public string HotkeyStatusText
    {
        get => hotkeyStatusText;
        set => SetField(ref hotkeyStatusText, value);
    }

    public ImageSource? PreviewImage
    {
        get => previewImage;
        set => SetField(ref previewImage, value);
    }

    public string StatusText
    {
        get => statusText;
        set => SetField(ref statusText, value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
