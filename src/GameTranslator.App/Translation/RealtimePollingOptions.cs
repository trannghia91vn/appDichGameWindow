namespace GameTranslator.App.Translation;

public sealed class RealtimePollingOptions
{
    public static RealtimePollingOptions Balanced { get; } = new(
        TimeSpan.FromMilliseconds(1500),
        TimeSpan.FromMilliseconds(3000),
        TimeSpan.FromMilliseconds(500));

    public RealtimePollingOptions(
        TimeSpan initialDelay,
        TimeSpan maximumDelay,
        TimeSpan backoffStep)
    {
        if (initialDelay < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(initialDelay));
        }

        if (maximumDelay < initialDelay)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumDelay),
                "Maximum delay cannot be less than the initial delay.");
        }

        if (backoffStep < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(backoffStep));
        }

        InitialDelay = initialDelay;
        MaximumDelay = maximumDelay;
        BackoffStep = backoffStep;
    }

    public TimeSpan InitialDelay { get; }

    public TimeSpan MaximumDelay { get; }

    public TimeSpan BackoffStep { get; }

    public TimeSpan Increase(TimeSpan currentDelay)
    {
        var remaining = MaximumDelay - currentDelay;
        return remaining <= TimeSpan.Zero || BackoffStep >= remaining
            ? MaximumDelay
            : currentDelay + BackoffStep;
    }
}
