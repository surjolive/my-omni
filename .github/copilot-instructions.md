# MY OMNI Copilot instructions

## Project purpose

MY OMNI is a Windows-native workbench with a secure AI assistant, a project explorer, text editing, and OmniRoute-backed conversation support. Preserve the existing .NET WinForms code and avoid unnecessary rewrites.

## Coding expectations

- Keep the app architecture aligned to the native Windows desktop model.
- Prefer small, verifiable feature additions over large speculative rewrites.
- Protect user secrets and never leak API keys in source or logs.
- Ask for explicit approval before destructive file-system actions.
- Validate compilation and behavior after edits.

## Feature conventions

- File-system changes must reflect real on-disk state.
- AI requests must keep provider credentials out of client-side code.
- Use the current OmniRoute contract and model discovery flow instead of guessing endpoints.
- Keep the workspace and terminal modules isolated from the AI provider transport layer.

## Hard constraints

- Do not claim the full VS Code IDE stack is implemented when the repo only contains the native workbench scaffold.
- Do not fabricate runtime behavior such as fake terminal output or fake AI responses.
- Do not overwrite user files without confirmation.
