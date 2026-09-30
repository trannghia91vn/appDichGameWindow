namespace GameTranslator.Core;

public sealed class OcrPipelineBusyException : InvalidOperationException
{
    public OcrPipelineBusyException()
        : base("An OCR operation is already in progress.")
    {
    }
}
