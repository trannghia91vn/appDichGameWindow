using GameTranslator.App.Overlay;
using GameTranslator.App.Translation;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class TranslationCommandTests
{
    private static readonly ScreenRegion Region = new(0, 0, 320, 100);

    [Fact]
    public async Task ExecuteUsesExistingPipelineExactlyOnce()
    {
        var pipeline = new FakePipeline();
        var command = CreateCommand(pipeline);

        Assert.True(command.TryExecuteAsync(CancellationToken.None, out var execution));
        var result = await execution!;

        Assert.Equal(1, pipeline.CallCount);
        Assert.False(result.UsedOverlayCoordinator);
    }

    [Fact]
    public async Task RapidTriggersProduceOnlyOneExecutionAndNoQueue()
    {
        var pipeline = new BlockingPipeline();
        var command = CreateCommand(pipeline);

        Assert.True(command.TryExecuteAsync(CancellationToken.None, out var first));
        await pipeline.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        Assert.False(command.TryExecuteAsync(CancellationToken.None, out _));
        Assert.False(command.TryExecuteAsync(CancellationToken.None, out _));
        Assert.False(command.TryExecuteAsync(CancellationToken.None, out _));

        pipeline.Release.SetResult();
        await first!;
        Assert.Equal(1, pipeline.CallCount);
    }

    private static TranslationCommand CreateCommand(ITranslationPipeline pipeline)
    {
        var overlay = new OverlayTranslationCoordinator(pipeline, new FakeOverlayView());
        return new TranslationCommand(
            pipeline,
            overlay,
            () => Region,
            () => "translategemma:4b",
            () => false);
    }

    private class FakePipeline : ITranslationPipeline
    {
        public int CallCount { get; protected set; }

        public virtual Task<TranslationPipelineResult> TranslateAsync(
            ScreenRegion? region,
            string model,
            CancellationToken cancellationToken,
            Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
        {
            CallCount++;
            return Task.FromResult(CreateResult(model));
        }
    }

    private sealed class BlockingPipeline : FakePipeline
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async Task<TranslationPipelineResult> TranslateAsync(
            ScreenRegion? region,
            string model,
            CancellationToken cancellationToken,
            Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
        {
            CallCount++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return CreateResult(model);
        }
    }

    private sealed class FakeOverlayView : ITranslationOverlayView
    {
        public void SetTranslationEnabled(bool isEnabled) { }

        public void ShowProcessing(string message) { }

        public Task HideForCaptureAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task ShowAfterCaptureAsync(string message, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public void ShowTranslation(string translatedText) { }

        public void ShowError(string message) { }
    }

    private static TranslationPipelineResult CreateResult(string model)
    {
        var image = new CapturedImage([1], 320, 100, TimeSpan.Zero);
        var ocr = new OcrResult("English", TimeSpan.Zero);
        var translation = new TranslationResult(
            ocr.Text,
            "Tiếng Việt",
            model,
            TimeSpan.Zero);
        return new TranslationPipelineResult(image, ocr, translation, TimeSpan.Zero);
    }
}
