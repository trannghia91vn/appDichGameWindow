using System.Diagnostics;

namespace GameTranslator.App.Translation;

public sealed class RealtimeProcessPriorityScope : IDisposable
{
    private readonly Process? process;
    private readonly ProcessPriorityClass originalPriority;
    private readonly bool priorityChanged;
    private bool isDisposed;

    private RealtimeProcessPriorityScope(
        Process? process,
        ProcessPriorityClass originalPriority,
        bool priorityChanged)
    {
        this.process = process;
        this.originalPriority = originalPriority;
        this.priorityChanged = priorityChanged;
    }

    public static IDisposable EnterBelowNormal()
    {
        var process = Process.GetCurrentProcess();
        try
        {
            var originalPriority = process.PriorityClass;
            var shouldLowerPriority = originalPriority is
                ProcessPriorityClass.Normal or
                ProcessPriorityClass.AboveNormal or
                ProcessPriorityClass.High or
                ProcessPriorityClass.RealTime;

            if (shouldLowerPriority)
            {
                process.PriorityClass = ProcessPriorityClass.BelowNormal;
                Trace.TraceInformation(
                    "Realtime process priority changed from {0} to BelowNormal.",
                    originalPriority);
            }

            return new RealtimeProcessPriorityScope(
                process,
                originalPriority,
                shouldLowerPriority);
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not lower realtime process priority: {0}", ex);
            process.Dispose();
            return new RealtimeProcessPriorityScope(null, default, false);
        }
    }

    public void Dispose()
    {
        if (isDisposed)
        {
            return;
        }

        isDisposed = true;
        if (process is null)
        {
            return;
        }

        try
        {
            if (priorityChanged)
            {
                process.PriorityClass = originalPriority;
                Trace.TraceInformation(
                    "Realtime process priority restored to {0}.",
                    originalPriority);
            }
        }
        catch (Exception ex)
        {
            Trace.TraceWarning("Could not restore process priority: {0}", ex);
        }
        finally
        {
            process.Dispose();
        }
    }
}
