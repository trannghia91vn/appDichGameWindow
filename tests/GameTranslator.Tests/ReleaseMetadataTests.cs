using GameTranslator.App.ViewModels;
using Xunit;

namespace GameTranslator.Tests;

public sealed class ReleaseMetadataTests
{
    [Fact]
    public void ApplicationVersionIsDisplayedAsOneOneTwo()
    {
        var assemblyVersion = typeof(MainWindowViewModel).Assembly.GetName().Version;

        Assert.NotNull(assemblyVersion);
        Assert.Equal(new Version(1, 1, 2, 0), assemblyVersion);
        Assert.Equal("v1.1.2", new MainWindowViewModel().VersionText);
    }
}
