using GameTranslator.App.Translation;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class RealtimeTranslationControllerTests
{
    private static readonly ScreenRegion Region = new(0, 0, 320, 100);
    private static readonly RealtimePollingOptions FastPolling = new(
        TimeSpan.FromMilliseconds(5),
        TimeSpan.FromMilliseconds(5),
        TimeSpan.Zero);

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
            FastPolling);
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
            FastPolling);

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
            FastPolling);
        controller.ResultAvailable += _ => Interlocked.Increment(ref results);

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var stopping = controller.StopAsync();
        release.SetResult();
        await stopping;

        Assert.Equal(0, results);
        Assert.False(controller.IsRunning);
    }

    [Fact]
    public async Task UnchangedTextBacksOffToMaximumDelay()
    {
        var delays = new List<TimeSpan>();
        var reachedExpectedDelays = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var call = 0;
        var controller = new RealtimeTranslationController(
            (_, model, _, _) => Task.FromResult(CreateResult(
                model,
                "SAME TEXT",
                textChanged: Interlocked.Increment(ref call) == 1)),
            RealtimePollingOptions.Balanced,
            delayAsync: CreateRecordingDelay(delays, 5, reachedExpectedDelays));

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await reachedExpectedDelays.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await controller.StopAsync();

        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(1500),
                TimeSpan.FromMilliseconds(2000),
                TimeSpan.FromMilliseconds(2500),
                TimeSpan.FromMilliseconds(3000),
                TimeSpan.FromMilliseconds(3000)
            ],
            delays);
    }

    [Fact]
    public async Task ChangedTextResetsDelayToInitialValue()
    {
        var delays = new List<TimeSpan>();
        var reachedExpectedDelays = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var textChanged = new[] { true, false, true };
        var call = 0;
        var controller = new RealtimeTranslationController(
            (_, model, _, _) =>
            {
                var index = Math.Min(Interlocked.Increment(ref call) - 1, textChanged.Length - 1);
                return Task.FromResult(CreateResult(model, $"TEXT {index}", textChanged[index]));
            },
            RealtimePollingOptions.Balanced,
            delayAsync: CreateRecordingDelay(delays, 3, reachedExpectedDelays));

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await reachedExpectedDelays.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await controller.StopAsync();

        Assert.Equal(
            [
                TimeSpan.FromMilliseconds(1500),
                TimeSpan.FromMilliseconds(2000),
                TimeSpan.FromMilliseconds(1500)
            ],
            delays);
    }

    [Fact]
    public async Task FailedStepUsesMaximumDelay()
    {
        var delays = new List<TimeSpan>();
        var delayRecorded = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new RealtimeTranslationController(
            (_, _, _, _) => throw new InvalidOperationException("temporary failure"),
            RealtimePollingOptions.Balanced,
            delayAsync: CreateRecordingDelay(delays, 1, delayRecorded));

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await delayRecorded.Task.WaitAsync(TimeSpan.FromSeconds(3));
        await controller.StopAsync();

        Assert.Equal([TimeSpan.FromMilliseconds(3000)], delays);
    }

    [Fact]
    public async Task ExecutionScopeIsDisposedWhenRealtimeStops()
    {
        var stepStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var scopeEntered = false;
        var scopeDisposed = false;
        var controller = new RealtimeTranslationController(
            async (_, model, _, cancellationToken) =>
            {
                stepStarted.TrySetResult();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                return CreateResult(model, "unused", textChanged: true);
            },
            FastPolling,
            () =>
            {
                scopeEntered = true;
                return new CallbackDisposable(() => scopeDisposed = true);
            });

        controller.Start(Region, "translategemma:4b", CancellationToken.None);
        await stepStarted.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Assert.True(scopeEntered);

        await controller.StopAsync();

        Assert.True(scopeDisposed);
    }

    [Fact]
    public void PollingOptionsRejectInvalidRanges()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RealtimePollingOptions(
            TimeSpan.FromMilliseconds(-1),
            TimeSpan.Zero,
            TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RealtimePollingOptions(
            TimeSpan.FromSeconds(2),
            TimeSpan.FromSeconds(1),
            TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new RealtimePollingOptions(
            TimeSpan.Zero,
            TimeSpan.Zero,
            TimeSpan.FromMilliseconds(-1)));
    }

    private static Func<TimeSpan, CancellationToken, Task> CreateRecordingDelay(
        List<TimeSpan> delays,
        int blockAfterCount,
        TaskCompletionSource reachedExpectedCount) =>
        (delay, cancellationToken) =>
        {
            delays.Add(delay);
            if (delays.Count < blockAfterCount)
            {
                return Task.CompletedTask;
            }

            reachedExpectedCount.TrySetResult();
            return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        };

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

    private sealed class CallbackDisposable(Action onDispose) : IDisposable
    {
        public void Dispose() => onDispose();
    }
}
