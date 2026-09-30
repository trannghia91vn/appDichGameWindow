namespace GameTranslator.Core;

public sealed class ExactTranslationCache
{
    private readonly Dictionary<CacheKey, string> entries = [];
    private readonly object syncRoot = new();

    public bool TryGet(string sourceText, string model, out string translatedText)
    {
        var key = CreateKey(sourceText, model);
        lock (syncRoot)
        {
            return entries.TryGetValue(key, out translatedText!);
        }
    }

    public void Store(string sourceText, string model, string translatedText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(translatedText);
        var key = CreateKey(sourceText, model);

        lock (syncRoot)
        {
            entries[key] = translatedText;
        }
    }

    private static CacheKey CreateKey(string sourceText, string model)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        var normalizedText = OcrTextNormalizer.Normalize(sourceText);
        ArgumentException.ThrowIfNullOrWhiteSpace(normalizedText);
        return new CacheKey(normalizedText, model);
    }

    private readonly record struct CacheKey(string SourceText, string Model);
}
