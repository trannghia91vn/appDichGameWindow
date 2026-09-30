using GameTranslator.App.Overlay;
using GameTranslator.Core;

namespace GameTranslator.App.Translation;

public sealed record TranslationCommandResult(
    TranslationPipelineResult PipelineResult,
    bool UsedOverlayCoordinator);

public sealed class TranslationCommand
{
    private readonly ITranslationPipeline translationPipeline;
    private readonly OverlayTranslationCoordinator overlayCoordinator;
    private readonly Func<ScreenRegion?> regionProvider;
    private readonly Func<string?> modelProvider;
    private readonly Func<bool> overlayVisibleProvider;
    private int isRunning;

    public TranslationCommand(
        ITranslationPipeline translationPipeline,
        OverlayTranslationCoordinator overlayCoordinator,
        Func<ScreenRegion?> regionProvider,
        Func<string?> modelProvider,
        Func<bool> overlayVisibleProvider)
    {
        this.translationPipeline = translationPipeline;
        this.overlayCoordinator = overlayCoordinator;
        this.regionProvider = regionProvider;
        this.modelProvider = modelProvider;
        this.overlayVisibleProvider = overlayVisibleProvider;
    }

    public bool TryExecuteAsync(
        CancellationToken cancellationToken,
        out Task<TranslationCommandResult>? execution)
    {
        if (Interlocked.CompareExchange(ref isRunning, 1, 0) != 0)
        {
            execution = null;
            return false;
        }

        execution = ExecuteCoreAsync(cancellationToken);
        return true;
    }

    private async Task<TranslationCommandResult> ExecuteCoreAsync(
        CancellationToken cancellationToken)
    {
        try
        {
            var region = regionProvider()
                ?? throw new TranslationCommandException("Vui lòng chọn vùng trước.");
            var model = modelProvider();
            if (string.IsNullOrWhiteSpace(model))
            {
                throw new TranslationCommandException("Vui lòng chọn một model Ollama.");
            }

            var useOverlay = overlayVisibleProvider();
            var result = useOverlay
                ? await overlayCoordinator.ExecuteAsync(region, model, cancellationToken)
                    .ConfigureAwait(false)
                : await translationPipeline.TranslateAsync(region, model, cancellationToken)
                    .ConfigureAwait(false);
            return new TranslationCommandResult(result, useOverlay);
        }
        finally
        {
            Volatile.Write(ref isRunning, 0);
        }
    }
}

public sealed class TranslationCommandException : InvalidOperationException
{
    public TranslationCommandException(string userMessage)
        : base(userMessage)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
