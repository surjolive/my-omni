# My Omni Native Windows Architecture

## Scope

My Omni is a native Windows Forms application compiled against the installed .NET Framework. It has no browser renderer, Electron host, VS Code webview, or extension package.

## Runtime components

- `MyOmniNative.cs`: WinForms workbench, source-file tabs with lightweight syntax coloring, project explorer, DPAPI-protected settings, and OmniRoute workspace agent.
- `build-native.ps1`: embeds the build machine's compatible Node executable and npm distribution, then compiles `release/MyOmni-final.exe` with the Windows .NET Framework C# compiler.
- `release/MyOmni-final.exe`: standalone native client. It has a docked explorer/editor/AI workbench and a hideable assistant sidebar. On explicit user action it installs the pinned OmniRoute npm package under `%LOCALAPPDATA%/My Omni/omniroute` and starts it bound to `127.0.0.1`.

## Security boundaries

- The OmniRoute API key is protected at rest with Windows DPAPI for the current user. It is not written into the project or passed to a shell.
- Workspace paths are selected with the native folder picker. Directory traversal, reparse-point files, binary content, oversized files and large workspaces are rejected or skipped.
- The model picker is populated from the authenticated OmniRoute `/models` response. Chat history, the capped active-file excerpt, and any workspace files the agent reads are sent to the configured OmniRoute provider.
- The workspace agent can list and read bounded source/text files under the selected workspace. It excludes hidden and common secret files and rejects linked paths. Proposed edits require a review dialog and are applied only to unsaved editor buffers; the user saves them explicitly.
- No command execution or terminal is exposed. Code execution remains unavailable until an isolated worker with resource limits and a tested filesystem/network boundary exists.

## Native build

Run `powershell -ExecutionPolicy Bypass -File .\build-native.ps1` from Windows PowerShell. The build requires the .NET Framework compiler, Node.js 24.15 or later, and npm. OmniRoute itself is downloaded at first launch and is not embedded in the EXE.
