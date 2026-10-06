# Architecture

## Dependency Direction

```text
GameTranslator.App
        |
        v
GameTranslator.Core
```

`GameTranslator.Core` contains product models and contracts:

- `ScreenRegion`
- `CapturedImage`
- `IScreenCaptureService`
- `IOcrService`
- `ITranslationService`
- `ITranslationPipeline`
- `CaptureSession`
- `OcrPipeline`
- `TranslationPipeline`
- `ExactTranslationCache`

`GameTranslator.App` contains the WPF shell and Windows/provider-specific implementations: `RegionSelectorWindow`, `TranslationOverlayWindow`, `WindowsScreenCaptureService`, `RapidOcrService`, `OllamaTranslationService`, the global-hotkey services, and local JSON settings.

## Translation Pipelines

The shared translation pipeline is:

```text
ScreenRegion
    -> IScreenCaptureService
    -> CapturedImage (PNG bytes in memory)
    -> IOcrService / RapidOcrService
    -> conservatively normalized English text
    -> ExactTranslationCache
    -> ITranslationService / OllamaTranslationService on cache miss
    -> Vietnamese text
```

One explicit manual translation command calls capture once, OCR once, and performs one exact cache lookup. A cache miss makes one logical translation call. `Chụp thử` calls capture once without OCR or translation. Manual paths reject overlapping work rather than queueing it.

MainWindow `DỊCH`, overlay `DỊCH`, and the global hotkey all execute one `TranslationCommand` backed by the same `TranslationPipeline` instance and in-memory cache. When the overlay is visible, `OverlayTranslationCoordinator` runs this order:

```text
explicit translation command
    -> show immediate processing state
    -> hide overlay
    -> flush desktop composition
    -> capture one frame
    -> show overlay with "Đang dịch..."
    -> OCR
    -> exact cache / Ollama
    -> show Vietnamese
```

`TranslationPipeline` exposes a capture-completed callback after the screenshot is fully materialized and before OCR. This keeps the core independent of WPF while allowing the overlay to remain hidden only during capture.

When the user explicitly enables the overlay `Realtime` switch, `RealtimeTranslationController` runs the same pipeline in a cancellation-aware sequential loop. Each iteration must complete before the controller waits 850 ms and starts another. `TranslationPipeline.TranslateIfChangedAsync` compares normalized OCR text with the previous observation and skips cache/Ollama work when unchanged. Blank observations are remembered so the same dialogue is translated again if it disappears and later returns.

Realtime defaults off, never starts with the application, stops when the switch is turned off or the overlay is hidden, and disables manual translation controls while active. There is no overlapping capture/OCR/translation, work queue, frame-similarity detector, or separate background OCR worker.

## Region Coordinates

`ScreenRegion` stores physical screen pixels:

- `X`
- `Y`
- `Width`
- `Height`

The app declares Per-Monitor-V2 DPI awareness in `app.manifest`. `RegionSelectorWindow` spans the virtual desktop using Win32 virtual-screen metrics and stores cursor positions from `GetCursorPos`, which are physical screen pixels. WPF DIP conversion is isolated in `PhysicalScreenCoordinateMapper` and is used only to draw the selection rectangle. Capture therefore does not assume one DIP equals one physical pixel. Negative X and Y values are preserved for monitors left of or above the primary display.

## One-Shot Capture

`WindowsScreenCaptureService` uses `Graphics.CopyFromScreen` once per request and encodes the result as PNG bytes in memory. It does not write screenshots to disk or keep a capture session running. The current approach targets desktop apps and windowed or borderless-windowed games. Exclusive fullscreen games may require a future Desktop Duplication fallback.

## English OCR

`RapidOcrService` uses RapidOcrNet 4.2.0 with the bundled PP-OCRv5 Latin detector, classifier, recognizer, and dictionary. Model paths are resolved from `AppContext.BaseDirectory/models/v5`, so runtime does not depend on the process working directory or a NuGet cache path.

Initialization is lazy and represented by one cached task per application service instance. The initialized `RapidOcr` engine is reused for all requests, while a semaphore serializes inference. RapidOcrNet runs with its default CPU provider. There is no startup capture and no warm-up image.

Captured PNG bytes are decoded directly to `SKBitmap`; no temporary image file is created. `DetectAsync` keeps CPU work off the WPF UI thread and accepts cancellation. The OCR output keeps block line breaks, then `OcrTextNormalizer` trims outer/trailing whitespace, normalizes line endings, and collapses repeated blank lines without spelling correction or punctuation changes.

The normalized English text is the source passed to the exact translation cache and local Ollama translation.

## Local Ollama Translation

