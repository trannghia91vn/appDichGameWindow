# GameTranslator Agent Guide

GameTranslator is a Windows-only C#/.NET/WPF desktop app for local game-screen OCR and translation.

Persistent rules:

- Translation work starts only from an explicit manual command or while the user has explicitly enabled the overlay Realtime switch.
- Realtime must default off, stop when disabled or when the overlay is hidden, and never start at application startup.
- Keep realtime capture/OCR/translation sequential and cancellation-aware. Do not overlap or queue iterations, and do not add frame similarity detection or an independent background OCR worker.
- Keep the architecture small: explicit services, cancellation-aware async work, and focused tests.
- Keep Ollama local-only when Phase 3 starts; do not add cloud translation.
- Run build and tests after each phase.
- Update documentation when architecture changes.
- Put durable product and architecture details in `docs/`.

Reference docs:

- `docs/PRODUCT.md`
- `docs/ARCHITECTURE.md`
- `docs/PLAN.md`
