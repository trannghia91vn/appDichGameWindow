namespace GameTranslator.Core;

public sealed record TranslationResult(
    string SourceText,
    string TranslatedText,
    string Model,
    TimeSpan Duration,
    bool FromCache = false,
    int? PromptEvalCount = null,
    int? EvalCount = null,
    TimeSpan? LoadDuration = null,
    TimeSpan? EvalDuration = null);
