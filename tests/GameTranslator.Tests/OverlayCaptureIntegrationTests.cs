using System.Net.Http;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using GameTranslator.App.Capture;
using GameTranslator.App.Ocr;
using GameTranslator.App.Overlay;
using GameTranslator.App.Translation;
using GameTranslator.Core;
using Xunit;
using Xunit.Abstractions;

namespace GameTranslator.Tests;

public sealed class OverlayCaptureIntegrationTests(ITestOutputHelper output)
{
    [Fact]
    public async Task OverlayDoesNotContaminateRealCaptureOcrAndTranslation()
    {
        if (!string.Equals(
                Environment.GetEnvironmentVariable("GAMETRANSLATOR_OVERLAY_SMOKE"),
                "1",
                StringComparison.Ordinal))
        {
            return;
        }

        var completion = new TaskCompletionSource<SmokeResult>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() => RunSmokeTestAsync(completion))
        {
            IsBackground = true,
            Name = "GameTranslator overlay smoke test"
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();

        var result = await completion.Task.WaitAsync(TimeSpan.FromSeconds(45));
        thread.Join(TimeSpan.FromSeconds(3));

        output.WriteLine(
            "WDA={0}; click-through on/off={1}/{2}; last hide={3:F1} ms",
            result.CaptureExclusionEnabled,
            result.ClickThroughEnabled,
            result.ClickThroughDisabled,
            result.HideDuration.TotalMilliseconds);
        for (var index = 0; index < result.PipelineResults.Count; index++)
        {
            var pipelineResult = result.PipelineResults[index];
            output.WriteLine(
                "#{0}: capture={1:F1} ms; OCR={2:F1} ms; translation={3:F1} ms; total={4:F1} ms; cache={5}",
                index + 1,
                pipelineResult.CapturedImage.CaptureDuration.TotalMilliseconds,
                pipelineResult.Ocr.ProcessingDuration.TotalMilliseconds,
                pipelineResult.Translation?.Duration.TotalMilliseconds ?? 0,
                pipelineResult.TotalDuration.TotalMilliseconds,
                pipelineResult.Translation?.FromCache == true ? "HIT" : "MISS");
            output.WriteLine("OCR: {0}", pipelineResult.Ocr.Text);
            output.WriteLine("Vietnamese: {0}", pipelineResult.Translation?.TranslatedText);
        }

        Assert.True(result.CaptureExclusionEnabled);
        Assert.True(result.ClickThroughEnabled);
        Assert.True(result.ClickThroughDisabled);
        Assert.Equal(11, result.PipelineResults.Count);
        foreach (var pipelineResult in result.PipelineResults)
        {
            Assert.DoesNotContain("OVERLAY", pipelineResult.Ocr.Text, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(pipelineResult.Translation);
            Assert.False(string.IsNullOrWhiteSpace(pipelineResult.Translation!.TranslatedText));
        }

        Assert.Contains(
            "LEAVE",
            result.PipelineResults[0].Ocr.Text,
            StringComparison.OrdinalIgnoreCase);
        Assert.False(result.PipelineResults[0].Translation!.FromCache);
        Assert.True(result.PipelineResults[^1].Translation!.FromCache);
    }

    private static void RunSmokeTestAsync(TaskCompletionSource<SmokeResult> completion)
    {
        var dispatcher = Dispatcher.CurrentDispatcher;
        dispatcher.InvokeAsync(async () =>
        {
            Window? gameWindow = null;
            TranslationOverlayWindow? overlay = null;
            RapidOcrService? ocr = null;
            HttpClient? httpClient = null;

            try
            {
                gameWindow = CreateGameWindow();
                gameWindow.Show();
                await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);

                overlay = new TranslationOverlayWindow
                {
                    WindowStartupLocation = WindowStartupLocation.Manual,
                    Left = gameWindow.Left,
                    Top = gameWindow.Top,
                    Width = gameWindow.ActualWidth,
                    Height = gameWindow.ActualHeight
                };
                overlay.ApplyVisualSettings(28, 0.95);
                overlay.ShowTranslation("ĐÂY LÀ OVERLAY TEST");
                await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);

                overlay.SetClickThrough(true);
                var clickThroughEnabled = overlay.IsClickThroughApplied;
                overlay.SetClickThrough(false);
                var clickThroughDisabled = !overlay.IsClickThroughApplied;

                var topLeft = gameWindow.PointToScreen(new Point(0, 0));
                var bottomRight = gameWindow.PointToScreen(
                    new Point(gameWindow.ActualWidth, gameWindow.ActualHeight));
                var region = new ScreenRegion(
                    (int)Math.Round(topLeft.X),
                    (int)Math.Round(topLeft.Y),
                    (int)Math.Round(bottomRight.X - topLeft.X),
                    (int)Math.Round(bottomRight.Y - topLeft.Y));

                var capture = new WindowsScreenCaptureService();
                ocr = new RapidOcrService();
                httpClient = new HttpClient { Timeout = OllamaOptions.RequestTimeout };
                var translation = new OllamaTranslationService(
                    httpClient,
                    () => OllamaOptions.DefaultBaseUrl);
                var pipeline = new TranslationPipeline(
                    capture,
                    ocr,
                    translation,
                    new ExactTranslationCache());
                var coordinator = new OverlayTranslationCoordinator(
                    pipeline,
                    overlay,
                    exception => exception is OllamaException ollama
                        ? ollama.UserMessage
                        : "Không thể dịch nội dung này.");

                var gameText = (TextBlock)((Border)gameWindow.Content).Child;
                string[] sources =
                [
                    "WE NEED TO LEAVE BEFORE SUNRISE.",
                    "THE GATE IS LOCKED.",
                    "FIND THE SILVER KEY.",
                    "DO NOT MAKE A SOUND.",
                    "MEET ME AT THE OLD BRIDGE.",
                    "YOU HAVE THREE POTIONS LEFT.",
                    "THE ENEMY IS APPROACHING.",
                    "SAVE YOUR STRENGTH FOR BATTLE.",
                    "THIS PATH LEADS TO THE VILLAGE.",
                    "THANK YOU FOR SAVING MY FAMILY."
                ];
                var pipelineResults = new List<TranslationPipelineResult>();
                foreach (var source in sources)
                {
                    gameText.Text = source;
                    await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                    var result = await coordinator.ExecuteAsync(
                        region,
                        OllamaOptions.PreferredModel,
                        CancellationToken.None);
                    if (!string.Equals(
                            overlay.DisplayedText,
                            result.Translation?.TranslatedText,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("Overlay did not display the pipeline result.");
                    }

                    pipelineResults.Add(result);
                }

                gameText.Text = sources[0];
                await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ApplicationIdle);
                pipelineResults.Add(await coordinator.ExecuteAsync(
                    region,
                    OllamaOptions.PreferredModel,
                    CancellationToken.None));
                completion.SetResult(new SmokeResult(
                    pipelineResults,
                    overlay.CaptureExclusionEnabled,
                    overlay.LastHideForCaptureDuration,
                    clickThroughEnabled,
                    clickThroughDisabled));
            }
            catch (Exception ex)
            {
                completion.SetException(ex);
            }
            finally
            {
                overlay?.Shutdown();
                gameWindow?.Close();
                ocr?.Dispose();
                httpClient?.Dispose();
                dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
            }
        });

        Dispatcher.Run();
    }

    private static Window CreateGameWindow()
    {
        var workArea = SystemParameters.WorkArea;
        return new Window
        {
            Title = "GameTranslator overlay smoke background",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            Topmost = true,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Left = workArea.Left + 80,
            Top = workArea.Top + 80,
            Width = 760,
            Height = 230,
            Background = Brushes.White,
            Content = new Border
            {
                Background = Brushes.White,
                Padding = new Thickness(24),
                Child = new TextBlock
                {
                    Text = "WE NEED TO LEAVE BEFORE SUNRISE.",
                    Foreground = Brushes.Black,
                    Background = Brushes.White,
                    FontFamily = new FontFamily("Segoe UI"),
                    FontSize = 42,
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center
                }
            }
        };
    }

    private sealed record SmokeResult(
        IReadOnlyList<TranslationPipelineResult> PipelineResults,
        bool CaptureExclusionEnabled,
        TimeSpan HideDuration,
        bool ClickThroughEnabled,
        bool ClickThroughDisabled);
}
