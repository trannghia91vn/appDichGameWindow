namespace GameTranslator.Core;

public sealed record OcrResult(
    string Text,
    TimeSpan ProcessingDuration,
    double? AverageConfidence = null)
{
    public bool HasText => !string.IsNullOrWhiteSpace(Text);
}
