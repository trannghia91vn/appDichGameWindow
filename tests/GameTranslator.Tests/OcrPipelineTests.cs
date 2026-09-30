using System.Reflection;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OcrPipelineTests
{
    [Fact]
    public async Task OneExecutionCapturesAndRecognizesExactlyOnce()
    {
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("  THE DOOR IS LOCKED  ");
        var pipeline = new OcrPipeline(capture, ocr);

        var result = await pipeline.RecognizeAsync(
            new ScreenRegion(10, 20, 300, 100),
            CancellationToken.None);

        Assert.Equal(1, capture.CallCount);
        Assert.Equal(1, ocr.CallCount);
        Assert.Equal("THE DOOR IS LOCKED", result.Ocr.Text);
        Assert.True(result.Ocr.HasText);
    }

    [Fact]
    public async Task MissingRegionDoesNotCaptureOrRecognize()
    {
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("unused");
        var pipeline = new OcrPipeline(capture, ocr);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => pipeline.RecognizeAsync(null, CancellationToken.None));

        Assert.Equal(0, capture.CallCount);
        Assert.Equal(0, ocr.CallCount);
    }

    [Fact]
    public async Task CaptureFailureDoesNotCallOcr()
    {
        var capture = new FakeCaptureService
        {
            ExceptionToThrow = new InvalidOperationException("capture failed")
        };
        var ocr = new FakeOcrService("unused");
        var pipeline = new OcrPipeline(capture, ocr);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.RecognizeAsync(
            new ScreenRegion(0, 0, 100, 40),
            CancellationToken.None));

        Assert.Equal(1, capture.CallCount);
        Assert.Equal(0, ocr.CallCount);
    }

    [Fact]
    public async Task EmptyOcrTextReturnsValidNoTextResult()
    {
        var pipeline = new OcrPipeline(
            new FakeCaptureService(),
            new FakeOcrService("  \r\n "));

        var result = await pipeline.RecognizeAsync(
            new ScreenRegion(0, 0, 100, 40),
            CancellationToken.None);

        Assert.Equal(string.Empty, result.Ocr.Text);
        Assert.False(result.Ocr.HasText);
    }

    [Fact]
    public async Task ConcurrentExecutionIsRejectedWithoutQueueing()
    {
        var capture = new FakeCaptureService();
        var ocr = new BlockingOcrService();
        var pipeline = new OcrPipeline(capture, ocr);
        var region = new ScreenRegion(0, 0, 100, 40);

        var first = pipeline.RecognizeAsync(region, CancellationToken.None);
        await ocr.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<OcrPipelineBusyException>(
            () => pipeline.RecognizeAsync(region, CancellationToken.None));

        ocr.Release.SetResult();
        await first;
        Assert.Equal(1, capture.CallCount);
        Assert.Equal(1, ocr.CallCount);
    }

    [Fact]
    public void PipelineContainsNoPollingTimer()
    {
        var timerTypes = new[]
        {
            typeof(System.Threading.Timer),
            typeof(System.Timers.Timer),
            typeof(PeriodicTimer)
        };
        var fields = typeof(OcrPipeline)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field =>
            timerTypes.Any(timerType => timerType.IsAssignableFrom(field.FieldType)));
    }

    private sealed class FakeCaptureService : IScreenCaptureService
    {
        public Exception? ExceptionToThrow { get; init; }

        public int CallCount { get; private set; }

        public Task<CapturedImage> CaptureAsync(
            ScreenRegion region,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(new CapturedImage(
                [1, 2, 3],
                region.Width,
                region.Height,
                TimeSpan.FromMilliseconds(4)));
        }
    }

    private sealed class FakeOcrService : IOcrService
    {
        private readonly string text;

        public FakeOcrService(string text)
        {
            this.text = text;
        }

        public int CallCount { get; private set; }

        public Task<OcrResult> RecognizeEnglishAsync(
            CapturedImage image,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new OcrResult(text, TimeSpan.FromMilliseconds(12)));
        }
    }

    private sealed class BlockingOcrService : IOcrService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<OcrResult> RecognizeEnglishAsync(
            CapturedImage image,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new OcrResult("Done", TimeSpan.FromMilliseconds(12));
        }
    }
}
