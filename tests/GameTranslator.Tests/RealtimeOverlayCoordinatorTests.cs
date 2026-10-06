using GameTranslator.App.Overlay;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class RealtimeOverlayCoordinatorTests
{
    private static readonly ScreenRegion Region = new(0, 0, 320, 100);

    [Fact]
    public async Task CaptureExclusionAvoidsHidingAndChangedTextUpdatesOverlay()
    {
        var overlay = new FakeOverlayView("Bản dịch cũ");
        var pipeline = new FakeRealtimePipeline("NEW TEXT", "Bản dịch mới", textChanged: true);
        var coordinator = new RealtimeOverlayCoordinator(
            pipeline,
            overlay,
            () => true);

        var result = await coordinator.ExecuteAsync(
            Region,
            "translategemma:4b",
            "OLD TEXT",
            CancellationToken.None);

        Assert.True(result.TextChanged);
        Assert.Equal(0, overlay.HideCount);
        Assert.Equal("Bản dịch mới", overlay.DisplayedText);
        Assert.Equal("Realtime: đã cập nhật.", overlay.Status);
    }

    [Fact]
    public async Task FallbackHideRestoresExistingTranslationWhenTextIsUnchanged()
    {
        var overlay = new FakeOverlayView("Giữ bản dịch này");
        var pipeline = new FakeRealtimePipeline("SAME TEXT", null, textChanged: false);
        var coordinator = new RealtimeOverlayCoordinator(
            pipeline,
            overlay,
            () => false);

        var result = await coordinator.ExecuteAsync(
            Region,
            "translategemma:4b",
            "SAME TEXT",
            CancellationToken.None);

        Assert.False(result.TextChanged);
        Assert.Equal(1, overlay.HideCount);
        Assert.Equal(1, overlay.ShowAfterCaptureCount);
        Assert.Equal("Giữ bản dịch này", overlay.DisplayedText);
        Assert.Equal("Realtime: đang theo dõi.", overlay.Status);
    }

    private sealed class FakeOverlayView(string text) : ITranslationOverlayView
    {
        public string DisplayedText { get; private set; } = text;

        public string Status { get; private set; } = string.Empty;

        public int HideCount { get; private set; }

        public int ShowAfterCaptureCount { get; private set; }

        public void SetTranslationEnabled(bool isEnabled) { }

        public void SetStatus(string message) => Status = message;

        public void ShowProcessing(string message) => DisplayedText = message;

        public Task HideForCaptureAsync(CancellationToken cancellationToken)
        {
            HideCount++;
            return Task.CompletedTask;
        }

        public Task ShowAfterCaptureAsync(string message, CancellationToken cancellationToken)
        {
            ShowAfterCaptureCount++;
            DisplayedText = message;
            return Task.CompletedTask;
        }

        public void ShowTranslation(string translatedText) => DisplayedText = translatedText;

        public void ShowError(string message) => DisplayedText = message;
    }

    private sealed class FakeRealtimePipeline(
        string ocrText,
        string? translatedText,
        bool textChanged) : IRealtimeTranslationPipeline
    {
        public async Task<RealtimeTranslationPipelineResult> TranslateIfChangedAsync(
            ScreenRegion? region,
            string model,
            string? previousOcrText,
            CancellationToken cancellationToken,
            Func<CapturedImage, CancellationToken, Task>? captureCompleted = null)
        {
            var image = new CapturedImage([1], 320, 100, TimeSpan.Zero);
            if (captureCompleted is not null)
            {
                await captureCompleted(image, cancellationToken);
            }

            var ocr = new OcrResult(ocrText, TimeSpan.Zero);
            var translation = translatedText is null
                ? null
                : new TranslationResult(ocrText, translatedText, model, TimeSpan.Zero);
            return new RealtimeTranslationPipelineResult(
                new TranslationPipelineResult(image, ocr, translation, TimeSpan.Zero),
                textChanged);
        }
    }
}
