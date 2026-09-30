namespace GameTranslator.App.Overlay;

public interface ITranslationOverlayView
{
    void SetTranslationEnabled(bool isEnabled);

    void ShowProcessing(string message);

    Task HideForCaptureAsync(CancellationToken cancellationToken);

    Task ShowAfterCaptureAsync(string message, CancellationToken cancellationToken);

    void ShowTranslation(string translatedText);

    void ShowError(string message);
}
