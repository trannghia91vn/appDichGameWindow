namespace GameTranslator.Core;

public sealed record TranslationPipelineResult(
    CapturedImage CapturedImage,
    OcrResult Ocr,
    TranslationResult? Translation,
    TimeSpan TotalDuration);
