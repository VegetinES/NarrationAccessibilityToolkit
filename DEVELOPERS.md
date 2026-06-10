# Developers Guide

Thanks for contributing to NAT.

This document is for developers and collaborators who want to improve the plugin, validate accessibility behavior, and help the project evolve with real-world feedback.

## Project Priorities

- Priority 1: reliable accessibility behavior for players.
- Priority 2: predictable behavior across platforms.
- Priority 3: clear and maintainable code.

## Repository Structure

- `Assets/NAT/Runtime`: runtime narration logic.
- `Assets/NAT/Editor`: editor setup helpers and menu tools.
- `Assets/NAT/Resources/NarrationI18n`: i18n files (`es.json`, `en.json`).
- `Assets/NAT/Plugins`: native/platform bridge code.

## Collaboration Workflow

1. Create a branch from `main`.
2. Keep changes focused and reviewable.
3. Validate in Unity before opening a PR.
4. Open a PR with:
   - problem statement
   - implementation summary
   - tested platforms
   - manual validation steps

## Coding Style

- Prefer simple and explicit solutions.
- Keep classes and methods easy to scan.
- Add comments only when context is not obvious.
- Avoid unnecessary abstractions.

## Minimum Validation Checklist

- Unity compiles without errors.
- Manager creation from menu still works.
- `Narrator.SpeakLiteral(...)` works on tested target.
- UI keyboard/gamepad navigation is not broken.
- i18n JSON files remain valid after edits.

## Platform Validation Status

Validated:

- Windows with NVDA support, falling back to the current system narrator speech path
- Android with system TTS

Pending validation:

- iOS (expected to work)
- Linux (expected to work)

If you can test any pending target, your report is highly valuable.

## Documentation

- End-user setup and usage tutorial: [USER_GUIDE.md](USER_GUIDE.md)
- Public project summary: [README.md](README.md)
