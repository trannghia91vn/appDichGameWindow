using GameTranslator.App.ViewModels;
using Xunit;

namespace GameTranslator.Tests;

public sealed class ReleaseMetadataTests
{
    [Fact]
    public void ApplicationVersionIsDisplayedAsOneZeroZero()
    {
        var assemblyVersion = typeof(MainWindowViewModel).Assembly.GetName().Version;

        Assert.NotNull(assemblyVersion);
        Assert.Equal(new Version(1, 0, 0, 0), assemblyVersion);
        Assert.Equal("v1.0.0", new MainWindowViewModel().VersionText);
    }
}
