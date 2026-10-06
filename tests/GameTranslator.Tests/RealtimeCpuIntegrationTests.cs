using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using GameTranslator.App.Ocr;
using GameTranslator.App.Translation;
using GameTranslator.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameTranslator.Tests;

public sealed class RealtimeCpuIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task BalancedRealtimeStaysWithinCpuPeakBudget()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GAMETRANSLATOR_CPU_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        using var ocrService = new RapidOcrService();
        var image = CreateEnglishTextImage("THE DOOR IS LOCKED");
        var firstResult = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var controller = new RealtimeTranslationController(
            async (_, model, previousText, cancellationToken) =>
            {
                var ocr = await ocrService.RecognizeEnglishAsync(image, cancellationToken);
                var textChanged = !string.Equals(
                    ocr.Text,
                    previousText,
                    StringComparison.Ordinal);
                return new RealtimeTranslationPipelineResult(
                    new TranslationPipelineResult(image, ocr, null, ocr.ProcessingDuration),
                    textChanged);
            },
            RealtimePollingOptions.Balanced,
            RealtimeProcessPriorityScope.EnterBelowNormal);
        controller.ResultAvailable += _ => firstResult.TrySetResult();

        using var samplingCancellation = new CancellationTokenSource();
        var samples = new List<double>();
        var sampler = SampleCpuAsync(samples, samplingCancellation.Token);
        using var process = Process.GetCurrentProcess();
        var originalPriority = process.PriorityClass;

        controller.Start(
            new ScreenRegion(0, 0, image.Width, image.Height),
            "performance-smoke",
            CancellationToken.None);
        await firstResult.Task.WaitAsync(TimeSpan.FromSeconds(5));
        process.Refresh();
        var expectedRealtimePriority = originalPriority is
            ProcessPriorityClass.Normal or
            ProcessPriorityClass.AboveNormal or
            ProcessPriorityClass.High or
            ProcessPriorityClass.RealTime
                ? ProcessPriorityClass.BelowNormal
                : originalPriority;
        Assert.Equal(expectedRealtimePriority, process.PriorityClass);

        await Task.Delay(TimeSpan.FromSeconds(30));
        await controller.StopAsync();
        process.Refresh();
        Assert.Equal(originalPriority, process.PriorityClass);
        await samplingCancellation.CancelAsync();
        await sampler;

        var peak = samples.Max();
        var average = samples.Average();
        output.WriteLine("Logical processors: {0}", Environment.ProcessorCount);
        output.WriteLine("CPU samples: {0}", samples.Count);
        output.WriteLine("Average total-machine CPU: {0:F1}%", average);
        output.WriteLine("Peak total-machine CPU: {0:F1}%", peak);

        if (Environment.ProcessorCount >= 16)
        {
            Assert.True(
                peak < 15,
                $"Realtime CPU peak {peak:F1}% exceeded the 15% budget.");
        }
    }

    private static async Task SampleCpuAsync(
        List<double> samples,
        CancellationToken cancellationToken)
    {
        using var process = Process.GetCurrentProcess();
        var previousCpu = process.TotalProcessorTime.TotalSeconds;
        var previousTime = Stopwatch.GetTimestamp();

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            process.Refresh();
            var currentTime = Stopwatch.GetTimestamp();
            var currentCpu = process.TotalProcessorTime.TotalSeconds;
            var elapsedSeconds = Stopwatch.GetElapsedTime(previousTime, currentTime).TotalSeconds;
            var totalMachinePercent =
                (currentCpu - previousCpu) /
                elapsedSeconds /
                Environment.ProcessorCount *
                100;
            samples.Add(totalMachinePercent);
            previousCpu = currentCpu;
            previousTime = currentTime;
        }
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
