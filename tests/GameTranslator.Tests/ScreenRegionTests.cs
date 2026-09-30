using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class ScreenRegionTests
{
    [Fact]
    public void AcceptsPositiveWidthAndHeight()
    {
        var region = new ScreenRegion(10, 20, 300, 160);

        Assert.True(region.IsValid);
        region.ThrowIfInvalid();
    }

    [Fact]
    public void AllowsNegativeXCoordinate()
    {
        var region = new ScreenRegion(-500, 200, 300, 160);

        Assert.Equal(-500, region.X);
        Assert.True(region.IsValid);
    }

    [Fact]
    public void AllowsNegativeYCoordinate()
    {
        var region = new ScreenRegion(500, -200, 300, 160);

        Assert.Equal(-200, region.Y);
        Assert.True(region.IsValid);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveWidth(int width)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenRegion(0, 0, width, 10));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void RejectsNonPositiveHeight(int height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ScreenRegion(0, 0, 10, height));
    }
}
