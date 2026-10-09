# GameTranslator

GameTranslator is a small Windows-only desktop app for local English-to-Vietnamese game screen translation. It uses local screen capture, RapidOcrNet, and a local Ollama model.

Select a region and use `DỊCH`/the global hotkey for one translation, or explicitly enable `Realtime` in the overlay to translate when recognized text changes. Realtime is off by default.

## Portable Release Requirements

- Windows 10/11 x64
- Ollama installed and running at `http://localhost:11434`
- At least one local translation-capable model

The portable release is self-contained and does not require .NET to be installed. Extract `GameTranslator-win-x64.zip`, then run `GameTranslator.exe`. Ollama and its models are intentionally not bundled.

Recommended model:

```powershell
ollama pull translategemma:4b
```

GameTranslator lists installed models but never downloads one automatically.

## Development Requirements

- .NET 10 SDK
- NuGet access for package restore

## Commands

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src\GameTranslator.App\GameTranslator.App.csproj
```

If this workspace-local SDK is present, use:

```powershell
.\.dotnet\dotnet.exe build
.\.dotnet\dotnet.exe test
.\.dotnet\dotnet.exe run --project src\GameTranslator.App\GameTranslator.App.csproj
```

Create and verify the portable release with:

```powershell
.\scripts\publish-win-x64.ps1
```

The script produces `artifacts\publish\win-x64\` and `artifacts\GameTranslator-win-x64.zip` after Release build, tests, and runtime-file validation succeed.

Install or overwrite the current-user copy and create Desktop/Start Menu shortcuts with:

```powershell
.\scripts\install-current-user.ps1
```

The installed executable is `%LOCALAPPDATA%\Programs\GameTranslator\GameTranslator.exe`. Personal settings remain in `%LOCALAPPDATA%\GameTranslator\settings.json`.

## Current Status

Phase 0 through Phase 8 are implemented. Version 1.1.2 is distributed as a self-contained Windows x64 folder and portable ZIP. OCR uses RapidOcrNet 4.2.0 and bundled PP-OCRv5 Latin models on two CPU inference threads. Translation uses local Ollama `/api/chat`, and successful results are cached in memory by exact normalized OCR text plus model. A lightweight always-on-top overlay displays Vietnamese while MainWindow remains the diagnostic/control surface. The configurable global translation hotkey defaults to `F8`.

## Usage

1. Run GameTranslator.
2. Click `Chọn vùng`.
3. Drag over an English dialogue area.
4. Click `Chụp thử` to verify the captured pixels without OCR.
5. Confirm Ollama is connected and select an installed model.
6. Click `DỊCH` to capture once, run OCR once, and translate once on a cache miss.
7. Review the screenshot, English text, Vietnamese translation, timings, and cache status.

The same translation command can be started with the global hotkey while the game has focus or MainWindow is minimized. Use `Đổi phím` in MainWindow, press one valid key combination, or press `Escape` to cancel. `Khôi phục mặc định` restores `F8`. The app keeps the previous registered shortcut when a replacement is unavailable.

## Overlay Usage

1. Select the game dialogue region in MainWindow.
2. Click `Hiện Overlay`.
3. Drag and resize the overlay to a convenient position.
4. When dialogue appears, click `DỊCH` in the overlay.
5. Read the Vietnamese result while the overlay stays above the game.

For automatic updates, turn the overlay `Realtime` switch ON. The app observes the selected region sequentially and translates only when normalized OCR text changes. Polling adapts from 1.5 to 3 seconds while text remains unchanged, OCR is limited to two CPU threads, and the process runs below normal priority during realtime. Turn the switch OFF to stop realtime and use `DỊCH` or the global hotkey manually again. Hiding the overlay also stops realtime.

The overlay hides only for the screenshot, then returns immediately with a processing state while OCR and Ollama continue. Font size, background opacity, position, dimensions, and the global hotkey persist between sessions. Optional click-through defaults off; while it is enabled, use the global hotkey to translate without interacting with the overlay.

The Ollama base URL and selected model are stored in `%LocalAppData%\GameTranslator\settings.json`. Translation is local-only; no cloud API is used.
