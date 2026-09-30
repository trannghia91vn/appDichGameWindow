# Product

GameTranslator is a local English-to-Vietnamese game screen translator for Windows.

The user selects one rectangular region on the desktop or game screen. The app does not watch that region continuously. It does not poll frames, run OCR in the background, detect frame similarity, or trigger translation automatically.

Translation is manual-trigger-only:

```text
Selected screen region
-> capture one image
-> OCR English text
-> translate to Vietnamese with local Ollama
-> show Vietnamese text in an overlay
```

The application is local-first and does not use cloud APIs. English OCR and English-to-Vietnamese translation will run locally. Low latency, reliability, low idle resource use, and easy debugging are primary goals.

Through Phase 5, region selection, one-shot capture, screenshot preview, local English OCR, exact translation caching, local Ollama English-to-Vietnamese translation, the floating translation overlay, and one configurable global translation hotkey are implemented. Translation remains explicit user-trigger-only whether it starts from a button or the hotkey.
