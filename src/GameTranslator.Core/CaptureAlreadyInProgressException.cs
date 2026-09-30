namespace GameTranslator.Core;

public sealed class CaptureAlreadyInProgressException : InvalidOperationException
{
    public CaptureAlreadyInProgressException()
        : base("A screen capture is already in progress.")
    {
    }
}
