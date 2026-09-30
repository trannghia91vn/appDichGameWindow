namespace GameTranslator.Core;

public sealed class TranslationPipelineBusyException : InvalidOperationException
{
    public TranslationPipelineBusyException()
        : base("A translation pipeline execution is already in progress.")
    {
    }
}
