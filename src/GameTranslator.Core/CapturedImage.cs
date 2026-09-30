namespace GameTranslator.Core;

public sealed record CapturedImage
{
    public CapturedImage(byte[] pngBytes, int width, int height, TimeSpan captureDuration)
    {
        ArgumentNullException.ThrowIfNull(pngBytes);

        if (pngBytes.Length == 0)
        {
            throw new ArgumentException("PNG data cannot be empty.", nameof(pngBytes));
        }

        if (width <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Width must be greater than zero.");
        }

        if (height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Height must be greater than zero.");
        }

        if (captureDuration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(captureDuration));
        }

        PngBytes = pngBytes;
        Width = width;
        Height = height;
        CaptureDuration = captureDuration;
    }

    public byte[] PngBytes { get; }

    public int Width { get; }

    public int Height { get; }

    public string MediaType => "image/png";

    public TimeSpan CaptureDuration { get; }

    public void ThrowIfInvalid()
    {
        if (Width <= 0 || Height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(CapturedImage), "Captured image dimensions are invalid.");
        }

        if (PngBytes.Length == 0)
        {
            throw new ArgumentException("Captured image has no PNG data.", nameof(PngBytes));
        }
    }
}
