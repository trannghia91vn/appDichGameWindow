using GameTranslator.App.Overlay;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OverlayTranslationCoordinatorTests
{
    private static readonly ScreenRegion Region = new(0, 0, 320, 120);

    [Fact]
    public async Task SuccessHidesBeforeCaptureAndShowsBeforeOcrAndTranslation()
    {
        var events = new List<string>();
        var overlay = new FakeOverlayView(events);
        var pipeline = new OrderedPipeline(events);
        var coordinator = new OverlayTranslationCoordinator(pipeline, overlay);

        var result = await coordinator.ExecuteAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);

        Assert.Equal(
            ["hide", "capture", "show-after-capture", "ocr", "translation", "show-translation"],
            events.Where(value => !value.StartsWith("enabled:", StringComparison.Ordinal) &&
                !value.StartsWith("processing:", StringComparison.Ordinal)));
        Assert.Equal("Bản dịch tiếng Việt", overlay.Text);
        Assert.True(overlay.TranslationEnabled);
        Assert.NotNull(result.Translation);
    }

    [Fact]
    public async Task FailureRestoresOverlayWithErrorState()
    {
        var events = new List<string>();
        var overlay = new FakeOverlayView(events);
        var pipeline = new OrderedPipeline(events)
        {
            ExceptionToThrow = new InvalidOperationException("failed")
        };
        var coordinator = new OverlayTranslationCoordinator(
            pipeline,
            overlay,
            _ => "Không thể dịch nội dung này.");

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));

        Assert.Equal("Không thể dịch nội dung này.", overlay.Text);
        Assert.Contains("show-error", events);
        Assert.True(overlay.TranslationEnabled);
    }

    [Fact]
    public async Task ConcurrentExecutionIsRejected()
    {
        var overlay = new FakeOverlayView([]);
        var pipeline = new BlockingPipeline();
        var coordinator = new OverlayTranslationCoordinator(pipeline, overlay);

        var first = coordinator.ExecuteAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);
        await pipeline.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<TranslationPipelineBusyException>(() =>
            coordinator.ExecuteAsync(
                Region,
                "translategemma:4b",
                CancellationToken.None));

        pipeline.Release.SetResult();
        await first;
        Assert.Equal(1, pipeline.CallCount);
    }

    private sealed class FakeOverlayView(List<string> events) : ITranslationOverlayView
    {
        public string Text { get; private set; } = string.Empty;

        public string DisplayedText => Text;

        public bool TranslationEnabled { get; private set; } = true;

        public void SetTranslationEnabled(bool isEnabled)
        {
            TranslationEnabled = isEnabled;
            events.Add($"enabled:{isEnabled}");
        }

        public void SetStatus(string message) => events.Add($"status:{message}");

        public void ShowProcessing(string message) => events.Add($"processing:{message}");

        public Task HideForCaptureAsync(CancellationToken cancellationToken)
        {
            events.Add("hide");
            return Task.CompletedTask;
        }

        public Task ShowAfterCaptureAsync(string message, CancellationToken cancellationToken)
        {
            events.Add("show-after-capture");
            return Task.CompletedTask;
        }

        public void ShowTranslation(string translatedText)
        {
            Text = translatedText;
            events.Add("show-translation");
        }

        public void ShowError(string message)
        {
            Text = message;
            events.Add("show-error");
        }
    }

    private sealed class OrderedPipeline(List<string> events) : ITranslationPipeline
    {
        public Exception? ExceptionToThrow { get; init; }

        public async Task<TranslationPipelineResult> TranslateAsync(
            ScreenRegion? region,
            string model,
            CancellationToken cancellationToken,
            Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
        {
            events.Add("capture");
            var image = new CapturedImage([1], 320, 120, TimeSpan.FromMilliseconds(4));
            if (captureCompleted is not null)
            {
                await captureCompleted(image, cancellationToken);
            }

            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            events.Add("ocr");
            var ocr = new OcrResult("English text", TimeSpan.FromMilliseconds(10));
            events.Add("translation");
            var translation = new TranslationResult(
                ocr.Text,
                "Bản dịch tiếng Việt",
                model,
                TimeSpan.FromMilliseconds(20));
            return new TranslationPipelineResult(
                image,
                ocr,
                translation,
                TimeSpan.FromMilliseconds(34));
        }
    }

    private sealed class BlockingPipeline : ITranslationPipeline
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<TranslationPipelineResult> TranslateAsync(
            ScreenRegion? region,
            string model,
            CancellationToken cancellationToken,
            Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
        {
            CallCount++;
            var image = new CapturedImage([1], 320, 120, TimeSpan.Zero);
            if (captureCompleted is not null)
            {
                await captureCompleted(image, cancellationToken);
            }

            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            var ocr = new OcrResult("English", TimeSpan.Zero);
            return new TranslationPipelineResult(
                image,
                ocr,
                new TranslationResult(ocr.Text, "Tiếng Việt", model, TimeSpan.Zero),
                TimeSpan.Zero);
        }
    }
}
