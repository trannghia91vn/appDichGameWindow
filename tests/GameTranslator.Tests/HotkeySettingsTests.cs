using System.IO;
using System.Windows.Input;
using GameTranslator.App.Hotkeys;
using GameTranslator.App.Settings;
using Xunit;

namespace GameTranslator.Tests;

public sealed class HotkeySettingsTests
{
    [Fact]
    public void DefaultSettingsUseF8()
    {
        Assert.Equal(HotkeyGesture.Default, new AppSettings().GetTranslationHotkey());
    }

    [Fact]
    public async Task CustomHotkeySavesAndLoads()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"GameTranslator-{Guid.NewGuid():N}");
        var path = Path.Combine(directory, "settings.json");

        try
        {
            var store = new JsonSettingsStore(path);
            var settings = new AppSettings();
            settings.SetTranslationHotkey(new HotkeyGesture(Key.T, Ctrl: true, Shift: true));

            await store.SaveAsync(settings, CancellationToken.None);
            var loaded = store.Load();

            Assert.Equal(
                new HotkeyGesture(Key.T, Ctrl: true, Shift: true),
                loaded.GetTranslationHotkey());
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
    public void ResetRestoresF8()
    {
        var settings = new AppSettings();
        settings.SetTranslationHotkey(new HotkeyGesture(Key.T, Ctrl: true));

        settings.ResetTranslationHotkey();

        Assert.Equal(HotkeyGesture.Default, settings.GetTranslationHotkey());
    }
}
