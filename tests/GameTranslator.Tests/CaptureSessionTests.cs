using System.Reflection;
using GameTranslator.Core;
using Xunit;

namespace GameTranslator.Tests;

public sealed class CaptureSessionTests
{
    [Fact]
    public void CancelledSelectionPreservesPreviousRegion()
    {
        var session = new CaptureSession(new FakeCaptureService());
        var previous = new ScreenRegion(-1500, 200, 900, 300);
        session.ApplySelection(previous);

        var changed = session.ApplySelection(null);

        Assert.False(changed);
        Assert.Equal(previous, session.SelectedRegion);
    }

    [Fact]
    public async Task CaptureWithoutRegionDoesNotCallService()
    {
        var service = new FakeCaptureService();
        var session = new CaptureSession(service);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.CaptureOnceAsync(CancellationToken.None));

        Assert.Equal(0, service.CallCount);
    }

    [Fact]
    public async Task CaptureActionCallsServiceExactlyOnce()
    {
        var service = new FakeCaptureService();
        var session = new CaptureSession(service);
        session.ApplySelection(new ScreenRegion(10, 20, 300, 100));

        var image = await session.CaptureOnceAsync(CancellationToken.None);

        Assert.Equal(1, service.CallCount);
        Assert.Same(service.Result, image);
        Assert.Same(image, session.LastCapturedImage);
    }

    [Fact]
    public async Task ConcurrentCaptureIsRejectedWithoutQueueing()
    {
        var service = new BlockingCaptureService();
        var session = new CaptureSession(service);
        session.ApplySelection(new ScreenRegion(10, 20, 300, 100));

        var firstCapture = session.CaptureOnceAsync(CancellationToken.None);
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));

        await Assert.ThrowsAsync<CaptureAlreadyInProgressException>(
            () => session.CaptureOnceAsync(CancellationToken.None));

        service.Release.SetResult();
        await firstCapture;
        Assert.Equal(1, service.CallCount);
    }

    [Fact]
    public async Task FailedCapturePreservesPreviousImageAndRegion()
    {
        var service = new FakeCaptureService();
        var session = new CaptureSession(service);
        var region = new ScreenRegion(-400, -100, 320, 180);
        session.ApplySelection(region);
        var previousImage = await session.CaptureOnceAsync(CancellationToken.None);
        service.ExceptionToThrow = new InvalidOperationException("monitor disconnected");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => session.CaptureOnceAsync(CancellationToken.None));

        Assert.Equal(region, session.SelectedRegion);
        Assert.Same(previousImage, session.LastCapturedImage);
    }

    [Fact]
    public void CaptureSessionContainsNoPollingTimer()
    {
        var timerTypes = new[]
        {
            typeof(System.Threading.Timer),
            typeof(System.Timers.Timer),
            typeof(PeriodicTimer)
        };
        var fields = typeof(CaptureSession)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        Assert.DoesNotContain(fields, field =>
            timerTypes.Any(timerType => timerType.IsAssignableFrom(field.FieldType)));
    }

    private sealed class FakeCaptureService : IScreenCaptureService
    {
        public CapturedImage Result { get; } = new([1, 2, 3], 300, 100, TimeSpan.FromMilliseconds(5));

        public Exception? ExceptionToThrow { get; set; }

        public int CallCount { get; private set; }

        public Task<CapturedImage> CaptureAsync(
            ScreenRegion region,
            CancellationToken cancellationToken)
        {
            CallCount++;
            if (ExceptionToThrow is not null)
            {
                throw ExceptionToThrow;
            }

            return Task.FromResult(Result);
        }
    }

    private sealed class BlockingCaptureService : IScreenCaptureService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CallCount { get; private set; }

        public async Task<CapturedImage> CaptureAsync(
            ScreenRegion region,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Started.SetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return new CapturedImage([1], region.Width, region.Height, TimeSpan.FromMilliseconds(5));
        }
    }
}
