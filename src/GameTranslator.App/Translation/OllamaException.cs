namespace GameTranslator.App.Translation;

public sealed class OllamaException : Exception
{
    public OllamaException(string userMessage, Exception? innerException = null)
        : base(userMessage, innerException)
    {
        UserMessage = userMessage;
    }

    public string UserMessage { get; }
}
