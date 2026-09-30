using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using GameTranslator.App.Ocr;
using GameTranslator.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameTranslator.Tests;

public sealed class RapidOcrIntegrationTests
{
    private readonly ITestOutputHelper output;

    public RapidOcrIntegrationTests(ITestOutputHelper output)
    {
        this.output = output;
    }

    [Fact]
    public async Task BundledLatinModelsRecognizeRepeatedInMemoryEnglishText()
    {
        using var service = new RapidOcrService();
        var image = CreateEnglishTextImage("THE DOOR IS LOCKED");
        var results = new List<OcrResult>();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            results.Add(await service.RecognizeEnglishAsync(image, CancellationToken.None));
        }

        var differentText = await service.RecognizeEnglishAsync(
            CreateEnglishTextImage("RETURN BEFORE SUNRISE"),
            CancellationToken.None);
        var noText = await service.RecognizeEnglishAsync(
            CreateEnglishTextImage(string.Empty),
            CancellationToken.None);

        Assert.All(results, result =>
        {
            Assert.True(result.HasText);
            Assert.Contains("DOOR", result.Text, StringComparison.OrdinalIgnoreCase);
        });
        Assert.Contains("SUNRISE", differentText.Text, StringComparison.OrdinalIgnoreCase);
        Assert.False(noText.HasText);

        var steadyState = results.Skip(1).Select(result => result.ProcessingDuration.TotalMilliseconds).ToArray();
        output.WriteLine("OCR initialization: {0:F0} ms", service.InitializationDuration?.TotalMilliseconds);
        output.WriteLine("First OCR: {0:F0} ms", results[0].ProcessingDuration.TotalMilliseconds);
        output.WriteLine("Steady-state OCR average: {0:F0} ms", steadyState.Average());
    }

    private static CapturedImage CreateEnglishTextImage(string text)
    {
        const int width = 1100;
        const int height = 220;

        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        using var font = new Font("Arial", 72, FontStyle.Bold, GraphicsUnit.Pixel);
        graphics.Clear(Color.White);
        graphics.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        graphics.DrawString(text, font, Brushes.Black, new PointF(24, 58));

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        return new CapturedImage(stream.ToArray(), width, height, TimeSpan.Zero);
    }
}
