using GameTranslator.Core;

namespace GameTranslator.App.Translation;

public sealed class RealtimeTranslationController
{
    private static readonly TimeSpan DefaultInterval = TimeSpan.FromMilliseconds(850);

    private readonly Func<
        ScreenRegion,
        string,
        string?,
        CancellationToken,
        Task<RealtimeTranslationPipelineResult>> executeStep;
    private readonly TimeSpan interval;
    private CancellationTokenSource? sessionCancellation;
    private Task? loopTask;

    public RealtimeTranslationController(
        Func<
            ScreenRegion,
            string,
            string?,
            CancellationToken,
            Task<RealtimeTranslationPipelineResult>> executeStep,
        TimeSpan? interval = null)
    {
        this.executeStep = executeStep;
        this.interval = interval ?? DefaultInterval;
        if (this.interval < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }
    }

    public event Action<RealtimeTranslationPipelineResult>? ResultAvailable;

    public event Action<Exception>? StepFailed;

    public bool IsRunning => loopTask is { IsCompleted: false };

    public bool Start(
        ScreenRegion region,
        string model,
        CancellationToken lifetimeCancellationToken)
    {
        if (IsRunning)
        {
            return false;
        }

        region.ThrowIfInvalid();
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        sessionCancellation?.Dispose();
        sessionCancellation = CancellationTokenSource.CreateLinkedTokenSource(
            lifetimeCancellationToken);
        loopTask = RunAsync(region, model, sessionCancellation.Token);
        return true;
    }

    public void Stop() => sessionCancellation?.Cancel();

    public async Task StopAsync()
    {
        var task = loopTask;
        if (task is null)
        {
            return;
        }

        Stop();
        await task.ConfigureAwait(false);
        sessionCancellation?.Dispose();
        sessionCancellation = null;
        loopTask = null;
    }

    private async Task RunAsync(
        ScreenRegion region,
        string model,
        CancellationToken cancellationToken)
    {
        string? previousOcrText = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var result = await executeStep(
                        region,
                        model,
                        previousOcrText,
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (result.TextChanged)
                {
                    previousOcrText = result.PipelineResult.Ocr.Text;
                }

                ResultAvailable?.Invoke(result);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                StepFailed?.Invoke(ex);
            }

            try
            {
                await Task.Delay(interval, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
