namespace GameTranslator.App.Translation;

public static class OllamaOptions
{
    public const string DefaultBaseUrl = "http://localhost:11434";
    public const string PreferredModel = "translategemma:4b";
    public const int ContextSize = 4096;
    public const int MaxOutputTokens = 256;
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(25);
}
