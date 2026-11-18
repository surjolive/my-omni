# My Omni

My Omni is a native Windows coding workspace with a source editor, project explorer, and OmniRoute-backed AI chat. The workbench follows familiar editor, explorer, status bar, and assistant-sidebar patterns. It is built with Windows Forms and does not use a browser, Electron, React, or a VS Code extension.

## Screenshot

![MY OMNI welcome screen and AI chat sidebar](resources/screenshots/my-omni-main.png)

## Build

Requirements: Windows, PowerShell, the .NET Framework C# compiler, and Node.js 24.15 or later with npm installed. Build the self-contained native executable with:

```powershell
powershell -ExecutionPolicy Bypass -File .\build-native.ps1
```

The output is `release\MyOmni-0.1.0-Windows-x64.exe`. The EXE embeds the Node runtime and npm CLI available to the build script. OmniRoute itself is downloaded when you choose **Install / Start OmniRoute**; initial installation requires internet access and roughly 461 MB of disk space.

## Use

Launch `release\MyOmni-0.1.0-Windows-x64.exe`. The Open Editor welcome screen offers New File, Open File, Open Folder, and Git clone actions, and lists saved recent workspaces. Recent project paths and last-opened times are stored in `%LOCALAPPDATA%\My Omni\settings.json`. Cloning requires Git to be installed; choose a parent folder in the picker, and use Git Credential Manager or an SSH agent rather than putting credentials in a repository URL. The activity bar switches between Explorer, filename search, Git status, settings, accounts, and capability panels. The former toolbar actions are available from the File and View menus. Shortcuts include `Ctrl+N` (new), `Ctrl+O` (open file), `Ctrl+Shift+O` (open folder), `Ctrl+S` (save), `Ctrl+Shift+S` (Save As), `Ctrl+B` (Explorer sidebar), `Ctrl+Shift+I` (AI chat), and `Ctrl+Shift+P` (Command Palette).
Launch `release\MyOmni-0.1.0-Windows-x64.exe`. The Open Editor welcome screen offers New File, Open File, Open Folder, and Git clone actions, and lists saved recent workspaces. Recent project paths and preferences are stored in `%LOCALAPPDATA%\My Omni\settings.json`. Settings include OmniRoute startup, accent color, editor font size and wrapping, Explorer hidden files, and AI context/file limits. Cloning requires Git to be installed; choose a parent folder in the picker, and use Git Credential Manager or an SSH agent rather than putting credentials in a repository URL. Shortcuts include `Ctrl+N` (new), `Ctrl+O` (open file), `Ctrl+Shift+O` (open folder), `Ctrl+S` (save), `Ctrl+Shift+S` (Save As), `Ctrl+Shift+P` (Command Palette), `Ctrl+B` (Explorer), and `Ctrl+Shift+I` (AI chat).

If the pinned OmniRoute release is installed, My Omni starts the local gateway on launch and validates the saved API key. If auto-start is enabled and OmniRoute is missing or outdated, My Omni asks before downloading the pinned npm package and dependencies, then installs and starts the gateway when approved. You can also choose **Install / Start OmniRoute** at any time; after setup the dashboard opens so you can connect a provider and create an OmniRoute API key. Enter the key in the assistant panel and choose **Connect**. Connecting starts the installed gateway if it is stopped, validates the key, loads available models, and stores the key with Windows DPAPI for the current user. After saving, the password field is cleared; the profile status confirms that the key was saved and verified. Leave the field blank to keep the saved key, or enter a replacement. My Omni cannot create a gateway API key automatically; create it in the OmniRoute dashboard.

Use **Open Folder** to load a project, double-click a file in Explorer to open it, and press `Ctrl+S` to save. New untitled tabs use Save As on the first save. The editor provides lightweight syntax coloring for common languages, including C#, JavaScript/TypeScript, Python, JSON, SQL, HTML/XML, CSS, and PowerShell. The assistant supports three contexts: workspace tools when a folder is open; active-editor read and approved edits when only a file (including an untitled file) is active; and general programming chat with no local-file tools when neither is open. Workspace search, read, and proposed write/edit tools are bounded to readable workspace files. Edits require review; applying to the editor leaves changes unsaved, while **Apply & Save** explicitly writes through the atomic file writer. Workspace content sent to the assistant is routed through the configured OmniRoute provider; review that provider's data-handling policy before use.
Use **Open Folder** to load a project, double-click a file in Explorer to open it, and press `Ctrl+S` to save. New untitled tabs use Save As on the first save. The editor provides lightweight syntax coloring for common languages, including C#, JavaScript/TypeScript, Python, JSON, SQL, HTML/XML, CSS, and PowerShell. AI chat works in general mode without local-file tools, active-editor mode for standalone/untitled files, and workspace mode when a folder is open. Workspace tools search/read files and propose changes only within the open project; every edit requires review. Workspace content sent to the assistant is routed through the configured OmniRoute provider; review that provider's data-handling policy before use.

When the assistant proposes a file edit, review the complete before/after content and approve or cancel it. Approved edits are placed in editor tabs as unsaved changes; they are not written to disk until you save them with `Ctrl+S` or **Save All**. Common AI tool calls are supported through OmniRoute, but tool availability depends on the selected provider/model.

Code execution, an integrated terminal, an extension host, and Ollama transport are not included in this native build. The AI assistant cannot run commands or execute project code. Issue reporting has no configured tracker destination. This is a focused native workbench, not a feature-complete VS Code replacement.

## Troubleshooting

- **Install cannot reach npm:** check internet access and retry **Install / Start OmniRoute**.
- **Gateway is online but chat fails:** connect a provider in the OmniRoute dashboard and select a valid model from the dropdown.
- **Chat returns no assistant text:** check the selected model and provider configuration in the OmniRoute dashboard; the app now reports API error details when the gateway provides them.
- **No model aliases appear:** verify that the OmniRoute API key is valid and an eligible chat model is configured.
- **Port 20128 is already used:** stop the other OmniRoute instance or resolve that port conflict before starting the bundled gateway.

## Licenses

My Omni is dual-licensed under MIT OR Apache-2.0. See `LICENSE.md`, `LICENSE-MIT.md`, and `LICENSE-APACHE.md`. Runtime notices are in `THIRD_PARTY_NOTICES.md`.