`OllamaTranslationService` uses one reusable `HttpClient` with a 25-second timeout. The base URL defaults to `http://localhost:11434` and can be edited in MainWindow. Startup remains usable when Ollama is offline.

```text
GET  /api/tags  -> connection check and installed model discovery
POST /api/chat  -> one English-to-Vietnamese translation
```

Chat requests use one user message, `stream:false`, `keep_alive:-1`, and top-level `think:false`. Generation options are fixed at temperature 0, context size 4096, and at most 256 output tokens. If an older Ollama/model rejects `think`, the service retries that logical request once without the property.

Models whose names start with `translategemma` receive the dedicated English-to-Vietnamese TranslateGemma prompt, including exactly two blank lines before source text. Other models receive a short generic game-dialogue prompt. Neither path requests JSON, reasoning, chat history, or explanations.

The selected model and Ollama base URL are saved to `%LocalAppData%/GameTranslator/settings.json`. The preferred model is `translategemma:4b`; the app never downloads models.

## Exact Translation Cache

`ExactTranslationCache` is memory-only. Its key is normalized OCR text plus the exact selected model name. Punctuation changes and model changes are cache misses. There is no fuzzy, edit-distance, semantic, or frame-similarity matching. Failed, cancelled, and empty translations are not cached.

## Translation Overlay

`TranslationOverlayWindow` is a borderless, always-on-top WPF window with a draggable header, resize grip, wrapped/scrollable Vietnamese text, processing state, manual `DỊCH`, an opt-in `Realtime` switch, hide, and close-as-hide controls. It does not own MainWindow, so MainWindow can remain minimized while the overlay stays visible. Manual `DỊCH` is disabled only while realtime is active.

Two independent layers prevent overlay pixels from contaminating OCR:

1. The coordinator temporarily hides the overlay, yields through the WPF dispatcher, and calls `DwmFlush` before screen capture. It shows the overlay immediately after capture, before OCR and translation.
2. After the overlay HWND exists, `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)` is applied. Failure is logged and does not stop the hide-before-capture path.

The optional click-through mode preserves existing extended window styles and toggles `WS_EX_LAYERED | WS_EX_TRANSPARENT` with architecture-safe `GetWindowLongPtr`/`SetWindowLongPtr` calls. It defaults off; while enabled, the global hotkey remains available without focusing the overlay.

Font size, background opacity, click-through mode, position, and dimensions share the Phase 3 JSON settings file. Text remains opaque while only the background brush alpha changes. Saved placement is checked against the current virtual desktop and centered back onto the primary work area when completely off-screen. Closing the overlay hides and reuses it; closing MainWindow shuts it down.

## Global Translation Hotkey

`GlobalHotkeyService` attaches an `HwndSource` hook to MainWindow and uses Win32 `RegisterHotKey`/`UnregisterHotKey` with `MOD_NOREPEAT`. It handles only `WM_HOTKEY`; there is no keyboard hook, timer, polling loop, or background key scanner. The registration stays active while MainWindow is minimized and does not activate either application window.

The default gesture is `F8`. One configurable key plus Ctrl, Alt, Shift, and Win modifiers is stored in the existing JSON settings file. Capture mode accepts the next valid combination, treats `Escape` as cancel, and rejects modifier-only input and reserved Windows combinations.

`HotkeyRegistrationManager` unregisters the current gesture before attempting a replacement. If registration fails because another application owns the combination, it restores the previous registration and does not persist the failed value. Shutdown unregisters the active hotkey and removes the HWND hook.

Every trigger enters the same atomic `TranslationCommand` gate. Repeated presses while capture, OCR, or translation is active are ignored instead of queued.

## Deployment

Version 1.1.0 targets Windows 10/11 x64 and publishes as a normal self-contained .NET folder. Trimming, NativeAOT, and single-file publishing are disabled so WPF, RapidOcrNet, ONNX Runtime, SkiaSharp, native DLLs, and OCR assets keep their validated deployment layout.

The release resolves OCR models only from `models/v5` under `AppContext.BaseDirectory`. The publish verification script requires all four PP-OCRv5 model/dictionary files, RapidOcrNet, ONNX Runtime, SkiaSharp, WPF, and self-contained .NET host/runtime files before producing the portable ZIP. It also rejects developer paths and source/test entries in the package.

Ollama remains an external local dependency at the configurable loopback URL; it and its models are not bundled. Screenshots, OCR text, and translations are not sent to cloud services.

Writable settings remain under `%LOCALAPPDATA%/GameTranslator/settings.json`, independent of the installation directory. Runtime logging uses `Trace` only and does not create unbounded log files beside the executable.
