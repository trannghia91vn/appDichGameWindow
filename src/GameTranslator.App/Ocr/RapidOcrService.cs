using System.Diagnostics;
using System.IO;
using GameTranslator.Core;
using RapidOcrNet;
using SkiaSharp;
using CoreOcrResult = GameTranslator.Core.OcrResult;

namespace GameTranslator.App.Ocr;

public sealed class RapidOcrService : IOcrService, IDisposable
{
    private readonly object initializationLock = new();
    private readonly SemaphoreSlim inferenceGate = new(1, 1);
    private Task<RapidOcr>? initializationTask;
    private bool isDisposed;

    public TimeSpan? InitializationDuration { get; private set; }

    public async Task<CoreOcrResult> RecognizeEnglishAsync(
        CapturedImage image,
        CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(isDisposed, this);
        image.ThrowIfInvalid();

        await inferenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var engine = await GetEngineAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            using var bitmap = SKBitmap.Decode(image.PngBytes)
                ?? throw new InvalidDataException("The captured PNG image could not be decoded.");

            var stopwatch = Stopwatch.StartNew();
            var result = await engine
                .DetectAsync(bitmap, RapidOcrOptions.Default, null, cancellationToken)
                .ConfigureAwait(false);
            stopwatch.Stop();

            return new CoreOcrResult(
                result.StrRes ?? string.Empty,
                stopwatch.Elapsed,
                CalculateAverageConfidence(result));
        }
        finally
        {
            inferenceGate.Release();
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (initializationTask?.IsCompletedSuccessfully == true)
        {
            initializationTask.Result.Dispose();
        }

        inferenceGate.Dispose();
    }

    private async Task<RapidOcr> GetEngineAsync(CancellationToken cancellationToken)
    {
        Task<RapidOcr> task;
        lock (initializationLock)
        {
            initializationTask ??= Task.Run(InitializeEngine);
            task = initializationTask;
        }

        try
        {
            return await task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new OcrInitializationException(ex);
        }
    }

    private RapidOcr InitializeEngine()
    {
        var stopwatch = Stopwatch.StartNew();
        var engine = new RapidOcr();
        try
        {
            var modelDirectory = Path.Combine(AppContext.BaseDirectory, "models", "v5");
            engine.InitModels(
                detPath: Path.Combine(modelDirectory, "ch_PP-OCRv5_mobile_det.onnx"),
                clsPath: Path.Combine(modelDirectory, "ch_PP-LCNet_x0_25_textline_ori_cls_mobile.onnx"),
                recPath: Path.Combine(modelDirectory, "latin_PP-OCRv5_rec_mobile_infer.onnx"),
                keysPath: Path.Combine(modelDirectory, "ppocrv5_latin_dict.txt"));
            stopwatch.Stop();
            InitializationDuration = stopwatch.Elapsed;
            Trace.TraceInformation(
                "RapidOcrNet initialized in {0:F0} ms.",
                stopwatch.Elapsed.TotalMilliseconds);
            return engine;
        }
        catch
        {
            engine.Dispose();
            throw;
        }
    }

    private static double? CalculateAverageConfidence(RapidOcrNet.OcrResult result)
    {
        var scores = result.TextBlocks
            .SelectMany(block => block.CharScores ?? [])
            .ToArray();

        return scores.Length == 0 ? null : scores.Average();
    }
}
