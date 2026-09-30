namespace GameTranslator.App.Overlay;

public readonly record struct OverlayPlacement(
    double Left,
    double Top,
    double Width,
    double Height);

public readonly record struct DesktopBounds(
    double Left,
    double Top,
    double Width,
    double Height);

public static class OverlayPlacementValidator
{
    public const double MinimumWidth = 320;
    public const double MinimumHeight = 140;

    public static OverlayPlacement EnsureVisible(
        OverlayPlacement requested,
        DesktopBounds virtualDesktop,
        DesktopBounds fallbackDesktop)
    {
        var width = IsPositiveFinite(requested.Width)
            ? Math.Max(MinimumWidth, Math.Min(requested.Width, virtualDesktop.Width))
            : Math.Min(520, virtualDesktop.Width);
        var height = IsPositiveFinite(requested.Height)
            ? Math.Max(MinimumHeight, Math.Min(requested.Height, virtualDesktop.Height))
            : Math.Min(210, virtualDesktop.Height);

        var hasFinitePosition = double.IsFinite(requested.Left) && double.IsFinite(requested.Top);
        var intersectsVirtualDesktop = hasFinitePosition &&
            requested.Left < virtualDesktop.Left + virtualDesktop.Width &&
            requested.Left + width > virtualDesktop.Left &&
            requested.Top < virtualDesktop.Top + virtualDesktop.Height &&
            requested.Top + height > virtualDesktop.Top;

        if (intersectsVirtualDesktop)
        {
            return new OverlayPlacement(requested.Left, requested.Top, width, height);
        }

        var left = fallbackDesktop.Left + Math.Max(0, (fallbackDesktop.Width - width) / 2);
        var top = fallbackDesktop.Top + Math.Max(0, (fallbackDesktop.Height - height) / 2);
        return new OverlayPlacement(left, top, width, height);
    }

    private static bool IsPositiveFinite(double value) =>
        double.IsFinite(value) && value > 0;
}
