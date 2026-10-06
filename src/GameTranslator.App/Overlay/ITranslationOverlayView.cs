namespace GameTranslator.App.Overlay;

public interface ITranslationOverlayView
{
    string DisplayedText { get; }

    void SetTranslationEnabled(bool isEnabled);

    void SetStatus(string message);

    void ShowProcessing(string message);

    Task HideForCaptureAsync(CancellationToken cancellationToken);

    Task ShowAfterCaptureAsync(string message, CancellationToken cancellationToken);

    void ShowTranslation(string translatedText);

    void ShowError(string message);
}
