# Product

GameTranslator is a local English-to-Vietnamese game screen translator for Windows.

The user selects one rectangular region on the desktop or game screen. Translation can run once from an explicit command, or repeatedly while the user has explicitly enabled the overlay `Realtime` switch. Realtime is off by default and stops when the switch is disabled or the overlay is hidden.

Manual mode:

```text
Selected screen region
-> capture one image
-> OCR English text
-> translate to Vietnamese with local Ollama
-> show Vietnamese text in an overlay
```

Realtime mode uses the same local pipeline sequentially. It captures and OCRs the selected region, translates only when normalized OCR text changes, then waits before the next observation. Iterations never overlap or queue, and there is no frame-similarity detector or startup background worker.

The application is local-first and does not use cloud APIs. English OCR and English-to-Vietnamese translation will run locally. Low latency, reliability, low idle resource use, and easy debugging are primary goals.

Through Phase 7, region selection, one-shot capture, screenshot preview, local English OCR, exact translation caching, local Ollama English-to-Vietnamese translation, the floating translation overlay, one configurable global translation hotkey, and opt-in realtime overlay translation are implemented.
