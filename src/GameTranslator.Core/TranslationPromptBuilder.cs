namespace GameTranslator.Core;

public static class TranslationPromptBuilder
{
    private const string TranslateGemmaPrefix =
        "You are a professional English (en) to Vietnamese (vi) translator. Your goal is to accurately convey the meaning and nuances of the original English text while adhering to Vietnamese grammar, vocabulary, and cultural sensitivities.\n" +
        "Produce only the Vietnamese translation, without any additional explanations or commentary. Please translate the following English text into Vietnamese:";

    private const string GenericPrefix =
        "Translate the following English game dialogue into natural Vietnamese.\n" +
        "Return only the Vietnamese translation.\n" +
        "Preserve character names, item names, numbers, punctuation, and meaningful line breaks.";

    public static string Build(string model, string englishText)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        ArgumentException.ThrowIfNullOrWhiteSpace(englishText);

        return IsTranslateGemma(model)
            ? $"{TranslateGemmaPrefix}\n\n\n{englishText}"
            : $"{GenericPrefix}\n\n{englishText}";
    }

    public static bool IsTranslateGemma(string model) =>
        model.StartsWith("translategemma", StringComparison.OrdinalIgnoreCase);
}
