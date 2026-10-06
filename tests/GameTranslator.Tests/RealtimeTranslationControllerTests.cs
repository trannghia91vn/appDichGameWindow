using GameTranslator.App.Translation;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class RealtimeTranslationControllerTests
{
    private static readonly ScreenRegion Region = new(0, 0, 320, 100);

    [Fact]
    public async Task LoopIsSequentialAndCarriesPreviousRecognizedText()
    {
        var previousTexts = new List<string?>();
        var active = 0;
        var maximumActive = 0;
        var completed = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new RealtimeTranslationController(
            async (_, model, previousText, cancellationToken) =>
            {
                previousTexts.Add(previousText);
                var currentActive = Interlocked.Increment(ref active);
                maximumActive = Math.Max(maximumActive, currentActive);
                try
                {
                    await Task.Delay(15, cancellationToken);
                    return CreateResult(model, "HELLO", textChanged: previousText is null);
                }
                finally
                {
                    Interlocked.Decrement(ref active);
                }
            },
            TimeSpan.FromMilliseconds(5));
        controller.ResultAvailable += _ =>
        {
            if (previousTexts.Count >= 2)
            {
                completed.TrySetResult();
            }
        };

        Assert.True(controller.Start(Region, "translategemma:4b", CancellationToken.None));
        Assert.False(controller.Start(Region, "translategemma:4b", CancellationToken.None));
        await completed.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await controller.StopAsync();

        Assert.Equal(1, maximumActive);
        Assert.Null(previousTexts[0]);
        Assert.Equal("HELLO", previousTexts[1]);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public async Task StopCancelsActiveStepAndPreventsAnotherIteration()
    {
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        var controller = new RealtimeTranslationController(
            async (_, model, _, cancellationToken) =>
            {
                Interlocked.Increment(ref calls);
                started.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateResult(model, "unused", textChanged: true);
            },
            TimeSpan.Zero);

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await controller.StopAsync();
        await Task.Delay(30);

        Assert.Equal(1, calls);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public async Task StopSuppressesResultThatFinishesAfterCancellation()
    {
        var started = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var results = 0;
        var controller = new RealtimeTranslationController(
            async (_, model, _, _) =>
            {
                started.TrySetResult();
                await release.Task;
                return CreateResult(model, "LATE RESULT", textChanged: true);
            },
            TimeSpan.Zero);
        controller.ResultAvailable += _ => Interlocked.Increment(ref results);

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stopping = controller.StopAsync();
        release.SetResult();
        await stopping;

        Assert.Equal(0, results);
        Assert.False(controller.IsRunning);
    }

    private static RealtimeTranslationPipelineResult CreateResult(
        string model,
        string text,
        bool textChanged)
    {
        var image = new CapturedImage([1], 320, 100, TimeSpan.Zero);
        var ocr = new OcrResult(text, TimeSpan.Zero);
        var translation = textChanged
            ? new TranslationResult(text, "Bản dịch", model, TimeSpan.Zero)
            : null;
        return new RealtimeTranslationPipelineResult(
            new TranslationPipelineResult(image, ocr, translation, TimeSpan.Zero),
            textChanged);
    }
}
