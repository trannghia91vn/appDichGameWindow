namespace GameTranslator.Core;

public interface ITranslationPipeline
{
    Task<TranslationPipelineResult> TranslateAsync(
        ScreenRegion? region,
        string model,
        CancellationToken cancellationToken,
        Func<CapturedImage, CancellationToken, Task>? captureCompleted = null);
}
