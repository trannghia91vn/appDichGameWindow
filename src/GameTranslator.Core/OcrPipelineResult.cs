namespace GameTranslator.Core;

public sealed record OcrPipelineResult(
    CapturedImage CapturedImage,
    OcrResult Ocr,
    TimeSpan TotalDuration);
