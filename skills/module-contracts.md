# MY OMNI module contracts skill

Use this skill when adding or changing code under `src/`, `electron/`, `resources/`, `scripts/`, `tests/`, or `build/`.

## Project boundary

The supported application remains the native Windows Forms workbench implemented in `MyOmniNative.cs`. The TypeScript and Electron files are forward-looking contracts; they are not connected to the native app and do not make those features available. Preserve that distinction in code, documentation, and release claims. Do not start an Electron migration unless the user explicitly requests it.

## Module responsibilities

- `src/core/application`: application lifecycle and state.
- `src/core/workspace`: workspace selection and bounded file/directory operations.
- `src/core/configuration`: persisted settings and references to protected secrets.
- `src/core/services`: typed service registration and resolution.
- `src/editor/monaco`: editor host lifecycle and text access.
- `src/editor/tabs`: open, activate, save, and close editor tabs.
- `src/editor/split-editor`: editor groups and tab placement.
- `src/editor/autocomplete`: completion-provider registration and requests.
- `src/editor/formatting`: formatter registration and document formatting.
- `src/explorer`: workspace tree presentation and refresh.
- `src/search`: workspace search and preview-before-replace.
- `src/terminal`: terminal session lifecycle; do not enable command execution without a tested isolation boundary.
- `src/debugger`: debug-session controls; do not claim a debugger exists until a real adapter is connected.
- `src/extensions`: extension discovery and lifecycle; require trust and consent for third-party code.
- `src/git`: repository state and explicit mutations such as commit or checkout.
- `src/panels`: workbench panel registration and visibility.
- `src/themes`: theme discovery and activation.
- `src/settings`: user preference access and change notifications.
- `src/preview`: preview lifecycle; do not execute untrusted content in the privileged desktop context.
- `src/ai/omniroute`: OmniRoute connection, model discovery, and chat transport.
- `src/ai/ollama`: local Ollama connection, model discovery, and chat transport.
- `src/ai/agents`: planning and consent-gated execution of proposed actions.
- `src/ai/chat`: conversation lifecycle and message sending.
- `src/ai/context`: bounded, explicitly selected context collection.
- `src/ai/tools`: tool discovery and approval-gated invocation.
- `src/ui`: view lifecycle, notifications, and explicit confirmation prompts.
- `src/shared`: small cross-module types only; avoid moving feature behavior here.
- `electron/main`: desktop application lifecycle.
- `electron/preload`: narrow renderer bridge; expose only approved capabilities.
- `electron/ipc`: typed request routing between trusted desktop components.
- `electron/security`: renderer, path, and operation policy checks.
- `electron/services`: desktop-specific service composition and disposal.
- `resources/icons`, `resources/themes`, `resources/images`: static assets only.
- `scripts`: development and maintenance automation; document side effects and avoid destructive defaults.
- `tests`: contract and behavior tests; never use real credentials or mutate user workspaces.
- `build`: generated build output only; do not place hand-maintained source here.

## Implementation rules

- Treat exported interfaces in module `index.ts` files as contracts, not implementations. Add real behavior behind the owning module and wire it only through an explicit application composition root.
- Keep filesystem access inside workspace services. Normalize and confine paths to the selected workspace, reject traversal and reparse points, cap file sizes, and require explicit confirmation before destructive operations.
- Never store API keys in source, ordinary settings, logs, command arguments, or renderer-accessible state. Pass protected credential references to provider adapters; keep DPAPI handling in the native host unless a separately reviewed desktop secret store is implemented.
- Discover provider models from the provider's authenticated/local model endpoint. Do not guess endpoints or fabricate responses.
- Do not pass arbitrary IPC channels through preload. Use an allowlisted, typed bridge, validate every payload in the host, and keep renderer privileges disabled.
- Do not implement shell execution, debugging, extension execution, agent actions, or previewed scripts as stubs that report success. These require real adapters, explicit user consent, and tested isolation/security boundaries.
- For bulk replacement or repository changes, show the proposed changes before applying them and preserve the ability to cancel.
- Keep APIs cancellable and bounded for workspace search, provider requests, and context gathering where practical.
- Add focused tests for behavior and security boundaries. Build and verify the native app when changing its C# implementation; verify TypeScript contracts separately and do not treat that as native app validation.

## Verification

- TypeScript changes: run the configured TypeScript build and relevant tests when dependencies are available.
- Native changes: follow `skills/omni-workbench.md` and run `build-native.ps1` on Windows with the documented prerequisites.
- Report what was actually built and tested. An interface-only contract does not prove a feature is operational.
