using GameTranslator.App.Overlay;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OverlayTextFormatterTests
{
    [Theory]
    [InlineData("The gate\nis locked.\n\nFind the key.", "The gate is locked. Find the key.")]
    [InlineData("  A\r\n  B\rC\t  D  ", "A B C D")]
    [InlineData("Wait... don't go!", "Wait... don't go!")]
    [InlineData(" \r\n\t ", "")]
    public void TranslationFlowsWithoutForcedLineBreaks(string input, string expected)
    {
        Assert.Equal(expected, OverlayTextFormatter.Format(input));
    }
}
