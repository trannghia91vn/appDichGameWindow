using GameTranslator.Core;

namespace GameTranslator.App.Translation;

public sealed class RealtimeTranslationController
{
    private readonly Func<
        ScreenRegion,
        string,
        string?,
        CancellationToken,
        Task<RealtimeTranslationPipelineResult>> executeStep;
    private readonly RealtimePollingOptions pollingOptions;
    private readonly Func<IDisposable> executionScopeFactory;
    private readonly Func<TimeSpan, CancellationToken, Task> delayAsync;
    private CancellationTokenSource? sessionCancellation;
    private Task? loopTask;

    public RealtimeTranslationController(
        Func<
            ScreenRegion,
            string,
            string?,
            CancellationToken,
            Task<RealtimeTranslationPipelineResult>> executeStep,
        RealtimePollingOptions? pollingOptions = null,
        Func<IDisposable>? executionScopeFactory = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        this.executeStep = executeStep;
        this.pollingOptions = pollingOptions ?? RealtimePollingOptions.Balanced;
        this.executionScopeFactory = executionScopeFactory ??
            (static () => EmptyDisposable.Instance);
        this.delayAsync = delayAsync ?? Task.Delay;
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
        using var executionScope = executionScopeFactory();
        string? previousOcrText = null;
        var nextDelay = pollingOptions.InitialDelay;

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
                    nextDelay = pollingOptions.InitialDelay;
                }
                else
                {
                    nextDelay = pollingOptions.Increase(nextDelay);
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
                nextDelay = pollingOptions.MaximumDelay;
            }

            try
            {
                await delayAsync(nextDelay, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private sealed class EmptyDisposable : IDisposable
    {
        public static EmptyDisposable Instance { get; } = new();

        public void Dispose() { }
    }
}
