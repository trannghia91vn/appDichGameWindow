namespace GameTranslator.Core;

public sealed class CaptureSession
{
    private readonly IScreenCaptureService screenCaptureService;
    private readonly SemaphoreSlim captureGate = new(1, 1);

    public CaptureSession(IScreenCaptureService screenCaptureService)
    {
        this.screenCaptureService = screenCaptureService;
    }

    public ScreenRegion? SelectedRegion { get; private set; }

    public CapturedImage? LastCapturedImage { get; private set; }

    public bool ApplySelection(ScreenRegion? selectedRegion)
    {
        if (selectedRegion is null)
        {
            return false;
        }

        selectedRegion.Value.ThrowIfInvalid();
        SelectedRegion = selectedRegion;
        return true;
    }

    public async Task<CapturedImage> CaptureOnceAsync(CancellationToken cancellationToken)
    {
        var region = SelectedRegion
            ?? throw new InvalidOperationException("A screen region must be selected before capture.");

        if (!await captureGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new CaptureAlreadyInProgressException();
        }

        try
        {
            var image = await screenCaptureService.CaptureAsync(region, cancellationToken).ConfigureAwait(false);
            image.ThrowIfInvalid();
            LastCapturedImage = image;
            return image;
        }
        finally
        {
            captureGate.Release();
        }
    }
}
