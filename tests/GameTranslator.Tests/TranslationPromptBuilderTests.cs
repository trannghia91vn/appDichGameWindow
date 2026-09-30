using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class TranslationPromptBuilderTests
{
    [Fact]
    public void TranslateGemmaUsesDedicatedPromptWithExactlyTwoBlankLines()
    {
        const string source = "We need to leave before sunrise.";

        var prompt = TranslationPromptBuilder.Build("translategemma:4b", source);

        Assert.StartsWith(
            "You are a professional English (en) to Vietnamese (vi) translator.",
            prompt);
        Assert.EndsWith($"Vietnamese:\n\n\n{source}", prompt);
        Assert.DoesNotContain($"Vietnamese:\n\n\n\n{source}", prompt);
        Assert.Equal(1, CountOccurrences(prompt, source));
        Assert.DoesNotContain("system", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void GenericModelUsesConcisePlainTextPrompt()
    {
        const string source = "The gate is locked.";

        var prompt = TranslationPromptBuilder.Build("qwen3:4b", source);

        Assert.Contains("Translate the following English game dialogue", prompt);
        Assert.Contains("Return only the Vietnamese translation.", prompt);
        Assert.Contains("Preserve character names", prompt);
        Assert.DoesNotContain("reason", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("JSON", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, CountOccurrences(prompt, source));
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }
}
