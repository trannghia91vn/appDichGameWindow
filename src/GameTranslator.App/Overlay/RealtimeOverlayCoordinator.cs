using GameTranslator.Core;

namespace GameTranslator.App.Overlay;

public sealed class RealtimeOverlayCoordinator
{
    private readonly IRealtimeTranslationPipeline translationPipeline;
    private readonly ITranslationOverlayView overlayView;
    private readonly Func<bool> captureExclusionProvider;
    private readonly Func<Exception, string> errorMessageFactory;

    public RealtimeOverlayCoordinator(
        IRealtimeTranslationPipeline translationPipeline,
        ITranslationOverlayView overlayView,
        Func<bool> captureExclusionProvider,
        Func<Exception, string>? errorMessageFactory = null)
    {
        this.translationPipeline = translationPipeline;
        this.overlayView = overlayView;
        this.captureExclusionProvider = captureExclusionProvider;
        this.errorMessageFactory = errorMessageFactory ??
            (_ => "Không thể dịch nội dung này.");
    }

    public async Task<RealtimeTranslationPipelineResult> ExecuteAsync(
        ScreenRegion region,
        string model,
        string? previousOcrText,
        CancellationToken cancellationToken)
    {
        var displayedText = overlayView.DisplayedText;
        var mustHideForCapture = !captureExclusionProvider();
        var hidden = false;

        overlayView.SetStatus("Realtime: đang nhận diện...");
        try
        {
            if (mustHideForCapture)
            {
                await overlayView.HideForCaptureAsync(cancellationToken).ConfigureAwait(false);
                hidden = true;
            }

            var result = await translationPipeline.TranslateIfChangedAsync(
                    region,
                    model,
                    previousOcrText,
                    cancellationToken,
                    hidden
                        ? async (_, token) =>
                        {
                            await overlayView.ShowAfterCaptureAsync(displayedText, token)
                                .ConfigureAwait(false);
                            hidden = false;
                            overlayView.SetStatus("Realtime: đang nhận diện...");
                        }
                        : null)
                .ConfigureAwait(false);

            if (!result.PipelineResult.Ocr.HasText)
            {
                overlayView.SetStatus("Realtime: chưa nhận diện được chữ.");
            }
            else if (!result.TextChanged)
            {
                overlayView.SetStatus("Realtime: đang theo dõi.");
            }
            else if (result.PipelineResult.Translation is not null)
            {
                overlayView.ShowTranslation(result.PipelineResult.Translation.TranslatedText);
                overlayView.SetStatus("Realtime: đã cập nhật.");
            }

            return result;
        }
        catch (OperationCanceledException)
        {
            if (hidden)
            {
                await overlayView.ShowAfterCaptureAsync(displayedText, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            throw;
        }
        catch (Exception ex)
        {
            if (hidden)
            {
                await overlayView.ShowAfterCaptureAsync(displayedText, CancellationToken.None)
                    .ConfigureAwait(false);
            }

            overlayView.ShowError(errorMessageFactory(ex));
            throw;
        }
    }
}
