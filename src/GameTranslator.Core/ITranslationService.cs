namespace GameTranslator.Core;

public interface ITranslationService
{
    Task<TranslationResult> TranslateEnglishToVietnameseAsync(
        string englishText,
        string model,
        CancellationToken cancellationToken);
}
