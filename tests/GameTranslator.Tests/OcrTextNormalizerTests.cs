using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OcrTextNormalizerTests
{
    [Fact]
    public void TrimsOuterWhitespaceAndLineEndings()
    {
        const string input = "  Hello world  \r\n\r\n  We must go.  ";

        var result = OcrTextNormalizer.Normalize(input);

        Assert.Equal("Hello world\n\n  We must go.", result);
    }

    [Fact]
    public void NormalizesCarriageReturnsAndPreservesLines()
    {
        const string input = "Arthur\rWe have to leave.\r\nBefore sunrise.";

        var result = OcrTextNormalizer.Normalize(input);

        Assert.Equal("Arthur\nWe have to leave.\nBefore sunrise.", result);
    }

    [Fact]
    public void CollapsesRepeatedBlankLinesToOneBlankLine()
    {
        const string input = "First\n\n\n\nSecond";

        var result = OcrTextNormalizer.Normalize(input);

        Assert.Equal("First\n\nSecond", result);
    }

    [Fact]
    public void PreservesPunctuationAndWordCasing()
    {
        const string input = "Don't move! Is that Arthur's sword?";

        Assert.Equal(input, OcrTextNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  \r\n  ")]
    public void EmptyOrWhitespaceInputReturnsEmpty(string? input)
    {
        Assert.Equal(string.Empty, OcrTextNormalizer.Normalize(input));
    }
}
