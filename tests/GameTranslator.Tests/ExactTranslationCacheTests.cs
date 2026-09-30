using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class ExactTranslationCacheTests
{
    [Fact]
    public void ExactNormalizedTextAndModelReturnsCachedTranslation()
    {
        var cache = new ExactTranslationCache();
        cache.Store("  We must leave.  ", "translategemma:4b", "Chúng ta phải đi.");

        var found = cache.TryGet(
            "We must leave.",
            "translategemma:4b",
            out var translation);

        Assert.True(found);
        Assert.Equal("Chúng ta phải đi.", translation);
    }

    [Fact]
    public void PunctuationChangeIsCacheMiss()
    {
        var cache = new ExactTranslationCache();
        cache.Store("We must leave.", "translategemma:4b", "Chúng ta phải đi.");

        Assert.False(cache.TryGet(
            "We must leave!",
            "translategemma:4b",
            out _));
    }

    [Fact]
    public void ModelChangeIsCacheMiss()
    {
        var cache = new ExactTranslationCache();
        cache.Store("We must leave.", "translategemma:4b", "Chúng ta phải đi.");

        Assert.False(cache.TryGet("We must leave.", "qwen3:4b", out _));
    }
}
