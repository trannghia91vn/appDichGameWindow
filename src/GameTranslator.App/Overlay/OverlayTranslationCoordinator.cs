using GameTranslator.Core;

namespace GameTranslator.App.Overlay;

public sealed class OverlayTranslationCoordinator
{
    private readonly ITranslationPipeline translationPipeline;
    private readonly ITranslationOverlayView overlayView;
    private readonly Func<Exception, string> errorMessageFactory;
    private int isRunning;

    public OverlayTranslationCoordinator(
        ITranslationPipeline translationPipeline,
        ITranslationOverlayView overlayView,
        Func<Exception, string>? errorMessageFactory = null)
    {
        this.translationPipeline = translationPipeline;
        this.overlayView = overlayView;
        this.errorMessageFactory = errorMessageFactory ??
            (_ => "Không thể dịch nội dung này.");
    }

    public async Task<TranslationPipelineResult> ExecuteAsync(
        ScreenRegion? region,
        string model,
        CancellationToken cancellationToken)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            throw new TranslationPipelineBusyException();
        }

        overlayView.SetTranslationEnabled(false);
        overlayView.ShowProcessing("Đang đọc...");

        try
        {
            await overlayView.HideForCaptureAsync(cancellationToken).ConfigureAwait(false);
            var result = await translationPipeline.TranslateAsync(
                    region,
                    model,
                    cancellationToken,
                    (_, token) => overlayView.ShowAfterCaptureAsync("Đang dịch...", token))
                .ConfigureAwait(false);

            if (result.Translation is null)
            {
                overlayView.ShowError("Không nhận diện được chữ.");
            }
            else
            {
                overlayView.ShowTranslation(result.Translation.TranslatedText);
            }

            return result;
        }
        catch (Exception ex)
        {
            overlayView.ShowError(errorMessageFactory(ex));
            throw;
        }
        finally
        {
            overlayView.SetTranslationEnabled(true);
            Volatile.Write(ref isRunning, 0);
        }
    }
}
