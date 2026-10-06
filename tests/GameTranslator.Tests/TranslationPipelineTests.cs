using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class TranslationPipelineTests
{
    private static readonly ScreenRegion Region = new(10, 20, 300, 100);

    [Fact]
    public async Task NormalPathCallsEachServiceExactlyOnce()
    {
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("  We must leave.  ");
        var translation = new FakeTranslationService("Chúng ta phải đi.");
        var pipeline = CreatePipeline(capture, ocr, translation);

        var result = await pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);

        Assert.Equal(1, capture.CallCount);
        Assert.Equal(1, ocr.CallCount);
        Assert.Equal(1, translation.CallCount);
        Assert.Equal("We must leave.", result.Ocr.Text);
        Assert.Equal("Chúng ta phải đi.", result.Translation?.TranslatedText);
        Assert.False(result.Translation?.FromCache);
    }

    [Fact]
    public async Task CacheHitDoesNotCallTranslationServiceAgain()
    {
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("We must leave.");
        var translation = new FakeTranslationService("Chúng ta phải đi.");
        var pipeline = CreatePipeline(capture, ocr, translation);

        await pipeline.TranslateAsync(Region, "translategemma:4b", CancellationToken.None);
        var cached = await pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);

        Assert.Equal(2, capture.CallCount);
        Assert.Equal(2, ocr.CallCount);
        Assert.Equal(1, translation.CallCount);
        Assert.True(cached.Translation?.FromCache);
    }

    [Fact]
    public async Task RealtimeUnchangedTextSkipsTranslationEntirely()
    {
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("  The gate is locked.  ");
        var translation = new FakeTranslationService("Cổng đã khóa.");
        var pipeline = CreatePipeline(capture, ocr, translation);

        var first = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            previousOcrText: null,
            CancellationToken.None);
        var unchanged = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            first.PipelineResult.Ocr.Text,
            CancellationToken.None);

        Assert.True(first.TextChanged);
        Assert.NotNull(first.PipelineResult.Translation);
        Assert.False(unchanged.TextChanged);
        Assert.Null(unchanged.PipelineResult.Translation);
        Assert.Equal(2, capture.CallCount);
        Assert.Equal(2, ocr.CallCount);
        Assert.Equal(1, translation.CallCount);
    }

    [Fact]
    public async Task RealtimeChangedTextTranslatesNewTextOnce()
    {
        var capture = new FakeCaptureService();
        var ocr = new SequenceOcrService("FIRST LINE", "SECOND LINE");
        var translation = new FakeTranslationService("Bản dịch");
        var pipeline = new TranslationPipeline(
            capture,
            ocr,
            translation,
            new ExactTranslationCache());

        var first = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            previousOcrText: null,
            CancellationToken.None);
        var second = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            first.PipelineResult.Ocr.Text,
            CancellationToken.None);

        Assert.True(first.TextChanged);
        Assert.True(second.TextChanged);
        Assert.Equal("SECOND LINE", second.PipelineResult.Ocr.Text);
        Assert.Equal(2, translation.CallCount);
    }

    [Fact]
    public async Task RealtimeTextReturningAfterBlankIsTranslatedAgain()
    {
        var capture = new FakeCaptureService();
        var ocr = new SequenceOcrService("REPEATED LINE", string.Empty, "REPEATED LINE");
        var translation = new FakeTranslationService("Bản dịch");
        var pipeline = new TranslationPipeline(
            capture,
            ocr,
            translation,
            new ExactTranslationCache());

        var first = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            previousOcrText: null,
            CancellationToken.None);
        var blank = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            first.PipelineResult.Ocr.Text,
            CancellationToken.None);
        var repeated = await pipeline.TranslateIfChangedAsync(
            Region,
            "translategemma:4b",
            blank.PipelineResult.Ocr.Text,
            CancellationToken.None);

        Assert.True(blank.TextChanged);
        Assert.False(blank.PipelineResult.Ocr.HasText);
        Assert.Null(blank.PipelineResult.Translation);
        Assert.True(repeated.TextChanged);
        Assert.NotNull(repeated.PipelineResult.Translation);
        Assert.Equal(1, translation.CallCount);
        Assert.True(repeated.PipelineResult.Translation?.FromCache);
    }

    [Fact]
    public async Task EmptyOcrDoesNotCallTranslationService()
    {
        var translation = new FakeTranslationService("unused");
        var pipeline = CreatePipeline(
            new FakeCaptureService(),
            new FakeOcrService("  \r\n "),
            translation);

        var result = await pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);

        Assert.Null(result.Translation);
        Assert.Equal(0, translation.CallCount);
    }

    [Fact]
    public async Task FailedTranslationIsNotCached()
    {
        var translation = new FakeTranslationService("unused")
        {
            ExceptionToThrow = new InvalidOperationException("failed")
        };
        var pipeline = CreatePipeline(
            new FakeCaptureService(),
            new FakeOcrService("We must leave."),
            translation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));

        Assert.Equal(2, translation.CallCount);
    }

    [Fact]
    public async Task EmptyTranslationIsNotCached()
    {
        var translation = new FakeTranslationService("  ");
        var pipeline = CreatePipeline(
            new FakeCaptureService(),
            new FakeOcrService("We must leave."),
            translation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));
        await Assert.ThrowsAsync<InvalidOperationException>(() => pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));

        Assert.Equal(2, translation.CallCount);
    }

    [Fact]
    public async Task ConcurrentExecutionIsRejectedWithoutQueueing()
    {
        var translation = new BlockingTranslationService();
        var capture = new FakeCaptureService();
        var ocr = new FakeOcrService("We must leave.");
        var pipeline = new TranslationPipeline(
            capture,
            ocr,
            translation,
            new ExactTranslationCache());

        var first = pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None);
        await translation.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<TranslationPipelineBusyException>(() => pipeline.TranslateAsync(
            Region,
            "translategemma:4b",
            CancellationToken.None));

        translation.Release.SetResult();
        await first;
        Assert.Equal(1, capture.CallCount);
        Assert.Equal(1, ocr.CallCount);
        Assert.Equal(1, translation.CallCount);
    }

    private static TranslationPipeline CreatePipeline(
        FakeCaptureService capture,
        FakeOcrService ocr,
        FakeTranslationService translation) =>
        new(capture, ocr, translation, new ExactTranslationCache());

    private sealed class FakeCaptureService : IScreenCaptureService
    {
        public int CallCount { get; private set; }

        public Task<CapturedImage> CaptureAsync(
            ScreenRegion region,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new CapturedImage(
                [1, 2, 3],
                region.Width,
                region.Height,
                TimeSpan.FromMilliseconds(4)));
        }
    }

    private sealed class FakeOcrService(string text) : IOcrService
    {
        public int CallCount { get; private set; }

        public Task<OcrResult> RecognizeEnglishAsync(
            CapturedImage image,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(new OcrResult(text, TimeSpan.FromMilliseconds(12)));
        }
    }

    private sealed class SequenceOcrService(params string[] texts) : IOcrService
    {
        private int index;

        public Task<OcrResult> RecognizeEnglishAsync(
            CapturedImage image,
            CancellationToken cancellationToken)
        {
            var text = texts[Math.Min(index, texts.Length - 1)];
            index++;
            return Task.FromResult(new OcrResult(text, TimeSpan.FromMilliseconds(12)));
        }
    }

    private sealed class FakeTranslationService(string translatedText) : ITranslationService
    {
        public Exception? ExceptionToThrow { get; init; }

        public int CallCount { get; private set; }

        public Task<TranslationResult> TranslateEnglishToVietnameseAsync(
            string englishText,
            string model,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(new TranslationResult(
                englishText,
                translatedText,
                model,
                TimeSpan.FromMilliseconds(50)));
        }
    }

    private sealed class BlockingTranslationService : ITranslationService
    {
        public TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<TranslationResult> TranslateEnglishToVietnameseAsync(
            string englishText,
            string model,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new TranslationResult(
                englishText,
                "Chúng ta phải đi.",
                model,
                TimeSpan.FromMilliseconds(50));
        }
    }
}
