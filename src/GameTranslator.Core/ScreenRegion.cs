namespace GameTranslator.Core;

public readonly record struct ScreenRegion
{
    public ScreenRegion(int x, int y, int width, int height)
    {
        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public int X { get; }

    public int Y { get; }

    public int Width { get; }

    public int Height { get; }

    public bool IsValid => Width > 0 && Height > 0;

    public void ThrowIfInvalid()
    {
        if (!IsValid)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ScreenRegion),
                "Width and height must be greater than zero.");
        }
    }

    public override string ToString() => $"X={X}, Y={Y}, Width={Width}, Height={Height}";
}
