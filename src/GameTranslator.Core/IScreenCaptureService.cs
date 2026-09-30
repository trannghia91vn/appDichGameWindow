namespace GameTranslator.Core;

public interface IScreenCaptureService
{
    Task<CapturedImage> CaptureAsync(ScreenRegion region, CancellationToken cancellationToken);
}
