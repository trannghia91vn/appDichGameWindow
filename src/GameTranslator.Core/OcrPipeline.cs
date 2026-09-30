using System.Diagnostics;

namespace GameTranslator.Core;

public sealed class OcrPipeline
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly IOcrService ocrService;
    private readonly SemaphoreSlim executionGate = new(1, 1);

    public OcrPipeline(
        IScreenCaptureService screenCaptureService,
        IOcrService ocrService)
    {
        this.screenCaptureService = screenCaptureService;
        this.ocrService = ocrService;
    }

    public async Task<OcrPipelineResult> RecognizeAsync(
        ScreenRegion? region,
        CancellationToken cancellationToken)
    {
        if (region is null)
        {
            throw new InvalidOperationException("A screen region must be selected before OCR.");
        }

        region.Value.ThrowIfInvalid();

        if (!await executionGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new OcrPipelineBusyException();
        }

        var totalWatch = Stopwatch.StartNew();
        try
        {
            var image = await screenCaptureService
                .CaptureAsync(region.Value, cancellationToken)
                .ConfigureAwait(false);
            image.ThrowIfInvalid();

            cancellationToken.ThrowIfCancellationRequested();
            var rawOcr = await ocrService
                .RecognizeEnglishAsync(image, cancellationToken)
                .ConfigureAwait(false);
            var normalizedOcr = rawOcr with
            {
                Text = OcrTextNormalizer.Normalize(rawOcr.Text)
            };

            totalWatch.Stop();
            return new OcrPipelineResult(image, normalizedOcr, totalWatch.Elapsed);
        }
        finally
        {
            executionGate.Release();
        }
    }
}
