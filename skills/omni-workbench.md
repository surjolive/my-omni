# Omni workbench skill

Use this skill for tasks involving the MY OMNI native desktop workbench and its AI features.

## Focus

- Preserve the native WinForms architecture.
- Keep OmniRoute and optional Ollama support compatible with their actual provider contracts.
- Add modular structure where possible without breaking the existing app.
- Favor secure, explicit user consent for destructive or network-sensitive operations.

## Verification checklist

- Build succeeds with the project toolchain.
- The app still launches.
- AI connection and model discovery remain safe and explicit.
- No file-system operation silently destroys user data.

## Working style

- Inspect before editing.
- Keep changes scoped.
- Prefer adapters and services over hardcoded implementations.
- Record actual evidence from build and run output when closing a task.
