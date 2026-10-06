namespace GameTranslator.Core;

public interface IRealtimeTranslationPipeline
{
    Task<RealtimeTranslationPipelineResult> TranslateIfChangedAsync(
        ScreenRegion? region,
        string model,
        string? previousOcrText,
        CancellationToken cancellationToken,
        Func<CapturedImage, CancellationToken, Task>? captureCompleted = null);
}
