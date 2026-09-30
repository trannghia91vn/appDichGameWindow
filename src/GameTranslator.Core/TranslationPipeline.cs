using System.Diagnostics;

namespace GameTranslator.Core;

public sealed class TranslationPipeline : ITranslationPipeline
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
        if (region is null)
        {
            throw new InvalidOperationException("A screen region must be selected before translation.");
        }

        region.Value.ThrowIfInvalid();
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        if (!await executionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new TranslationPipelineBusyException();
        }

        var totalWatch = Stopwatch.StartNew();
        try
        {
            var image = await screenCaptureService
                .CaptureAsync(region.Value, cancellationToken)
                .ConfigureAwait(false);
            image.ThrowIfInvalid();

            if (captureCompleted is not null)
            {
                await captureCompleted(image, cancellationToken).ConfigureAwait(false);
            }

            var rawOcr = await ocrService
                .RecognizeEnglishAsync(image, cancellationToken)
                .ConfigureAwait(false);
            var ocr = rawOcr with { Text = OcrTextNormalizer.Normalize(rawOcr.Text) };

            if (!ocr.HasText)
            {
                totalWatch.Stop();
                return new TranslationPipelineResult(image, ocr, null, totalWatch.Elapsed);
            }

            var translationWatch = Stopwatch.StartNew();
            if (cache.TryGet(ocr.Text, model, out var cachedText))
            {
                translationWatch.Stop();
                totalWatch.Stop();
                var cachedResult = new TranslationResult(
                    ocr.Text,
                    cachedText,
                    model,
                    translationWatch.Elapsed,
                    FromCache: true);
                return new TranslationPipelineResult(image, ocr, cachedResult, totalWatch.Elapsed);
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

            totalWatch.Stop();
            return new TranslationPipelineResult(image, ocr, successfulTranslation, totalWatch.Elapsed);
        }
        finally
        {
            executionGate.Release();
        }
    }

    private static string NormalizeTranslation(string text) =>
        (text ?? string.Empty)
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
}
