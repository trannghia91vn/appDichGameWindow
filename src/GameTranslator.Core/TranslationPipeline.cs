using System.Diagnostics;

namespace GameTranslator.Core;

public sealed class TranslationPipeline : ITranslationPipeline, IRealtimeTranslationPipeline
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly IOcrService ocrService;
    private readonly ITranslationService translationService;
    private readonly ExactTranslationCache cache;
    private readonly SemaphoreSlim executionGate = new(1, 1);

    public TranslationPipeline(
        IScreenCaptureService screenCaptureService,
        IOcrService ocrService,
        ITranslationService translationService,
        ExactTranslationCache cache)
    {
        this.screenCaptureService = screenCaptureService;
        this.ocrService = ocrService;
        this.translationService = translationService;
        this.cache = cache;
    }

    public async Task<TranslationPipelineResult> TranslateAsync(
        ScreenRegion? region,
        string model,
        CancellationToken cancellationToken,
        Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
    {
        ValidateRequest(region, model);
        await EnterAsync(cancellationToken).ConfigureAwait(false);

        var totalWatch = Stopwatch.StartNew();
        try
        {
            var (image, ocr) = await CaptureAndRecognizeAsync(
                    region!.Value,
                    cancellationToken,
                    captureCompleted)
                .ConfigureAwait(false);
            var translation = await TranslateRecognizedTextAsync(
                    ocr,
                    model,
                    cancellationToken)
                .ConfigureAwait(false);

            totalWatch.Stop();
            return new TranslationPipelineResult(image, ocr, translation, totalWatch.Elapsed);
        }
        finally
        {
            executionGate.Release();
        }
    }

    public async Task<RealtimeTranslationPipelineResult> TranslateIfChangedAsync(
        ScreenRegion? region,
        string model,
        string? previousOcrText,
        CancellationToken cancellationToken,
        Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
    {
        ValidateRequest(region, model);
        await EnterAsync(cancellationToken).ConfigureAwait(false);

        var totalWatch = Stopwatch.StartNew();
        try
        {
            var (image, ocr) = await CaptureAndRecognizeAsync(
                    region!.Value,
                    cancellationToken,
                    captureCompleted)
                .ConfigureAwait(false);
            var textChanged = !string.Equals(
                ocr.Text,
                previousOcrText,
                StringComparison.Ordinal);
            var translation = textChanged && ocr.HasText
                ? await TranslateRecognizedTextAsync(ocr, model, cancellationToken)
                    .ConfigureAwait(false)
                : null;

            totalWatch.Stop();
            var pipelineResult = new TranslationPipelineResult(
                image,
                ocr,
                translation,
                totalWatch.Elapsed);
            return new RealtimeTranslationPipelineResult(pipelineResult, textChanged);
        }
        finally
        {
            executionGate.Release();
        }
    }

    private static void ValidateRequest(ScreenRegion? region, string model)
    {
        if (region is null)
        {
            throw new InvalidOperationException("A screen region must be selected before translation.");
        }

        region.Value.ThrowIfInvalid();
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
    }

    private async Task EnterAsync(CancellationToken cancellationToken)
    {
        if (!await executionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new TranslationPipelineBusyException();
        }
    }

    private async Task<(CapturedImage Image, OcrResult Ocr)> CaptureAndRecognizeAsync(
        ScreenRegion region,
        CancellationToken cancellationToken,
        Func<CapturedImage, CancellationToken, Task>? captureCompleted)
    {
        var image = await screenCaptureService
            .CaptureAsync(region, cancellationToken)
            .ConfigureAwait(false);
        image.ThrowIfInvalid();

        if (captureCompleted is not null)
        {
            await captureCompleted(image, cancellationToken).ConfigureAwait(false);
        }

        var rawOcr = await ocrService
            .RecognizeEnglishAsync(image, cancellationToken)
            .ConfigureAwait(false);
        return (image, rawOcr with { Text = OcrTextNormalizer.Normalize(rawOcr.Text) });
    }

    private async Task<TranslationResult?> TranslateRecognizedTextAsync(
        OcrResult ocr,
        string model,
        CancellationToken cancellationToken)
    {
        if (!ocr.HasText)
        {
            return null;
        }

        var translationWatch = Stopwatch.StartNew();
        if (cache.TryGet(ocr.Text, model, out var cachedText))
        {
            translationWatch.Stop();
            return new TranslationResult(
                ocr.Text,
                cachedText,
                model,
                translationWatch.Elapsed,
                FromCache: true);
        }

        var translation = await translationService
            .TranslateEnglishToVietnameseAsync(ocr.Text, model, cancellationToken)
            .ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        var cleanedText = NormalizeTranslation(translation.TranslatedText);
        if (string.IsNullOrWhiteSpace(cleanedText))
        {
            throw new InvalidOperationException("The translation service returned an empty result.");
        }

        var successfulTranslation = translation with
        {
            SourceText = ocr.Text,
            TranslatedText = cleanedText,
            Model = model,
            FromCache = false
        };
        cache.Store(ocr.Text, model, cleanedText);
        return successfulTranslation;
    }

    private static string NormalizeTranslation(string text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
}
