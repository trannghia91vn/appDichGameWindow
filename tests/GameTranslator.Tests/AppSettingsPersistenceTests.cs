using System.IO;
using System.Windows.Input;
using GameTranslator.App.Hotkeys;
using GameTranslator.App.Settings;
using Xunit;

namespace GameTranslator.Tests;

public sealed class AppSettingsPersistenceTests
{
    [Fact]
    public async Task AllUserConfigurableSettingsRoundTripThroughJson()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GameTranslator-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings
            {
                OllamaBaseUrl = "http://127.0.0.1:11434",
                SelectedModel = "translation-model:latest",
                OverlayLeft = -840,
                OverlayTop = 120,
                OverlayWidth = 700,
                OverlayHeight = 280,
                OverlayFontSize = 29,
                OverlayBackgroundOpacity = 0.7,
                OverlayClickThrough = true
            };
            settings.SetTranslationHotkey(
                new HotkeyGesture(Key.T, Ctrl: true, Shift: true));

            await store.SaveAsync(settings, CancellationToken.None);
            var loaded = store.Load();

            Assert.Equal(settings.OllamaBaseUrl, loaded.OllamaBaseUrl);
            Assert.Equal(settings.SelectedModel, loaded.SelectedModel);
            Assert.Equal(settings.OverlayLeft, loaded.OverlayLeft);
            Assert.Equal(settings.OverlayTop, loaded.OverlayTop);
            Assert.Equal(settings.OverlayWidth, loaded.OverlayWidth);
            Assert.Equal(settings.OverlayHeight, loaded.OverlayHeight);
            Assert.Equal(settings.OverlayFontSize, loaded.OverlayFontSize);
            Assert.Equal(settings.OverlayBackgroundOpacity, loaded.OverlayBackgroundOpacity);
            Assert.Equal(settings.OverlayClickThrough, loaded.OverlayClickThrough);
            Assert.Equal(settings.GetTranslationHotkey(), loaded.GetTranslationHotkey());
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }
}
