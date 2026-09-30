namespace GameTranslator.Core;

public interface IOcrService
{
    Task<OcrResult> RecognizeEnglishAsync(CapturedImage image, CancellationToken cancellationToken);
}
