namespace GameTranslator.App.Ocr;

public sealed class OcrInitializationException : InvalidOperationException
{
    public OcrInitializationException(Exception innerException)
        : base("RapidOcrNet initialization failed.", innerException)
    {
    }
}
