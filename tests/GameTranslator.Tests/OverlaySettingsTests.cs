using System.IO;
using GameTranslator.App.Overlay;
using GameTranslator.App.Settings;
using Xunit;

namespace GameTranslator.Tests;

public sealed class OverlaySettingsTests
{
    [Fact]
    public async Task OverlayAppearanceAndPlacementRoundTripThroughJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GameTranslator-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings
            {
                OverlayFontSize = 27,
                OverlayBackgroundOpacity = 0.75,
                OverlayLeft = -620,
                OverlayTop = 180,
                OverlayWidth = 640,
                OverlayHeight = 260,
                OverlayClickThrough = true
            };

            await store.SaveAsync(settings, CancellationToken.None);
            var loaded = store.Load();

            Assert.Equal(27, loaded.OverlayFontSize);
            Assert.Equal(0.75, loaded.OverlayBackgroundOpacity);
            Assert.Equal(-620, loaded.OverlayLeft);
            Assert.Equal(180, loaded.OverlayTop);
            Assert.Equal(640, loaded.OverlayWidth);
            Assert.Equal(260, loaded.OverlayHeight);
            Assert.True(loaded.OverlayClickThrough);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [Fact]
    public void OffScreenPlacementFallsBackToVisibleDesktop()
    {
        var result = OverlayPlacementValidator.EnsureVisible(
            new OverlayPlacement(5000, 5000, 520, 210),
            new DesktopBounds(-1920, 0, 3840, 1080),
            new DesktopBounds(0, 0, 1920, 1040));

        Assert.Equal(700, result.Left);
        Assert.Equal(415, result.Top);
        Assert.Equal(520, result.Width);
        Assert.Equal(210, result.Height);
    }

    [Fact]
    public void NegativeCoordinatesOnConnectedMonitorArePreserved()
    {
        var result = OverlayPlacementValidator.EnsureVisible(
            new OverlayPlacement(-1600, 100, 520, 210),
            new DesktopBounds(-1920, 0, 3840, 1080),
            new DesktopBounds(0, 0, 1920, 1040));

        Assert.Equal(-1600, result.Left);
        Assert.Equal(100, result.Top);
    }
}
