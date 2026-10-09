namespace GameTranslator.App.Overlay;

public static class OverlayTextFormatter
{
    public static string Format(string text) =>
        string.Join(" ", text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
