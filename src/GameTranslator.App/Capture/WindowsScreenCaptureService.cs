using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using GameTranslator.Core;

namespace GameTranslator.App.Capture;

public sealed class WindowsScreenCaptureService : IScreenCaptureService
{
    public Task<CapturedImage> CaptureAsync(
        ScreenRegion region,
        CancellationToken cancellationToken)
    {
        region.ThrowIfInvalid();
        cancellationToken.ThrowIfCancellationRequested();

        return Task.Run(() => Capture(region, cancellationToken), cancellationToken);
    }

    private static CapturedImage Capture(
        ScreenRegion region,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        using var bitmap = new Bitmap(region.Width, region.Height, PixelFormat.Format32bppArgb);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.CopyFromScreen(
                region.X,
                region.Y,
                0,
                0,
                new Size(region.Width, region.Height),
                CopyPixelOperation.SourceCopy);
        }

        cancellationToken.ThrowIfCancellationRequested();

        using var stream = new MemoryStream();
        bitmap.Save(stream, ImageFormat.Png);
        stopwatch.Stop();

        return new CapturedImage(
            stream.ToArray(),
            region.Width,
            region.Height,
            stopwatch.Elapsed);
    }
}
