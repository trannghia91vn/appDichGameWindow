# Plan

## Phase 0 - Bootstrap

Status: complete.

- Created solution structure.
- Added WPF app project, core project, and test project.
- Added concise `AGENTS.md`, `README.md`, and docs.
- Targeted Windows x64 and .NET 10.

## Phase 1 - Region selection + one-shot capture

Status: complete.

- Added virtual-desktop region selection with physical screen coordinates.
- Added one-shot GDI capture, in-memory PNG preview, timing, cancellation behavior, and concurrency protection.

## Phase 2 - English OCR

Status: complete.

- Added RapidOcrNet 4.2.0 with bundled PP-OCRv5 Latin models.
- Added lazy one-time engine initialization and in-memory PNG-to-SKBitmap OCR.
- Added one-shot capture/OCR pipeline, conservative normalization, preview, and timing diagnostics.
- Added unit and real model integration coverage, including repeated OCR, changed text, and no-text input.

## Phase 3 - Ollama translation

Status: complete.

- Added local Ollama health/model discovery through `GET /api/tags`.
- Added persisted base URL/model selection with `translategemma:4b` preferred.
- Added deterministic TranslateGemma and generic prompts through `POST /api/chat`.
- Added exact normalized-text-plus-model memory cache and translation timing diagnostics.
- Verified real `translategemma:4b` output across 10 different source sentences.

## Phase 4 - Translation overlay

Status: complete.

- Added a draggable, resizable, always-on-top WPF translation overlay.
- Reused the Phase 3 pipeline and cache from both MainWindow and overlay commands.
- Added hide/dispatcher/DWM synchronization around capture plus `WDA_EXCLUDEFROMCAPTURE` fallback protection.
- Added persisted font size, background opacity, position, dimensions, and optional click-through mode.
- Verified WDA and click-through on Windows and ran 10 distinct overlay capture/OCR/Ollama translations without overlay contamination; an identical repeat returned an exact-cache hit.

## Phase 5 - Global translation hotkey

Status: complete.

- Added one configurable system-wide translation hotkey using Win32 `RegisterHotKey`/`WM_HOTKEY`; the default is `F8`.
- Routed MainWindow, overlay, and hotkey triggers through one cancellation-aware translation command with no queue for repeated presses.
- Added shortcut capture, validation, persistence, reset, conflict reporting, and rollback to the previous registration.
- Verified real Win32 registration, conflict handling, release/re-registration, event delivery, persistence, and unchanged foreground focus.

## Phase 6 - Packaging and release

Status: complete.

- Set consistent product, assembly, and file version metadata to 1.0.0 and applied the existing app icon to the release executable/window.
- Added a reproducible clean Release pipeline for restore, build, tests, self-contained win-x64 publish, runtime/model validation, and portable ZIP creation.
- Bundled the PP-OCRv5 assets and required WPF, RapidOcrNet, ONNX Runtime, SkiaSharp, native, and .NET runtime files without trimming or single-file publishing.
- Added a concise end-user release README and kept writable settings under `%LOCALAPPDATA%/GameTranslator`.
- Verified startup from a ZIP-only extraction with package-local .NET, offline/missing-model startup, Release OCR/Ollama/overlay workflow, exact-cache reuse, and global-hotkey registration/focus behavior.

