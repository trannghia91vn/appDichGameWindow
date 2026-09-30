# GameTranslator Agent Guide

GameTranslator is a Windows-only C#/.NET/WPF desktop app for manual game-screen OCR and translation.

Persistent rules:

- Translation work starts only from an explicit user command.
- Do not add continuous screen polling, continuous OCR, frame similarity detection, debounce-driven translation, or background OCR loops.
- Keep the architecture small: explicit services, cancellation-aware async work, and focused tests.
- Keep Ollama local-only when Phase 3 starts; do not add cloud translation.
- Run build and tests after each phase.
- Update documentation when architecture changes.
- Put durable product and architecture details in `docs/`.

Reference docs:

- `docs/PRODUCT.md`
- `docs/ARCHITECTURE.md`
- `docs/PLAN.md`
